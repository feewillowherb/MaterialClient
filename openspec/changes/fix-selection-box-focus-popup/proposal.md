## Why

`CreatablePageableSearchableSelectionBox` 控件存在焦点管理缺陷：页面加载时控件自动获取焦点，以及选择条目后焦点回到控件导致弹窗再次打开。这两个问题严重影响 SolidWasteModeFormView 表单的可用性。

## What Changes

- 修复 `CreatablePageableSearchableSelectionBox` 选择非重复条目后弹窗关闭又立即重新打开的问题：选择后主动清除控件焦点，防止焦点回落触发弹窗重新打开。
- 修复 SolidWasteModeFormView 页面加载时焦点自动落在 `ProvidersSelectionBox` 上的问题：调整控件的焦点策略，确保页面初始化时不会自动聚焦到该控件。

## Capabilities

### New Capabilities

（无新增能力）

### Modified Capabilities

- `attended-weighing`: 修复 SolidWaste 模式下 CreatablePageableSearchableSelectionBox 控件的焦点与弹窗行为

## Impact

- `MaterialClient.UI/Views/Controls/CreatablePageableSearchableSelectionBox.axaml.cs` — 焦点管理逻辑修改
- `MaterialClient.UI/Views/Controls/SolidWasteModeFormView.axaml.cs` 或 `.axaml` — 可能需要调整初始焦点行为
- 现有测试文件 `CreatablePageableSearchableSelectionBoxInteractionTests.cs` 可能需要更新
