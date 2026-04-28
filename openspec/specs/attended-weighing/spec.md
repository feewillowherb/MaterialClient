# 有人值守称重 规范

## 目的
待定 - 由变更 refactor-weighing-item-navigation 归档后创建。归档后更新目的。

## 需求

### 需求：操作后的条目导航

系统应在执行操作（保存、完成、匹配、作废）后，提供一致的、指向 WeighingListItemDto 的导航。

#### 场景：完成操作后的导航
- **WHEN** 用户在 AttendedWeighingDetailView 中将运单完成（首磅 → 已完成）
- **THEN** 系统应：
  - 刷新列表数据以反映已完成状态
  - 按以下优先级选择下一个条目：
    1. 优先选择下一个未完成的 Waybill（ItemType=Waybill, OrderType≠Completed）
    2. 若无未完成 Waybill，选择下一个未完成的 WeighingRecord（ItemType=WeighingRecord, OrderType≠Completed）
    3. 仅当所有条目均已完成时，选择最新已完成项
  - 若选中的是未完成项，在 AttendedWeighingDetailView 中打开该条目供继续操作
  - 若选中的是已完成项（fallback），在 AttendedWeighingMainView 中显示
  - 若目标条目不在当前页，则导航到正确页码
  - 遵循标签页切换规则（尊重 IsShowAllRecords 标志）

#### 场景：完成操作后存在未完成 Waybill
- **WHEN** 用户完成一个运单
- **AND** 列表中存在其他 OrderType≠Completed 的 Waybill
- **THEN** 系统应选择第一个未完成的 Waybill
- **AND** 在 AttendedWeighingDetailView 中打开该 Waybill

#### 场景：完成操作后无未完成 Waybill 但存在未完成 WeighingRecord
- **WHEN** 用户完成一个运单
- **AND** 列表中不存在未完成的 Waybill
- **AND** 列表中存在 OrderType≠Completed 的 WeighingRecord
- **THEN** 系统应选择第一个未完成的 WeighingRecord
- **AND** 在 AttendedWeighingDetailView 中打开该 WeighingRecord

#### 场景：完成操作后所有条目均已完成
- **WHEN** 用户完成一个运单
- **AND** 列表中不存在任何未完成的 Waybill 或 WeighingRecord
- **THEN** 系统应选择最新已完成项
- **AND** 在 AttendedWeighingMainView 中显示

#### 场景：保存操作后的导航
- **当** 用户在 AttendedWeighingDetailView 中保存称重记录或运单的修改
- **则** 系统应：
  - 刷新列表数据以反映保存后的变更
  - 在列表中保持已保存条目为选中状态
  - 保持在 AttendedWeighingDetailView（允许继续编辑）
  - 保持当前标签页（条目状态未变）
  - 若因排序导致条目移动，则导航到正确页码

#### 场景：匹配操作后的导航
- **当** 用户手动将一条称重记录与另一条记录匹配
- **则** 系统应：
  - 刷新列表数据以显示新生成的运单
  - 在列表中选中下一个未匹配条目
  - 在 AttendedWeighingDetailView 中显示下一个未匹配条目
  - 若当前在“已完成”标签且未显示全部记录，则切换到“未匹配”标签
  - 导航到下一个条目所在页

#### 场景：作废操作后的导航
- **当** 用户作废（删除）一条称重记录
- **则** 系统应：
  - 刷新列表数据以移除已作废记录
  - 在列表中选中下一个未匹配条目
  - 在 AttendedWeighingDetailView 中显示下一个未匹配条目
  - 保持当前标签页（条目被移除，未移动）
  - 导航到下一个条目所在页

### 需求：标签页切换规则

系统应实现尊重用户上下文的智能标签页切换，仅在必要时切换。

#### 场景：标签切换尊重“全部记录”模式
- **当** IsShowAllRecords 为 true（用户选择了“全部记录”标签）
- **则** 系统在任何操作后均不得自动切换标签
- **因为** 该标签下所有条目均可见，与完成状态无关

