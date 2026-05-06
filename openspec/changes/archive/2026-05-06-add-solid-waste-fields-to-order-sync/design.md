## Context

`SynchronizationOrderInputDto` 是订单同步的请求体，通过 `FromWaybill()` 静态方法从 `Waybill` 实体转换而来，用于 `POST /api/Order/SynchronizationOrder` 和 `POST /api/Order/SynchronizationModifyOrder` 两个接口。

当前 `Waybill` 实体已通过 `IHasExtraProperties` 存储固废信息（`SolidWasteType`、`Street`、`SolidWasteOrderNumber`、`Shipper`），并通过 `SolidWasteInfoExtensions` 提供读写方法。`Waybill.WeighingMode` 枚举字段（`Standard=0` / `SolidWaste=1`）也已存在。但 `FromWaybill()` 转换未涉及这些字段，固废订单同步时服务端无法区分称重模式或获取固废业务信息。

## Goals / Non-Goals

**Goals:**
- 在 `SynchronizationOrderInputDto` 上新增 `WeighingMode` 和 `SolidWasteInfo` 字段，使服务端能区分标准/固废订单
- `FromWaybill()` 自动将 `Waybill` 的固废信息映射到 DTO 的嵌套 `SolidWasteInfoDto` 中
- 保持向后兼容：标准模式下 `WeighingMode=null`，`SolidWasteInfo=null`，请求体结构与现有行为一致
- 固废联单编号长度校验（≤100 字符）在 DTO 转换阶段执行

**Non-Goals:**
- 服务端 API 的实现（仅客户端侧 DTO 和转换逻辑变更）
- 固废信息录入 UI 的开发
- 固废模式的业务流程变更（匹配、称重等已有逻辑不变）

## Decisions

### 1. 新建独立 `SolidWasteInfoDto` 类 vs 内联对象

**决定**：新建 `SolidWasteInfoDto` 类。

**理由**：固废信息有 4 个独立属性，且服务端接口要求嵌套 JSON 对象结构。独立类提供类型安全、可复用的映射逻辑，与项目中 `OrderGoodsDto` 等嵌套 DTO 的风格一致。

**替代方案**：使用 `Dictionary<string, string>` 或 `JObject` — 丧失类型安全和 IDE 补全，不推荐。

### 2. 校验逻辑放置位置

**决定**：在 `FromWaybill()` 方法中执行联单编号长度校验，在 `SolidWasteInfoDto` 上不使用 Data Annotations。

**理由**：
- 联单编号校验（≤100 字符）与 `SolidWasteInfoExtensions.SetSolidWasteOrderNumber()` 中的现有校验逻辑一致，属于数据转换阶段的关注点
- `FromWaybill()` 是唯一的 DTO 构建入口，在此处校验可确保所有同步路径均受保护
- 服务端可能有自己的校验，客户端侧做前置校验可提前失败、减少无效网络请求

### 3. `WeighingMode` 的序列化类型

**决定**：使用 `int?`（与现有 `OrderType`、`DeliveryType` 等枚举字段风格一致），`null` 表示标准模式（向后兼容），`1` 表示固废模式。

**理由**：
- 与 DTO 中其他枚举字段（`OrderType`、`DeliveryType`）的序列化方式保持一致
- `null` 作为默认值确保标准模式订单的请求体不发生变化
- 无需引入 `JsonConverter` 或自定义序列化逻辑

### 4. `SolidWasteInfoDto` 的放置位置

**决定**：与 `SynchronizationOrderInputDto` 放在同一文件 `SynchronizationOrderInputDto.cs` 中。

**理由**：`SolidWasteInfoDto` 仅作为 `SynchronizationOrderInputDto` 的嵌套属性使用，不独立暴露。与 `OrderGoodsDto` 的组织方式一致（同一文件内定义多个相关 DTO）。

## Risks / Trade-offs

- **[服务端兼容性]** → 服务端必须在 `SynchronizationOrder` / `SynchronizationModifyOrder` 接口上接受新增字段。如果服务端尚未部署，JSON 序列化时会自动忽略未知字段（默认行为），因此不会导致请求失败。但固废信息将丢失。
- **[数据完整性]** → 如果 `Waybill` 的固废扩展属性未正确设置（如 `WeighingMode=SolidWaste` 但缺少固废类型），`SolidWasteInfoDto` 将包含 null 值。这不是阻断性问题，因为固废字段的必填校验由 UI 层保证。
- **[向后兼容]** → `WeighingMode` 为 `int?` 且默认 `null`，`SolidWasteInfo` 为引用类型默认 `null`，标准模式订单的 JSON 输出与现有行为完全一致，无破坏性变更。
