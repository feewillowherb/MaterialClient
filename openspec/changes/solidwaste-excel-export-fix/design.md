## Context

固废模块 Excel 导出在发料模式下发货/收货单位未按业务规则对调，导致导出数据错误。当前 `SolidWasteService.MapToExportRow` 仅简单赋值 `ShippingUnit = providerName`、`ReceivingUnit = waybill.GetShipper()`，未考虑 `DeliveryType.Sending` 的对调逻辑。

`WeighingMatchingService.CreateWeighingTicketDtoAsync`（第 178-199 行）已正确实现了该对调逻辑，但属于 Service 层的重复业务判断，违反 DDD 原则。

`SolidWasteInfoExtensions` 中 `GetStreet`/`SetStreet`、`GetShipper`/`SetShipper` 方法缺少 `SolidWaste` 前缀，命名边界不清。

## Goals / Non-Goals

**Goals:**

1. 修复 Excel 导出在发料模式下的发货/收货单位映射错误
2. 在 `SolidWasteInfoExtensions` 中新增 `GetSolidWasteShippingAndReceivingUnits` 领域方法，封装"收料/发料模式下发货/收货单位"的确定规则
3. `SolidWasteService.MapToExportRow` 和 `WeighingMatchingService.CreateWeighingTicketDtoAsync` 统一调用新领域方法，消除重复逻辑
4. 为 `GetStreet`/`SetStreet`、`GetShipper`/`SetShipper` 添加 `SolidWaste` 前缀，同步更新接口和所有调用方

**Non-Goals:**

- 不新增 API 端点
- 不修改数据库 schema
- 不更新文档
- 不新增单元测试

## Decisions

### Decision 1：领域方法 `GetSolidWasteShippingAndReceivingUnits` 放置于 `SolidWasteInfoExtensions`

**选择**：在 `SolidWasteInfoExtensions` 中新增静态扩展方法 `GetSolidWasteShippingAndReceivingUnits(this Waybill waybill, string providerName)`，返回 `(string ShippingUnit, string ReceivingUnit)` 元组。

**备选方案**：
- _(A) 放在 Waybill 实体内部_：Waybill 不应依赖外部字符串常量（DefaultShipper），且已有扩展方法模式
- _(B) 放在独立值对象_：过度设计，当前仅一个方法

**理由**：`SolidWasteInfoExtensions` 已承载所有固废领域扩展逻辑，保持一致性。方法签名接收 `providerName` 参数，因为 Provider 名称来自外部字典查询，不属于 Waybill 自身数据。

**规则实现**：
```
收料模式 (DeliveryType.Receiving)：
  ShippingUnit = providerName
  ReceivingUnit = waybill.GetProperty<string>(ShipperKey) ?? DefaultShipper

发料模式 (DeliveryType.Sending)：
  ShippingUnit = waybill.GetProperty<string>(ShipperKey) ?? DefaultShipper
  ReceivingUnit = providerName
```

### Decision 2：重命名方案 — `GetStreet` → `GetSolidWasteStreet`、`GetShipper` → `GetSolidWasteShipper`

**选择**：为 WeighingRecord 和 Waybill 两套扩展方法统一添加 `SolidWaste` 前缀。

**映射表**：

| 原方法名 | 新方法名 |
|----------|----------|
| `SetStreet` | `SetSolidWasteStreet` |
| `GetStreet` | `GetSolidWasteStreet` |
| `SetShipper` | `SetSolidWasteShipper` |
| `GetShipper` | `GetSolidWasteShipper` |

`ISolidWasteInfo` 接口同步更新方法签名。

### Decision 3：`SolidWasteService.MapToExportRow` 重命名为 `SolidWasteMapToExportRow`

**选择**：添加 `SolidWaste` 前缀，使方法名明确表达其固废专用性质。

**理由**：根据需求规范，SolidWasteService 中所有方法均需添加前缀。

### Decision 4：`SolidWasteService` 中的 private 方法也添加前缀

`QueryWaybillsAsync` → `SolidWasteQueryWaybillsAsync`
`BuildProviderDictAsync` → `SolidWasteBuildProviderDictAsync`
`BuildMaterialDictAsync` → `SolidWasteBuildMaterialDictAsync`
`MapToExportRow` → `SolidWasteMapToExportRow`

接口方法（`GetExportRowsAsync`、`GetPagedExportRowsAsync`）保持不变，因接口定义已明确属于 SolidWaste 上下文。

## Risks / Trade-offs

- **[Breaking Change]** → 接口 `ISolidWasteInfo` 方法签名变更，所有实现/调用方需同步更新。已确认受影响文件约 8 个，可控。
- **[ProviderDict 依赖]** → 新领域方法需接收 `providerName` 参数而非 ProviderId，因为扩展方法无法执行异步 DB 查询。Service 层负责从字典中获取 providerName 后传入。这是合理的职责分离。
- **[DefaultShipper 常量暴露]** → `GetSolidWasteShippingAndReceivingUnits` 内部使用 `DefaultShipper`，该常量仍为 `private const`，不对外暴露，封装不变。