#### 场景：条目变为已完成时的标签切换
- **当** 某条目变为已完成（OrderType 变为 Completed）
- **且** IsShowUnmatched 为 true（用户在“未匹配”标签）
- **且** IsShowAllRecords 为 false
- **则** 系统应切换到 IsShowCompleted = true（“已完成”标签）

#### 场景：条目变为未匹配时的标签切换
- **当** 某条目变为未匹配（OrderType 变为 FirstWeight 或 Unmatch）
- **且** IsShowCompleted 为 true（用户在“已完成”标签）
- **且** IsShowAllRecords 为 false
- **则** 系统应切换到 IsShowUnmatched = true（“未匹配”标签）

#### 场景：当前标签已包含目标条目时不切换标签
- **当** 操作完成且结果条目的状态与当前标签筛选一致
- **则** 系统不得切换标签
- **示例**：用户在“已完成”标签下保存已完成的运单 → 保持在“已完成”标签

### 需求：跨页条目导航

系统应能查找并导航到分页边界之外的条目。

#### 场景：目标条目在当前页
- **当** 操作后导航到目标条目
- **且** 目标条目在当前页
- **则** 系统应：
  - 不翻页即选中该条目
  - 在 O(1) 时间内完成导航

#### 场景：目标条目在其他页
- **当** 操作后导航到目标条目
- **且** 目标条目不在当前页
- **则** 系统应：
  - 从第 1 页起跨页搜索
  - 导航到包含目标条目的页
  - 找到后选中该条目
  - 最多搜索 10 页，避免过度加载

#### 场景：搜索后未找到条目
- **当** 操作后导航到目标条目
- **且** 在可用页中搜索后仍无法找到目标条目
- **则** 系统应：
  - 回退为选中当前列表的第一条
  - 记录关于缺失条目的警告日志
  - 不向用户显示错误（优雅降级）

### 需求：按条目状态选择视图

系统应根据条目的类型与完成状态自动选择合适视图（MainView 或 DetailView）。

#### 场景：已完成运单在 MainView 中显示
- **当** 导航到的条目为运单（Waybill）
- **且** 运单的 OrderType 为 Completed
- **则** 系统应显示 AttendedWeighingMainView（只读摘要视图）

#### 场景：可编辑条目在 DetailView 中显示
- **当** 导航到的条目不是已完成的运单
- **示例**：未匹配的 WeighingRecord、OrderType = FirstWeight 的运单
- **则** 系统应显示 AttendedWeighingDetailView（可编辑表单视图）

#### 场景：完成操作后的视图选择
- **当** 用户完成运单（将 OrderType 从 FirstWeight 改为 Completed）
- **则** 系统应从 AttendedWeighingDetailView 切换到 AttendedWeighingMainView
- **因为** 条目已变为只读，适合在 MainView 中查看

### 需求：操作事件上下文

系统应在操作事件中提供完整上下文信息，以支持正确导航。

#### 场景：事件参数包含操作上下文
- **当** 在 AttendedWeighingDetailView 中完成某操作（保存、完成、匹配、作废）
- **则** 触发的事件应包含：
  - ItemId：结果条目的 ID
  - ItemType：条目是 WeighingRecord 还是 Waybill
  - OrderType：当前订单类型（Unmatch、FirstWeight、Completed）
  - IsCompleted：用于快速判断完成状态的布尔标志
  - OperationType：标识所执行操作的字符串

#### 场景：完成操作事件
- **当** 用户成功完成运单
- **则** 应触发 CompleteCompleted 事件，且包含：
  - ItemId = 运单 ID
  - ItemType = Waybill
  - OrderType = Completed
  - IsCompleted = true
  - OperationType = "Complete"

#### 场景：保存操作事件
- **当** 用户保存对记录或运单的修改
- **则** 应触发 SaveCompleted 事件，且包含：
  - ItemId = 已保存条目的 ID
  - ItemType = 条目当前类型
  - OrderType = 条目当前订单类型
  - IsCompleted = 根据 OrderType 得出
  - OperationType = "Save"

### 需求：统一导航逻辑

系统应使用单一统一方法处理所有操作后导航，以保证一致性。

