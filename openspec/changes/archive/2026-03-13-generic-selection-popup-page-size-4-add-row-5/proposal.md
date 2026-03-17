## Why

当前通用选择弹窗（SearchableSelectionBox + GenericSelectionPopup）一次最多显示 5 条数据，且「新增」按钮在有结果时位于列表下方的独立一行，列表行数与分页大小未对齐。将分页大小改为 4、并把第 5 行固定留给「新增」按钮，可使列表区域始终为「4 行数据 + 1 行新增按钮」的固定布局，视觉一致且不随结果数量变化。

## What Changes

- 将通用选择弹窗的**分页大小**从当前默认（10）改为 **4**，即每页最多显示 4 条数据。
- 将列表区域的**布局**调整为：上方固定 4 行用于 DataGrid 数据，**第 5 行固定留给「新增」按钮**（当允许新增且应显示该按钮时）；无结果时仍保持「未找到匹配结果」+ 新增按钮的居中空状态。
- 涉及组件：`GenericSelectionPopup.axaml`（布局）、`GenericSelectionPopupViewModel` 或创建该 ViewModel 的调用处（分页大小 4）、以及可选地 `SearchableSelectionBox.axaml`（若仅展示相关则可不改）。

## Capabilities

### New Capabilities

- 无（本次仅修改现有能力）。

### Modified Capabilities

- **generic-selection-popup**：分页大小改为 4；列表区域固定为 4 行数据 + 第 5 行为「新增」按钮（当可新增时），有结果时新增按钮不再单独占列表外的一行，而是与列表同块内的第 5 行。

## Impact

- **Views**：`MaterialClient/Views/Controls/GenericSelectionPopup.axaml` — 列表区域行高与布局（4 行 DataGrid + 1 行新增按钮）。
- **ViewModels**：`GenericSelectionPopupViewModel` 的默认或实际使用的 `PageSize` 需为 4；若由调用方传入，则需在创建弹窗 VM 处传入 `pageSize: 4`（如 `AttendedWeighingDetailViewModel` 等）。
- **行为**：每页显示条数由当前默认 10 变为 4；有结果时「新增」按钮从「列表下方独立一行」改为「列表块内第 5 行」，与 4 条数据行形成固定 5 行布局。
