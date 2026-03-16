## ADDED Requirements

### Requirement: 通用 Excel 导出接口
系统 SHALL 提供与业务无关的“将表头与行数据写入 .xlsx 文件”的通用能力，以便多业务复用。该能力 SHALL 通过接口（如 `IExcelExportService` 或等效）暴露，接受列定义（表头文本）、数据行集合以及行到列值的映射方式，不包含具体业务（如固废）的查询或领域逻辑。

#### Scenario: 按表头与行数据写入文件
- **WHEN** 调用方传入输出路径、表头数组、行数据集合以及将每行转换为列值数组的映射
- **THEN** 系统 SHALL 生成 .xlsx 文件：第一行为表头，后续行为映射后的列值，不依赖固废或其它业务类型

#### Scenario: 业务层注入不同数据源
- **WHEN** 不同业务（如固废导出、后续其它导出）使用本通用能力
- **THEN** 各业务通过各自接口暴露（如 `ISolidWasteExcelExportService`），并注入各自数据源（如 SolidWasteService）与列定义，通用导出仅负责按给定表头与行数据写入文件

### Requirement: 与业务解耦
通用 Excel 导出实现 SHALL 不依赖 Waybill、SolidWasteExportFilter、SolidWasteExportRow 等业务类型；仅依赖写 Excel 所需的技术能力（如 ClosedXML）及调用方提供的表头与行枚举。业务特有的汇总行、多 Sheet 等可由业务门面在调用通用导出前后自行处理，或通过扩展参数/回调提供。

#### Scenario: 固废导出门面使用通用导出
- **WHEN** 固废 Excel 导出门面调用本通用能力
- **THEN** 门面 SHALL 先通过 SolidWasteService 获取行数据，再将固废 17 列表头与行→列值映射传给通用导出；汇总行可由门面在通用导出写入后追加，或由通用导出支持的可选逻辑处理