#### 场景：所有操作均使用 NavigateToItemAsync
- **当** 任一操作事件处理被触发（保存、完成、匹配、作废、手动匹配）
- **则** 处理程序应调用统一的 NavigateToItemAsync 方法
- **且** NavigateToItemAsync 应负责：
  - 数据刷新
  - 是否切换标签的决策
  - 页码导航
  - 条目选中
  - 视图选择

#### 场景：导航逻辑可预测且可测
- **当** 测试导航行为时
- **则** 所有导航路径均应经过 NavigateToItemAsync
- **从而** 实现单点测试与维护
- **并** 保证各操作行为一致

### 需求：车牌颜色优先级匹配

系统应实现基于优先级的车牌号选择机制：被筛选的车牌颜色视为最低优先级，而非直接拒绝。

#### 场景：高优先级车牌覆盖低优先级车牌
- **假设** 地磅上为黄牌（低优先级颜色）车辆
- **且** 该车牌已被识别 10 次（缓存中 count=10）
- **当** 同时检测到蓝牌（高优先级颜色）车辆一次
- **则** 系统应选择蓝牌为最常出现车牌号
- **且** 黄牌保留在缓存中但不被选中

#### 场景：无高优先级时使用低优先级车牌
- **假设** 地磅上为黄牌（低优先级颜色）车辆
- **且** 未检测到其他车牌
- **当** 系统选择最常出现车牌号
- **则** 系统应返回该黄牌
- **且** 记录说明正在使用低优先级车牌的日志

#### 场景：低优先级车牌不能覆盖已有高优先级车牌
- **假设** 蓝牌（高优先级颜色）车辆已缓存且 count=1
- **当** 黄牌（低优先级颜色）被识别 100 次
- **则** 系统应继续返回蓝牌
- **且** 黄牌在缓存中累加 count 但不被选中

#### 场景：无颜色信息的车牌视为高优先级
- **假设** 识别的车牌无颜色信息（colorType 为 null）
- **当** 该车牌被缓存
- **则** 系统应默认将其视为高优先级
- **且** 其应能覆盖低优先级车牌

### 需求：车牌号缓存颜色跟踪

系统应在缓存中随车牌号存储颜色信息，以支持按优先级选择。

#### 场景：缓存中持久化颜色信息
- **假设** 某车牌被识别且颜色类型为 YELLOW
- **当** 该车牌加入缓存
- **则** 缓存记录应包含：
  - Count：识别次数
  - LastUpdateTime：最近一次识别时间戳
  - ColorType：YELLOW（检测到的颜色）

#### 场景：增加 count 时保留颜色信息
- **假设** 车牌“京A12345”颜色为 BLUE，已缓存且 count=1
- **当** 同一车牌再次以 BLUE 被识别
- **则** 缓存记录应更新为：
  - Count：2（递增）
  - LastUpdateTime：（更新为当前时间）
  - ColorType：BLUE（与首次识别一致）

#### 场景：缓存处理缺失的颜色信息
- **假设** 某车牌被识别但无颜色信息（colorType 为 null）
- **当** 该车牌加入缓存
- **则** 缓存记录应将 ColorType 存为 null
- **且** 该车牌应被视为高优先级

### 需求：车牌颜色优先级配置

系统应支持将特定车牌颜色配置为低优先级，将其视为备选而非完全拒绝。

#### 场景：低优先级颜色带标志存储
- **假设** 配置在 LowPriorityPlateColors 数组中指定了 YELLOW
- **当** 通过 OnPlateNumberRecognized 识别到黄牌
- **则** 系统不得拒绝该车牌
- **且** 应将其以 ColorType=YELLOW 存入缓存
- **且** 应在选择逻辑中将其标记为低优先级
- **且** 记录检测到低优先级颜色的日志

#### 场景：普通颜色按高优先级存储
- **假设** 配置在 LowPriorityPlateColors 数组中指定了 YELLOW
- **当** 通过 OnPlateNumberRecognized 识别到蓝牌
- **则** 系统应将其以 ColorType=BLUE 存入缓存
- **且** 在选择逻辑中标记为高优先级
- **且** 不记录与优先级相关的日志

