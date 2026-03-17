## Why

当前固废模式下，`AttendedWeighingMainView` 只提供“打印”操作，业务人员在第一次称重完成后，如果发现需要调整运单 `OrderType` 或首次称重 `FirstWeight`，只能通过外部系统或后台数据修改，流程割裂且容易出错。需要在称重界面直接提供“修改”入口，以便快速修正业务关键字段。

## What Changes

- 在固废模式工具栏中，“打印”按钮左侧新增一个“修改”按钮，用于打开当前选中运单的修改操作。
- 通过 `IWeighingMatchingService` 新增/扩展接口，实现对当前匹配运单调用领域方法 `OrderType` 来修改 `FirstWeight` 等相关信息。
- 更新相关 ViewModel 与命令绑定，使“修改”按钮仅在存在可编辑的固废运单且具有权限时可见/可用。

## Capabilities

### New Capabilities
- `attended-weighing-solid-waste-edit`: 在有人值守固废称重界面中，支持基于当前选中运单，直接调用称重匹配服务进行运单类型及首次称重数据的修改。

### Modified Capabilities
- `attended-weighing-solid-waste-print`: 在原有固废称重打印能力基础上，补充说明与“修改”操作的协同关系（例如修改后允许重新打印或限制打印时机）。

## Impact

- 影响界面：`AttendedWeighingMainView.axaml` 及其对应 ViewModel，增加“修改”按钮及命令绑定。
- 影响应用服务：`IWeighingMatchingService` 及其实现类，增加基于当前运单调用领域方法 `OrderType` 修改 `FirstWeight` 的能力。
- 可能影响领域模型：运单领域对象中与 `OrderType`、`FirstWeight` 相关的方法与不变式，需要确保修改流程符合领域规则。
- 可能影响下游流程：修改后对打印、过磅记录、数据管理对话框等流程的联动需要在后续规格与设计中进一步梳理。
