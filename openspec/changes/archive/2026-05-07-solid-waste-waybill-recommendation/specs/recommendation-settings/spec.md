# 推荐设置 (Delta)

## MODIFIED Requirements

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

## ADDED Requirements

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