#### 场景：配置加载使用新键名
- **假设** appsettings.json 包含 LowPriorityPlateColors 数组
- **当** AttendedWeighingService 启动
- **则** 系统应从配置键 LowPriorityPlateColors 加载颜色
- **且** 存入 _lowPriorityPlateColors HashSet
- **且** 用于区分低优先级与高优先级车牌
- **且** 在初始化时记录低优先级颜色日志

### 需求：配置键重命名

系统应使用 LowPriorityPlateColors 作为配置键名，以体现基于优先级的语义，而非基于拒绝的筛选。

#### 场景：使用新配置键加载
- **假设** 配置文件包含 LowPriorityPlateColors 键
- **当** AttendedWeighingService 初始化
- **则** 系统应从 LowPriorityPlateColors 键读取车牌颜色
- **且** 不得从旧键 FilteredPlateColors 读取
- **且** 在初始化时记录“低优先级车牌颜色：[列表]”

#### 场景：变量命名体现优先级语义
- **假设** 服务代码使用内部变量表示车牌颜色优先级
- **则** 变量应命名为 _lowPriorityPlateColors（而非 _filteredPlateColors）
- **且** 所有日志应使用“低优先级”术语
- **且** 代码注释应引用基于优先级的行为

### 需求：系统必须将文件路径以相对路径存储以实现数据库可移植性

系统应在数据库中使用相对路径（相对于应用程序根目录）存储文件路径，以便在不同服务器或目录间迁移数据库时不破坏文件引用。

