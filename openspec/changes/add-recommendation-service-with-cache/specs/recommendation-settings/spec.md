## 新增需求

### 需求：EnableLatestRecommendation 设置属性

`SystemSettings` 须包含 `bool EnableLatestRecommendation` 属性，默认值为 `false`。

#### 场景：默认值
- **当** 创建新的 `SystemSettings` 实例
- **则** `EnableLatestRecommendation` 须为 `false`

#### 场景：序列化
- **当** `SystemSettings` 序列化为 JSON
- **则** `EnableLatestRecommendation` 须作为布尔字段包含在内

#### 场景：缺失字段反序列化
- **当** 已保存的设置 JSON 中不包含 `EnableLatestRecommendation`
- **则** 反序列化须默认为 `false`

### 需求：设置页面推荐开关 UI

设置窗口须在系统设置区域显示 `EnableLatestRecommendation` 的切换开关。

#### 场景：开关显示
- **当** 打开设置窗口
- **则** 系统设置区域须显示标签为"启用最新推荐数据"的切换开关

#### 场景：开关反映已保存的状态
- **当** 设置窗口加载
- **则** 开关须反映数据库中 `EnableLatestRecommendation` 的值

#### 场景：开关默认状态
- **当** 之前未保存过设置
- **则** 开关须处于关闭状态（false）

### 需求：推荐开关的设置保存与加载

`SettingsWindowViewModel` 须通过现有的保存/加载流程持久化 `EnableLatestRecommendation`。

#### 场景：保存启用状态
- **当** 用户启用开关并点击保存
- **则** `SystemSettings.EnableLatestRecommendation` 须以 `true` 保存到数据库

#### 场景：保存禁用状态
- **当** 用户禁用开关并点击保存
- **则** `SystemSettings.EnableLatestRecommendation` 须以 `false` 保存到数据库

#### 场景：窗口打开时加载
- **当** 执行 `LoadSettingsAsync`
- **则** ViewModel 须从 `settings.SystemSettings.EnableLatestRecommendation` 设置其 `EnableLatestRecommendation` 响应式属性

### 需求：明细 ViewModel 中的数据源选择

`StandardWeighingDetailViewModel` 须从 `ISettingsService` 读取 `EnableLatestRecommendation` 并选择相应的推荐数据源。

#### 场景：默认行为（设置未启用）
- **当** `EnableLatestRecommendation` 为 `false`
- **则** ViewModel 须调用 `IRecommendationService.GetRecommendationByPlateNumberAsync(plateNumber)` 查询数据库

#### 场景：启用最新推荐
- **当** `EnableLatestRecommendation` 为 `true`
- **则** ViewModel 须调用 `IRecommendationService.GetLatestRecommendationAsync(plateNumber)` 从缓存读取

#### 场景：构造注入
- **当** 构造 `StandardWeighingDetailViewModel`
- **则** 须通过构造注入接收 `IRecommendationService` 和 `ISettingsService`（替代 `ServiceProvider.GetRequiredService` 模式）
