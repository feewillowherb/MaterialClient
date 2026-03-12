## Context

项目当前没有任何 Excel 导出能力。现有的 Magicodes.IE 使用仅限于 `MaterialClient.Toolkit` 项目中的 CSV 导入（`CsvReaderService`）。固废模式的运单数据需要按 `sample.csv` 模板格式导出为 `.xlsx` 文件，涉及 17 列字段映射、关联实体查询和末尾汇总行。

### 数据映射分析

`sample.csv` 的 17 列字段来源如下：

| # | 列名 | 数据来源 | 说明 |
|---|------|---------|------|
| 1 | 流水号 | `Waybill.OrderNo` | 格式如 A202603040001 |
| 2 | 车号 | `Waybill.PlateNumber` | |
| 3 | 发货单位 | `Provider.ProviderName` | 通过 `Waybill.ProviderId` 关联 |
| 4 | 收货单位 | `Waybill.GetShipper()` | ExtraProperties，默认"东部资源化处置点" |
| 5 | 货名 | `Material.Name` | 通过 ExtraProperties `SolidWasteInfo.MaterialId` 关联 |
| 6 | 毛重 | `Waybill.OrderTotalWeight` | |
| 7 | 皮重 | `Waybill.OrderTruckWeight` | |
| 8 | 净重 | `Waybill.OrderGoodsWeight` | |
| 9 | 备注 | `Waybill.Remark` | |
| 10 | 毛重时间 | `Waybill.JoinTime` | 格式 yyyy-MM-dd HH:mm:ss |
| 11 | 皮重时间 | `Waybill.OutTime` | 格式 yyyy-MM-dd HH:mm:ss |
| 12 | 所属街道 | `Waybill.GetStreet()` | ExtraProperties |
| 13 | 类型 | `Waybill.GetSolidWasteType()` | ExtraProperties |
| 14 | 联单编号 | `Waybill.GetSolidWasteOrderNumber()` | ExtraProperties |
| 15 | 上传结果 | 固定为空 | 不填充数据 |
| 16 | 上传状态 | 固定为空 | 不填充数据 |
| 17 | 上传时间 | 固定为空 | 不填充数据 |

末尾汇总行：第 1 列为总车次数，第 6-8 列为毛重/皮重/净重合计，其余列留空。

## Goals / Non-Goals

**Goals:**
- 提供 `ISolidWasteExcelExportService` 接口，支持多条件可空过滤导出固废运单为 `.xlsx`
- 严格对齐 `sample.csv` 模板的列结构和汇总行格式
- 正确解析 `Waybill.ExtraProperties` 中的 SolidWaste 扩展字段
- 通过关联查询获取 `Provider.ProviderName` 和 `Material.Name`

**Non-Goals:**
- 不实现 Standard 模式的导出（仅 SolidWaste）
- 不实现 Excel 导入功能
- 不实现 UI 层的导出按钮和文件选择对话框（本次仅服务层接口）
- 不处理导出文件的样式美化（如字体、颜色、边框等）

## Decisions

### Decision 1：第三方库选择 — ClosedXML

**评估对比：**

| 维度 | ClosedXML | Magicodes.IE.Excel |
|------|-----------|-------------------|
| **License** | MIT | MIT (底层依赖 Magicodes.EPPlus) |
| **NuGet 下载量** | ~3.3 亿（极高人气） | ~200 万 |
| **API 风格** | 命令式，逐行/逐单元格操作 | 声明式，DTO + Attribute 装饰 |
| **模板/汇总行支持** | 原生支持，完全控制任意行 | 需额外处理，Attribute 模式不直接支持汇总行 |
| **依赖链** | OpenXML SDK（微软官方） | EPPlus fork（Magicodes.EPPlus） |
| **.NET 10 兼容性** | 支持 .NET 6+，持续更新 | 目标 .NET Standard 2.0 / .NET 6.0 |
| **项目内已有** | 无 | 已有 Magicodes.IE.Csv（仅 Toolkit 项目） |
| **灵活度** | 高：格式化、合并单元格、公式等 | 中：简单导出优秀，复杂场景需手动处理 |