**上下文**：当数据库文件迁移到新服务器或目录（例如从 `D:\MaterialClient\` 到 `E:\Apps\MaterialClient\`）时，绝对路径如 `D:\MaterialClient\Photos\car.jpg` 会失效。相对路径如 `Photos/car.jpg` 在迁移后仍然有效。

**实现约束**：适用于数据库中存储的所有 `AttachmentFile.LocalPath` 值。

#### 场景：照片路径以相对路径存储
**假设** 应用程序运行目录为 `D:\MaterialClient\`  
**当** 拍摄并保存照片到 `D:\MaterialClient\Photos\2026\01\23\bill.jpg`  
**则** 数据库中 `AttachmentFile.LocalPath` 必须存储 `"Photos/2026/01/23/bill.jpg"`（相对路径）  
**且** 不得存储 `"D:\MaterialClient\Photos\2026\01\23\bill.jpg"`（绝对路径）

#### 场景：数据库迁移到新位置
**假设** 数据库中存在 `AttachmentFile`，且 `LocalPath = "Photos/2026/01/23/car.jpg"`  
**且** 数据库文件从 `D:\MaterialClient\` 复制到 `E:\NewLocation\`  
**且** `Photos` 文件夹也复制到 `E:\NewLocation\Photos\`  
**当** 应用程序从 `E:\NewLocation\` 运行  
**则** `E:\NewLocation\Photos\2026/01/23/car.jpg` 处的照片必须能成功加载  
**且** 无需在数据库中更新路径

#### 场景：已有绝对路径仍可用
**假设** 数据库中存在旧版 `AttachmentFile`，且 `LocalPath = "D:\MaterialClient\Photos\car.jpg"`（绝对路径）  
**当** 应用程序从 `D:\MaterialClient\` 运行  
**则** 该照片仍须能成功加载  
**且** 之后新拍摄的照片必须使用相对路径

---

### 需求：图片转换器必须在加载前规范化路径

图片转换器在检查文件存在及加载图片前，应将相对路径规范化为绝对路径，以确保无论应用程序启动时的工作目录如何，图片都能正确显示。

**上下文**：当应用程序从 `C:\Windows\System32` 启动（例如通过任务计划程序）时，未规范化的数据库相对路径会错误地解析为 `C:\Windows\System32\Photos\...`。

**实现约束**：适用于整个 UI 中使用的 `CarNullOrEmptyImageConverter` 与 `NullOrEmptyImageConverter`。

#### 场景：从 System32 启动时从相对路径加载图片
**假设** 应用程序从 `C:\Windows\System32\` 启动  
**且** 数据库中存在 `AttachmentFile`，且 `LocalPath = "Photos/2026/01/23/car.jpg"`  
**当** UI 使用 `CarNullOrEmptyImageConverter` 显示图片  
**则** 转换器必须将路径规范化为 `{AppContext.BaseDirectory}\Photos\2026\01\23\car.jpg`  
**且** 图片必须成功显示  
**且** 不得尝试从 `C:\Windows\System32\Photos\2026\01\23\car.jpg` 加载

#### 场景：从绝对路径加载图片（向后兼容）
**假设** 数据库中存在旧版 `AttachmentFile`，且 `LocalPath = "D:\MaterialClient\Photos\car.jpg"`（绝对路径）  
**当** UI 尝试显示该图片  
**则** 转换器必须识别路径已是绝对路径  
**且** 直接使用、不做修改  
**且** 图片必须成功显示

#### 场景：文件缺失时显示默认图
**假设** 数据库中存在 `AttachmentFile`，且 `LocalPath = "Photos/missing.jpg"`  
**且** `{AppContext.BaseDirectory}\Photos\missing.jpg` 处不存在该文件  
**当** UI 尝试显示该图片  
**则** 转换器必须显示默认车辆图片占位符  
**且** 不得抛出异常

#### 场景：资源路径单独处理
**假设** ViewModel 提供资源路径 `"avares://MaterialClient/Assets/Car_Default.png"`  
**当** UI 使用 `CarNullOrEmptyImageConverter` 显示图片  
**则** 转换器必须识别为资源路径  
**且** 从嵌入资源加载  
**且** 不进行文件路径规范化

---

### 需求：系统必须提供统一的 PathManager 工具

系统应提供集中的 `PathManager` 工具，包含双向路径转换方法（`ToAbsolutePath`、`ToRelativePath`）及文件操作辅助方法，确保各服务中的路径处理一致。

**上下文**：路径转换逻辑曾分散在 `DatabaseConnectionStringFactory`、`AttachmentPathUtils` 及服务代码中。集中该逻辑可减少重复并保证一致性。

**实现约束**：`PathManager` 须为 `MaterialClient.Common/Utils/` 命名空间下的静态工具类，遵循项目中与配置无关的工具逻辑约定。

#### 场景：为文件操作将相对路径转为绝对路径
**假设** 应用程序根目录为 `D:\MaterialClient\`  
**当** 调用 `PathManager.ToAbsolutePath("Photos/2026/01/23/car.jpg")`  
**则** 必须返回 `"D:\MaterialClient\Photos\2026\01\23\car.jpg"`  
**且** 路径必须完全规范化（无 `..` 或多余斜杠）

#### 场景：为数据库存储将绝对路径转为相对路径
**假设** 应用程序根目录为 `D:\MaterialClient\`  
**当** 调用 `PathManager.ToRelativePath("D:\MaterialClient\Photos\2026\01\23\car.jpg")`  
**则** 必须返回 `"Photos\2026\01\23\car.jpg"`

#### 场景：幂等转换（已是绝对路径）
**假设** 绝对路径为 `"D:\MaterialClient\Photos\car.jpg"`  
**当** 调用 `PathManager.ToAbsolutePath("D:\MaterialClient\Photos\car.jpg")`  
**则** 必须原样返回输入：`"D:\MaterialClient\Photos\car.jpg"`

#### 场景：幂等转换（已是相对路径）
**假设** 相对路径为 `"Photos/car.jpg"`  
**当** 调用 `PathManager.ToRelativePath("Photos/car.jpg")`  
**则** 必须原样返回输入：`"Photos/car.jpg"`

#### 场景：应用程序目录外的路径保持为绝对路径
**假设** 绝对路径为 `"C:\Users\Admin\Desktop\export.pdf"`（在应用程序目录外）  
**当** 调用 `PathManager.ToRelativePath("C:\Users\Admin\Desktop\export.pdf")`  
**则** 必须原样返回输入（无法转为相对路径）  
**且** 返回 `"C:\Users\Admin\Desktop\export.pdf"`

#### 场景：带路径规范化的文件存在检查
**假设** 应用程序根目录为 `D:\MaterialClient\`  
**且** 文件存在于 `D:\MaterialClient\Photos\car.jpg`  
**当** 调用 `PathManager.FileExists("Photos/car.jpg")`  
**则** 必须在内部规范化为绝对路径  
**且** 返回 `true`

#### 场景：带路径规范化的目录创建
**假设** 应用程序根目录为 `D:\MaterialClient\`  
**当** 调用 `PathManager.EnsureDirectoryExists("Photos/2026/01/23")`  
**则** 必须在 `D:\MaterialClient\Photos\2026\01\23\` 创建目录  
**且** 返回绝对路径 `"D:\MaterialClient\Photos\2026\01\23"`  
**且** 创建所有缺失的父目录

### 需求：台账管理对话框按模式路由

系统 SHALL 根据当前 WeighingMode 决定"台账管理"按钮打开的对话框类型。

#### 场景：标准模式打开标准台账对话框
- **WHEN** 用户在 `AttendedWeighingWindow` 中点击"台账管理"按钮
- **AND WHEN** 当前 `WeighingMode` 为 Standard
- **THEN** 系统 SHALL 创建 `StandardDataManagementDialogViewModel` 并打开 `StandardDataManagementDialogWindow`

#### 场景：固废模式打开固废台账对话框
- **WHEN** 用户在 `AttendedWeighingWindow` 中点击"台账管理"按钮
- **AND WHEN** 当前 `WeighingMode` 为 SolidWaste
- **THEN** 系统 SHALL 创建 `DataManagementDialogViewModel` 并打开 `DataManagementDialogWindow`（保持现有行为不变）

### 需求：关闭车牌重写时称重记录创建使用锁定车牌
当 `WeighingConfiguration.EnablePlateRewrite = false` 时，系统在创建称重记录时 MUST 使用“当前推荐车牌”的锁定结果（基于 `finalPlateNumber` 的 `LockedAt` 规则）作为 `WeighingRecord.PlateNumber` 的初始值，从而保证同一称重周期内称重记录车牌稳定。

#### Scenario: 创建称重记录时使用锁定车牌
- **WHEN** 系统进入称重记录创建流程
- **AND WHEN** `EnablePlateRewrite = false`
- **AND WHEN** 当前存在可用的锁定车牌候选（`LockedAt != null`）
- **THEN** 系统 MUST 将 `LockedAt` 最早的车牌作为称重记录的 `PlateNumber`

#### Scenario: 无锁定候选时使用原有推荐规则
- **WHEN** 系统进入称重记录创建流程
- **AND WHEN** `EnablePlateRewrite = false`
- **AND WHEN** 当前不存在任何锁定车牌候选
- **THEN** 系统 MUST 退回到原有的推荐车牌选择逻辑来决定 `PlateNumber`

### Requirement: Newly created selection entities SHALL be locally queryable immediately
在人工称重流程中，用户新增 `Material` 或 `Provider` 成功后，系统 MUST 保证该记录已写入本地数据库，以便同一会话中的列表加载、搜索和再次打开选择框时可立即命中。

#### Scenario: Immediate re-query after creating provider
- **WHEN** 用户在人工称重界面新增 Provider 成功后立即触发 Provider 列表查询
- **THEN** 查询结果 MUST 包含该新建 Provider（无需等待后台轮询同步）

#### Scenario: Immediate re-query after creating material
- **WHEN** 用户在人工称重界面新增 Material 成功后立即触发 Material 列表查询
- **THEN** 查询结果 MUST 包含该新建 Material（无需等待后台轮询同步）

---

### Requirement: Attended weighing entity queries SHALL not use WeighingMode as enabled flag
在人工称重相关流程中，涉及 `Provider`、`Material`、`MaterialUnit` 的查询和选择 MUST NOT 将 `WeighingMode` 作为“启用”判定条件。`WeighingMode` 在该上下文仅视为遗留字段，不参与可用性过滤。

#### Scenario: Entity search in attended weighing returns all non-WeighingMode-filtered candidates
- **WHEN** 用户在人工称重流程中打开或搜索 Provider/Material/MaterialUnit 选项
- **THEN** 系统 MUST 基于既有业务条件返回结果，且 MUST NOT 添加 `WeighingMode` 过滤
