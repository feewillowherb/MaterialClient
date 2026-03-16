## ADDED Requirements

### Requirement: 固废列表数据唯一数据源
系统 SHALL 提供固废导出行数据的唯一数据源服务（如 `ISolidWasteService`），供数据管理对话框与固废 Excel 导出门面共同使用。该服务 SHALL 接受 `SolidWasteExportFilter` 过滤条件，返回与固废 Excel 导出一致的行模型列表（如 `SolidWasteExportRow`），并封装 Waybill 查询、Provider/Material 关联与行映射逻辑，不在其它服务内重复实现上述查询与映射。

#### Scenario: 按筛选条件返回导出行列表
- **WHEN** 调用方传入 `SolidWasteExportFilter`（含可空的起止日期、车牌号、货名、发货单位）
- **THEN** 系统 SHALL 按与现有固废导出相同的过滤规则查询 Waybill（`WeighingMode == SolidWaste` 且 `OrderType == Completed`，以及 filter 中各可空条件），将结果映射为 `SolidWasteExportRow` 列表并返回

#### Scenario: 与导出服务使用相同过滤与映射规则
- **WHEN** 使用相同的 `SolidWasteExportFilter` 分别调用本服务与固废 Excel 导出
- **THEN** 本服务返回的行列表与导出写入 Excel 的行数据在列、顺序、内容上 SHALL 一致，保证对话框预览与导出的数据来源一致

#### Scenario: 过滤参数为 null 时返回全部符合条件的运单行
- **WHEN** 调用时 `SolidWasteExportFilter` 的所有属性均为 null
- **THEN** 系统 SHALL 返回全部满足 `WeighingMode == SolidWaste` 且 `OrderType == Completed` 的运单所对应的 `SolidWasteExportRow` 列表

### Requirement: 服务注册方式
本服务的实现类 SHALL 实现 `Volo.Abp.DependencyInjection.ITransientDependency`，由 ABP 按约定隐式注册；不通过扩展方法（如 `AddSolidWasteExportServices`）或 Module 的 `ConfigureServices` 显式注册。若有构造注入依赖，实现类 SHALL 标注 `[AutoConstructor]`。

### Requirement: 可空过滤参数定义
本服务 SHALL 使用与现有固废导出一致的 `SolidWasteExportFilter` 定义，包含以下全部可空参数：`DateTime? StartDate`、`DateTime? EndDate`、`string? PlateNumber`、`string? GoodsName`、`string? ProviderName`。语义与现有导出规范一致（AddDate 范围、车牌号/货名/发货单位模糊匹配）。

#### Scenario: 车牌号模糊匹配
- **WHEN** 过滤条件中 `PlateNumber` 非空
- **THEN** 系统 SHALL 仅返回 `Waybill.PlateNumber` 包含该值的运单对应行

#### Scenario: 货名与发货单位模糊匹配
- **WHEN** 过滤条件中 `GoodsName` 或 `ProviderName` 非空
- **THEN** 系统 SHALL 分别按关联 Material.Name、Provider.ProviderName 的包含关系过滤，与现有固废导出行为一致
