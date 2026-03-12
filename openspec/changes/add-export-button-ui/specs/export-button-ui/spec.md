## ADDED Requirements

### Requirement: 导出按钮仅固废模式可见
系统 SHALL 在有人值守界面（AttendedWeighingWindow）顶部菜单栏"数据同步"按钮之后渲染一个"导出"按钮。该按钮仅在当前称重模式为 `WeighingMode.SolidWaste` 时可见，标准模式（`WeighingMode.Standard`）下 SHALL 不渲染。

#### Scenario: 固废模式下显示导出按钮
- **WHEN** 系统配置 `SystemSettings.DefaultWeighingMode` 为 `SolidWaste`
- **THEN** 顶部菜单栏"数据同步"按钮右侧 SHALL 显示"导出"按钮，样式与其他菜单按钮一致（`transparent-button`、白色前景）

#### Scenario: 标准模式下隐藏导出按钮
- **WHEN** 系统配置 `SystemSettings.DefaultWeighingMode` 为 `Standard`
- **THEN** 顶部菜单栏 SHALL 不显示"导出"按钮

#### Scenario: 标准模式 TODO 预留
- **WHEN** 代码中处理导出逻辑时
- **THEN** SHALL 包含 `// TODO: 支持标准模式导出` 注释，标记未来扩展点

### Requirement: 点击导出弹出过滤条件对话框
系统 SHALL 在用户点击"导出"按钮后弹出一个模态对话框，包含与 `SolidWasteExportFilter` 对应的过滤条件输入字段。

#### Scenario: 弹出过滤条件对话框
- **WHEN** 用户点击"导出"按钮
- **THEN** 系统 SHALL 弹出一个模态对话框（`ShowDialog`），包含以下输入字段：
  - 日期范围：起始日期（StartDate）和截止日期（EndDate），使用 `DateTimePicker` 控件
  - 车牌号（PlateNumber）：文本输入框
  - 保存位置：目录路径文本框 + [浏览] 按钮
  - "导出"按钮和"取消"按钮
- **AND** 货名（GoodsName）和发货单位（ProviderName）SHALL 不在对话框中展示，调用导出服务时始终以 null 传入

#### Scenario: 对话框取消操作
- **WHEN** 用户在过滤条件对话框中点击"取消"
- **THEN** 对话框 SHALL 关闭，不执行任何导出操作

### Requirement: 过滤条件对话框样式与 WeighingRecordListView 一致
过滤条件对话框的 UI 样式 SHALL 与 `WeighingRecordListView` 中的搜索过滤区域保持一致。

#### Scenario: 标签样式一致
- **WHEN** 过滤条件对话框渲染
- **THEN** 所有字段标签 SHALL 使用 `FontSize="13"` 和 `Foreground="#666"`

#### Scenario: 日期选择器样式一致
- **WHEN** 过滤条件对话框渲染日期字段
- **THEN** SHALL 使用 Ursa 的 `u:DateTimePicker` 控件

#### Scenario: 按钮样式一致
- **WHEN** 过滤条件对话框渲染按钮区域
- **THEN** "导出"按钮 SHALL 使用 `primary-button` 样式，"取消"按钮 SHALL 使用 `secondary-button` 样式

### Requirement: 保存路径字段
对话框 SHALL 包含「保存位置」字段，用户可输入或通过 [浏览] 按钮选择导出目录。

#### Scenario: 浏览按钮选择目录
- **WHEN** 用户点击 [浏览] 按钮
- **THEN** 系统 SHALL 通过 `StorageProvider.OpenFolderPickerAsync` 打开目录选择对话框
- **AND** 用户选择目录后 SHALL 将路径填入「保存位置」字段

#### Scenario: 首次使用默认路径
- **WHEN** `SystemSettings.ExportDefaultPath` 为空或不存在
- **THEN** 对话框打开时「保存位置」SHALL 默认填入桌面路径（`Environment.GetFolderPath(Desktop)`）

#### Scenario: 读取已记忆路径
- **WHEN** `SystemSettings.ExportDefaultPath` 有值
- **THEN** 对话框打开时「保存位置」SHALL 默认填入该值

#### Scenario: 保存路径为空时校验拦截
- **WHEN** 用户点击"导出"且「保存位置」字段为空
- **THEN** 系统 SHALL 不执行导出
- **AND** 「保存位置」字段 SHALL 显示红色边框和提示文本「请选择保存位置」

#### Scenario: 自动生成文件名
- **WHEN** 用户确认导出
- **THEN** 文件名 SHALL 由系统自动生成为 `固废运单_yyyyMMdd_HHmmss.xlsx`，拼接到所选目录路径后构成完整输出路径

### Requirement: 导出执行流程
系统 SHALL 在用户确认过滤条件且保存路径非空后执行导出操作。

#### Scenario: 确认导出
- **WHEN** 用户点击"导出"且保存路径非空
- **THEN** 系统 SHALL 调用 `ISolidWasteExcelExportService.ExportAsync` 传入过滤条件和完整输出路径执行导出

#### Scenario: 导出成功提示
- **WHEN** `ExportAsync` 返回 `Success = true`
- **THEN** 系统 SHALL 显示成功通知，包含导出行数信息

#### Scenario: 导出失败提示
- **WHEN** `ExportAsync` 返回 `Success = false`
- **THEN** 系统 SHALL 显示失败通知提醒用户

### Requirement: 保存路径记忆
系统 SHALL 在导出成功后将保存路径持久化到 `SystemSettings`，下次打开对话框自动填充。

#### Scenario: 导出成功后记忆路径
- **WHEN** `ExportAsync` 返回 `Success = true`
- **THEN** 系统 SHALL 将当前导出目录路径写入 `SystemSettings.ExportDefaultPath` 并调用 `SaveSettingsAsync` 持久化

#### Scenario: 导出失败不记忆路径
- **WHEN** `ExportAsync` 返回 `Success = false`
- **THEN** 系统 SHALL 不更新 `SystemSettings.ExportDefaultPath`

### Requirement: 上传字段映射
导出的「上传结果」「上传状态」「上传时间」三列 SHALL 从 `Waybill` 实体的同步状态字段映射，不再为空。

#### Scenario: 已同步运单的上传字段
- **WHEN** `Waybill.IsPendingSync == false`
- **THEN** 上传结果 SHALL 为 `"1"`
- **AND** 上传状态 SHALL 为 `"上传成功"`
- **AND** 上传时间 SHALL 为 `Waybill.LastSyncTime` 格式化为 `yyyy-MM-dd HH:mm:ss`

#### Scenario: 未同步运单的上传字段
- **WHEN** `Waybill.IsPendingSync == true`
- **THEN** 上传结果 SHALL 为 `"0"`
- **AND** 上传状态 SHALL 为 `"未上传"`
- **AND** 上传时间 SHALL 为空字符串

### Requirement: 接口与实现合并
`ISolidWasteExcelExportService` 接口 SHALL 与 `SolidWasteExcelExportService` 类合并到同一个文件中，删除独立的接口文件。接口定义保留不变。

### Requirement: 过滤参数可空与默认值
对话框中展示的过滤条件字段（日期范围、车牌号）SHALL 允许为空。GoodsName 和 ProviderName 不在对话框中展示，始终以 null 传入导出服务。

#### Scenario: 不填任何过滤条件直接导出
- **WHEN** 用户打开对话框后不填写任何字段，直接点击"导出"
- **THEN** 系统 SHALL 将所有过滤参数设为 null（包括始终为 null 的 GoodsName 和 ProviderName），导出全部满足固废模式条件的运单数据
