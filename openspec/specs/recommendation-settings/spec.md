# 推荐设置

## 目的

提供推荐功能的用户可配置开关，控制系统在数据库查询和缓存数据源之间的选择。

## 需求

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

明细 ViewModel（`StandardWeighingDetailViewModel` 和 `SolidWasteWeighingDetailViewModel`）须从 `ISettingsService` 读取 `EnableLatestRecommendation` 并选择相应的推荐数据源。

#### 场景：标准称重默认行为（设置未启用）
- **当** `EnableLatestRecommendation` 为 `false`
- **且** `StandardWeighingDetailViewModel.LoadModeSpecificDataAsync()` 执行
- **且** 当前车牌号非空
- **则** ViewModel 须调用 `IRecommendationService.GetRecommendationByPlateNumberAsync(plateNumber)` 查询数据库

#### 场景：标准称重默认行为车牌为空
- **当** `EnableLatestRecommendation` 为 `false`
- **且** `StandardWeighingDetailViewModel.LoadModeSpecificDataAsync()` 执行
- **且** 当前车牌号为 null 或空白
- **则** ViewModel 须跳过推荐数据获取

#### 场景：标准称重启用最新推荐
- **当** `EnableLatestRecommendation` 为 `true`
- **且** `StandardWeighingDetailViewModel.LoadModeSpecificDataAsync()` 执行
- **则** ViewModel 须调用 `IRecommendationService.GetLatestRecommendationAsync()` 从全局缓存读取（不要求车牌号非空）

#### 场景：固废称重默认行为（设置未启用）
- **当** `EnableLatestRecommendation` 为 `false`
- **且** `SolidWasteWeighingDetailViewModel.LoadModeSpecificDataAsync()` 执行
- **且** 当前车牌号非空
- **则** ViewModel 须调用 `IRecommendationService.GetRecommendationByPlateNumberAsync(plateNumber)` 查询数据库

#### 场景：固废称重默认行为车牌为空
- **当** `EnableLatestRecommendation` 为 `false`
- **且** `SolidWasteWeighingDetailViewModel.LoadModeSpecificDataAsync()` 执行
- **且** 当前车牌号为 null 或空白
- **则** ViewModel 须跳过推荐数据获取

#### 场景：固废称重启用最新推荐
- **当** `EnableLatestRecommendation` 为 `true`
- **且** `SolidWasteWeighingDetailViewModel.LoadModeSpecificDataAsync()` 执行
- **则** ViewModel 须调用 `IRecommendationService.GetLatestRecommendationAsync()` 从全局缓存读取（不要求车牌号非空）

#### 场景：标准称重构造注入
- **当** 构造 `StandardWeighingDetailViewModel`
- **则** 须通过构造注入接收 `IRecommendationService` 和 `ISettingsService`

#### 场景：固废称重构造注入
- **当** 构造 `SolidWasteWeighingDetailViewModel`
- **则** 须通过构造注入接收 `IRecommendationService` 和 `ISettingsService`

### 需求：固废称重推荐数据应用

`SolidWasteWeighingDetailViewModel` 须将推荐数据应用到正确的响应式属性，确保 UI 正确更新。

#### 场景：应用供应商推荐
- **当** 推荐数据包含 `ProviderId`
- **且** 当前 `SelectedProviderId` 为 null
- **则** ViewModel 须设置 `SelectedProviderId` 为推荐值
- **且** ViewModel 须设置 `SelectedProviderItem` 为对应的 `SelectionItem`
- **且** ViewModel 须更新 `_listItem.ProviderId`

#### 场景：应用材料推荐
- **当** 推荐数据包含 `MaterialId`
- **且** 当前 `SelectedSolidWasteMaterial` 为 null
- **则** ViewModel 须设置 `SelectedSolidWasteMaterial` 为对应的 `Material` 实体
- **且** ViewModel 须设置 `SelectedMaterialItem` 为对应的 `SelectionItem`
- **且** ViewModel 须触发材料单位的自动选择（通过现有响应式链）

#### 场景：推荐失败不阻塞加载
- **当** 推荐服务抛出异常
- **则** ViewModel 须记录错误日志
- **且** ViewModel 须继续完成数据加载流程
- **且** 用户须能手动输入数据

#### 场景：推荐不覆盖已有数据
- **当** ExtraProperties 已加载供应商或材料数据
- **且** 推荐服务返回推荐数据
- **则** ViewModel 须保留 ExtraProperties 中的数据
- **且** ViewModel 须不应用推荐数据到已有值的字段
