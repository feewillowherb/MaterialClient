## Why

固废模块 Excel 导出功能在发料模式（`DeliveryType.Sending`）下，发货单位和收货单位未按业务规则对调，导致导出数据错误。同时，部分领域扩展方法（`GetStreet`/`SetStreet`、`GetShipper`/`SetShipper`）缺少 `SolidWaste` 前缀，命名边界不清；发货/收货单位的确定逻辑分散在 Service 层（`SolidWasteService.MapToExportRow` 和 `WeighingMatchingService.CreateWeighingTicketDtoAsync` 两处重复实现），违反 DDD 原则。

## What Changes

- **修复 Excel 导出 Bug**：`SolidWasteService.MapToExportRow` 未根据 `DeliveryType` 对调发货/收货单位，发料模式下两个字段值错误
- **将发货/收货单位业务规则迁移至领域模型**：在 `SolidWasteInfoExtensions` 中新增 `GetSolidWasteUnits` 方法，封装"收料模式：发货=providerName，收货=shipper；发料模式：对调"的规则，消除 Service 层重复逻辑
- **方法命名规范化**：为 `SolidWasteInfoExtensions` 中的 `GetStreet`/`SetStreet`、`GetShipper`/`SetShipper` 添加 `SolidWaste` 前缀，同步更新 `ISolidWasteInfo` 接口和所有调用方
- **BREAKING**：`ISolidWasteInfo` 接口方法签名变更（`GetStreet` → `GetSolidWasteStreet`，`GetShipper` → `GetSolidWasteShipper` 等），所有实现和调用方需同步更新

## Capabilities

### New Capabilities

_(无新增能力)_

### Modified Capabilities

- `solidwaste-excel-export`：修复发料模式下发货/收货单位映射错误；导出列结构从 17 列扩展到 18 列（增加"称重类型"列）；发货/收货单位确定逻辑迁移至领域模型 `SolidWasteInfoExtensions`

## Impact

**受影响的文件（约 8 个）：**

| 文件路径 | 变更类型 | 变更原因 | 影响范围 |
|-----------|----------|----------|----------|
| `MaterialClient.Common/Entities/SolidWasteInfoExtensions.cs` | 修改 | 方法重命名 + 新增 `GetSolidWasteUnits` 领域方法 | 领域层核心 |
| `MaterialClient.Common/Entities/ISolidWasteInfo.cs` | 修改 | 接口方法重命名 | 接口层 |
| `MaterialClient.Common/Services/SolidWasteService.cs` | 修改 | 调用新领域方法修复 Bug | Service 层 |
| `MaterialClient.Common/Services/WeighingMatchingService.cs` | 修改 | 调用重命名后的扩展方法 + 复用领域逻辑 | Service 层 |
| `MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs` | 修改 | 调用重命名后的扩展方法 | UI 层 |
| `MaterialClient.Common.Tests/Tests/SolidWasteExcelExportTests.cs` | 修改 | 更新测试以匹配重命名 | 测试层 |
| `MaterialClient.Common.Tests/Tests/WeighingMatchingServiceSolidWasteTransferTests.cs` | 修改 | 更新测试以匹配重命名 | 测试层 |
| `MaterialClient/Common/Services/ExcelExportService.cs` | 无变更 | 通过 ISolidWasteService 间接调用，无直接依赖 | — |

**无 API 变更、无数据库迁移、无新增依赖。**