**选择 ClosedXML 的理由：**

1. **汇总行需求**：模板末尾需要自定义汇总行（总车次 + 三列合计），ClosedXML 可以直接在数据行之后追加任意内容。Magicodes.IE.Excel 的 Attribute 导出模式不原生支持此模式，需要手动操作 EPPlus 对象，失去了声明式的优势。

2. **字段映射复杂度**：17 列中有 5 列来自 `ExtraProperties`（需要扩展方法解析），2 列需要关联实体查询，2 列需要状态派生。无论使用哪个库都需要先构建中间 DTO，Magicodes.IE 的 Attribute 优势被中间 DTO 抵消。

3. **维护风险**：ClosedXML 基于微软官方 OpenXML SDK，社区活跃度极高。Magicodes.IE.Excel 底层依赖 Magicodes.EPPlus（EPPlus 的 fork），EPPlus 在 v5 后改为商业许可，fork 的长期维护存在不确定性。

4. **项目隔离**：现有 Magicodes.IE.Csv 仅在 `MaterialClient.Toolkit` 中使用（CSV 导入），与 `MaterialClient.Common` 无关。选择 ClosedXML 不引入不一致性。

**放弃 Magicodes.IE.Excel 的理由：**

- Attribute 模式对本场景优势不明显（字段映射复杂，需中间 DTO）
- 汇总行支持需绕过声明式 API，增加额外复杂度
- EPPlus fork 的长期维护不确定

### Decision 2：服务接口设计

```
ISolidWasteExcelExportService
├── ExportAsync(filter, outputPath) → Task<ExportResult>

SolidWasteExportFilter（所有参数可空）
├── DateTime?  StartDate      // AddDate 起始日期
├── DateTime?  EndDate        // AddDate 截止日期
├── string?    PlateNumber    // 车牌号（模糊匹配）
├── string?    GoodsName      // 货名（模糊匹配，匹配 Material.Name）
├── string?    ProviderName   // 发货单位（模糊匹配，匹配 Provider.ProviderName）
```

- 接口放置在 `MaterialClient.Common/Services/`
- 输入：`SolidWasteExportFilter`（全部可空过滤条件）和输出文件路径
- 输出：`ExportResult` 包含导出行数、文件路径、是否成功
- 固定过滤：`WeighingMode == SolidWaste` + `OrderType == Completed`（不可空，始终生效）
- 可空过滤：日期范围过滤 `Waybill.AddDate`，车牌号/货名/发货单位为模糊匹配（Contains）
- 当所有可空参数均为 null 时，导出全部符合固定条件的数据
- 通过 Repository 关联查询 `Provider` 和 `Material` 以获取名称

### Decision 4：上传相关列固定为空

`sample.csv` 模板中的上传结果（第 15 列）、上传状态（第 16 列）、上传时间（第 17 列）在导出时固定输出为空字符串。这三列保留在模板结构中以保持与纸质台账格式一致，但不从数据库填充数据。

### Decision 3：中间 DTO 设计

定义 `SolidWasteExportRow` 作为中间数据传输对象，与 `sample.csv` 的列一一对应。服务层负责将 `Waybill` + 关联实体 + ExtraProperties 映射为此 DTO，然后由 ClosedXML 写入 Excel。

这样分离的好处：
- 映射逻辑可独立于 Excel 库进行单元测试
- 如果未来需要其他格式导出（PDF、CSV），复用同一个 DTO

## Risks / Trade-offs

| 风险 | 缓解措施 |
|------|---------|
| ClosedXML 为新增依赖，增加包体积 | 仅在需要导出时加载，且包体积可控（~2MB） |
| ExtraProperties 字段可能为 null（数据不完整） | 映射时统一做 null 安全处理，输出空字符串 |
| 大数据量导出可能内存占用高 | 首期限定单次导出上限，后续可改为流式写入 |
| `SolidWasteInfo.MaterialId` 关联的 Material 可能已删除 | 查询时 left join，找不到时输出空 |
| 毛重/皮重的含义在 Receiving vs Sending 模式下不同 | 固废模式默认为 Receiving，直接使用 OrderTotalWeight/OrderTruckWeight |
