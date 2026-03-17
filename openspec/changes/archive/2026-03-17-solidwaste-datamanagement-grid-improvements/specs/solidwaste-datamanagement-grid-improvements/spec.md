## ADDED Requirements

### Requirement: Solid waste list uses ABP paging format
固废分页查询接口的返回结果必须采用项目中统一的 ABP 标准分页结构，并且分页总数字段 `totalCount` 的类型为 `long`，以便在大数据量场景下保持统计精度，并与平台分页契约保持一致。

#### Scenario: Get paged solid waste list
- **WHEN** 客户端调用固废分页查询接口并提供有效的分页参数（页码与每页条数）
- **THEN** 系统返回包含 `totalCount`(long) 和 `items` 字段的结果对象，其中 `items` 为当前页固废记录集合，`totalCount` 为符合筛选条件的记录总数

### Requirement: Date filter shows full value
固废数据管理对话框中的日期筛选控件必须在常见窗口宽度下完整展示日期文本，不允许被布局压缩到难以辨认。

#### Scenario: View date filter in default layout
- **WHEN** 用户在默认窗口大小下打开固废数据管理对话框并查看日期筛选控件
- **THEN** 日期文本在控件中完整可见，无明显截断，且不与相邻控件产生重叠

### Requirement: Columns in solid waste grid are resizable
固废数据管理对话框中的数据列表列必须支持用户拖拽调整宽度，并提供合理的最小宽度以保证可读性。

#### Scenario: User resizes grid column
- **WHEN** 用户在固废数据管理对话框中将鼠标移动到列表列边界并进行拖拽
- **THEN** 列宽随拖拽方向变化，且各列宽度调整后仍保持不小于定义的最小宽度，列表内容依然可读

