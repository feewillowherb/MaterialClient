# 称重模式 ViewModel 规范

## 目的

定义称重模式 ViewModel 的架构规范，包括基类接口、模式切换机制、数据绑定约定，确保标准模式和固废模式的 ViewModel 具有一致的结构和行为。

## 需求

### 需求：ViewModel 基类必须提供共享属性和方法

系统必须提供 `AttendedWeighingDetailViewModelBase` 抽象基类，包含所有称重模式共享的属性、命令和事件。

#### 场景：基类包含共享重量属性
- **当** 创建任何称重模式 ViewModel
- **则** 系统必须提供以下共享属性：
  - WeighingRecordId：称重记录 ID
  - AllWeight：总重量
  - TruckWeight：车重
  - GoodsWeight：净重（计算属性）

#### 场景：基类包含共享车辆属性
- **当** 创建任何称重模式 ViewModel
- **则** 系统必须提供以下共享属性：
  - PlateNumber：车牌号
  - PlateNumberError：车牌号验证错误信息

#### 场景：基类包含共享时间属性
- **当** 创建任何称重模式 ViewModel
- **则** 系统必须提供以下共享属性：
  - JoinTime：进场时间
  - OutTime：出场时间
  - Operator：操作员
  - Remark：备注

#### 场景：基类包含共享命令
- **当** 创建任何称重模式 ViewModel
- **则** 系统必须提供以下共享命令：
  - CloseCommand：关闭详情窗口
  - AbolishCommand：作废当前记录
  - MatchCommand：匹配记录

#### 场景：基类包含共享事件
- **当** 创建任何称重模式 ViewModel
- **则** 系统必须提供以下共享事件：
  - SaveCompleted：保存完成事件
  - CompleteCompleted：完成操作事件
  - CloseRequested：关闭请求事件
  - MatchCompleted：匹配完成事件
  - AbolishCompleted：作废完成事件

### 需求：派生类必须实现抽象方法

系统必须要求派生类实现 `SaveCoreAsync()` 和 `CompleteCoreAsync()` 抽象方法，以处理模式特定的保存和完成逻辑。

#### 场景：标准模式保存实现
- **当** 调用 StandardModeDetailViewModel.SaveCoreAsync()
- **则** 系统必须调用 `IWeighingMatchingService.UpdateListItemAsync()`
- **且** 使用标准模式的参数格式

#### 场景：固废模式保存实现
- **当** 调用 SolidWasteModeDetailViewModel.SaveCoreAsync()
- **则** 系统必须调用 `IWeighingMatchingService.UpdateSolidWasteModeAsync()`
- **且** 包含固废模式特有参数（联单号、镇街、类型）

#### 场景：标准模式完成实现
- **当** 调用 StandardModeDetailViewModel.CompleteCoreAsync()
- **则** 系统必须验证供应商、物料、物料单位、运单数量
- **且** 调用 `IWeighingMatchingService.CompleteOrderAsync()`

#### 场景：固废模式完成实现
- **当** 调用 SolidWasteModeDetailViewModel.CompleteCoreAsync()
- **则** 系统必须验证供应商、材料、镇街、类型、联单号
- **且** 调用 `IWeighingMatchingService.CompleteOrderAsync()`

### 需求：ViewModel 必须支持依赖注入

系统必须将所有 ViewModel 注册到 DI 容器，支持通过 `IServiceProvider` 获取实例。

#### 场景：DI 注册派生类
- **当** 应用程序启动
- **则** 系统必须注册以下类型：
  - StandardModeDetailViewModel : ITransientDependency
  - SolidWasteModeDetailViewModel : ITransientDependency

#### 场景：通过 DI 获取 ViewModel
- **当** 调用 `_serviceProvider.GetRequiredService<StandardModeDetailViewModel>()`
- **则** 系统必须返回一个新的 StandardModeDetailViewModel 实例
- **且** 所有依赖项必须正确注入

### 需求：ViewModel 必须支持 ReactiveUI 模式

系统必须使用 ReactiveUI 和 ReactiveUI.SourceGenerators 实现属性和命令。

#### 场景：使用 Reactive 属性
- **当** 定义 ViewModel 属性
- **则** 系统必须使用 `[Reactive]` 特性标记可变属性
- **且** 使用 `ReactiveUI.SourceGenerators` 自动生成代码

#### 场景：使用 ReactiveCommand
- **当** 定义 ViewModel 命令
- **则** 系统必须使用 `[ReactiveCommand]` 特性标记命令方法
- **且** 支持异步执行

### 需求：标准模式 ViewModel 必须提供物料管理功能

系统必须通过 StandardModeDetailViewModel 提供物料选择和管理功能。

#### 场景：标准模式物料属性
- **当** 使用 StandardModeDetailViewModel
- **则** 系统必须提供以下属性：
  - Providers：供应商列表
  - SelectedProvider：选中的供应商
  - Materials：物料列表
  - MaterialItems：物料行集合（DataGrid 绑定）
  - IsMaterialPopupOpen：物料选择弹窗状态
  - MaterialsSelectionPopupViewModel：物料选择弹窗 ViewModel

### 需求：固废模式 ViewModel 必须提供固废特有功能

系统必须通过 SolidWasteModeDetailViewModel 提供固废模式特有的功能。

#### 场景：固废模式特有属性
- **当** 使用 SolidWasteModeDetailViewModel
- **则** 系统必须提供以下属性：
  - SolidWasteOrderNumber：联单编号
  - Streets：镇街列表
  - SelectedStreet：选中的镇街
  - SolidWasteTypes：固废类型列表
  - SelectedSolidWasteType：选中的类型
  - SolidWasteMaterials：固废材料列表
  - SelectedSolidWasteMaterial：选中的材料

#### 场景：固废模式增强选择弹窗
- **当** 使用 SolidWasteModeDetailViewModel
- **则** 系统必须提供以下增强选择弹窗：
  - StreetsPopupViewModel：镇街选择弹窗
  - MaterialsPopupViewModel：材料选择弹窗
  - ProvidersPopupViewModel：供应商选择弹窗
  - IsStreetsPopupOpen、IsMaterialsPopupOpen、IsProvidersPopupOpen：弹窗状态
