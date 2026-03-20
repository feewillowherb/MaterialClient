# 有人值守称重 - 变更增量

本文档定义对现有 `attended-weighing` 规范的增量变更。

## ADDED Requirements

### Requirement: 系统必须根据称重模式选择对应的 ViewModel

系统必须根据 `WeighingListItemDto.WeighingMode` 属性动态选择使用 `StandardModeDetailViewModel` 或 `SolidWasteModeDetailViewModel`。

#### Scenario: 标准模式 ViewModel 选择
- **WHEN** 用户选择一条 `WeighingMode.Standard` 的称重记录
- **THEN** 系统必须创建 `StandardModeDetailViewModel` 实例
- **AND** 将其设置为 `AttendedWeighingDetailView` 的 DataContext

#### Scenario: 固废模式 ViewModel 选择
- **WHEN** 用户选择一条 `WeighingMode.SolidWaste` 的称重记录
- **THEN** 系统必须创建 `SolidWasteModeDetailViewModel` 实例
- **AND** 将其设置为 `AttendedWeighingDetailView` 的 DataContext

#### Scenario: 默认使用标准模式
- **WHEN** `WeighingListItemDto.WeighingMode` 为空或未知值
- **THEN** 系统必须默认创建 `StandardModeDetailViewModel` 实例

### Requirement: 子视图必须绑定到正确的 ViewModel 类型

系统必须确保 `StandardModeFormView` 和 `SolidWasteModeFormView` 分别绑定到对应的 ViewModel 类型。

#### Scenario: 标准模式表单绑定
- **WHEN** `StandardModeFormView` 显示时
- **THEN** 系统必须将 `x:DataType` 设置为 `StandardModeDetailViewModel`
- **AND** 绑定路径必须与 `StandardModeDetailViewModel` 的属性匹配

#### Scenario: 固废模式表单绑定
- **WHEN** `SolidWasteModeFormView` 显示时
- **THEN** 系统必须将 `x:DataType` 设置为 `SolidWasteModeDetailViewModel`
- **AND** 绑定路径必须与 `SolidWasteModeDetailViewModel` 的属性匹配

### Requirement: 事件必须在基类中定义

系统必须确保所有操作事件在 `AttendedWeighingDetailViewModelBase` 基类中定义，以便父 ViewModel 可以统一订阅。

#### Scenario: 父 ViewModel 订阅事件
- **WHEN** `AttendedWeighingViewModel` 订阅 `SaveCompleted` 事件
- **THEN** 无论是 `StandardModeDetailViewModel` 还是 `SolidWasteModeDetailViewModel`
- **AND** 事件必须能够正确触发并传递 `ItemOperationCompletedEventArgs`

#### Scenario: 事件参数包含完整上下文
- **WHEN** 任何派生 ViewModel 触发操作事件
- **THEN** 事件参数必须包含：
  - ItemId：结果条目的 ID
  - ItemType：条目类型
  - OrderType：订单类型
  - IsCompleted：完成状态
  - OperationType：操作类型

## MODIFIED Requirements

### Requirement: 按条目状态选择视图

> 原需求位置：openspec/specs/attended-weighing/spec.md
> 原文：系统应根据条目的类型与完成状态自动选择合适视图（MainView 或 DetailView）。

系统应根据条目的类型、完成状态**以及称重模式**自动选择合适视图和 ViewModel。

#### Scenario: 可编辑条目在 DetailView 中显示
- **WHEN** 导航到的条目不是已完成的运单
- **示例**：未匹配的 WeighingRecord、OrderType = FirstWeight 的运单
- **THEN** 系统必须显示 AttendedWeighingDetailView（可编辑表单视图）
- **AND** 根据 `WeighingMode` 创建对应的派生 ViewModel

#### Scenario: 标准模式使用 StandardModeFormView
- **WHEN** 显示 `WeighingMode.Standard` 的可编辑条目
- **THEN** 系统必须在 DetailView 中显示 `StandardModeFormView`
- **AND** 使用 `StandardModeDetailViewModel` 作为绑定上下文

#### Scenario: 固废模式使用 SolidWasteModeFormView
- **WHEN** 显示 `WeighingMode.SolidWaste` 的可编辑条目
- **THEN** 系统必须在 DetailView 中显示 `SolidWasteModeFormView`
- **AND** 使用 `SolidWasteModeDetailViewModel` 作为绑定上下文
