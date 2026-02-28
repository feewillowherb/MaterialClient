---
name: Creatable selection refactor
overview: "Refactor GenericSelectionPopup \"add new\" flow and parent Provider sync to follow the design in design-creatable-selection-react-select.md: insert new option into PagedItems first, then set SelectedItem; parent syncs selection immediately and inserts into its list when needed."
todos: []
isProject: false
---

# Creatable selection refactor (design doc implementation)

## Current state

- **[GenericSelectionPopupViewModel.cs](MaterialClient/ViewModels/GenericSelectionPopupViewModel.cs)** — `AddNewItemAsync()` sets `SelectedItem = new wrapper` then `await RefreshAsync()`. That violates the design: RefreshAsync replaces PagedItems, so the wrapper is not in the list and the DataGrid TwoWay binding clears SelectedItem.
- **[AttendedWeighingDetailViewModel.cs](MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs)** — Providers subscription closes the popup then runs `LoadProvidersAsync()` and `SelectedProvider = Providers.FirstOrDefault(p => p.Id == selectedId)`. The new provider may not be in `Providers` yet, so the form can show no selection.

Materials and Streets already sync selection immediately (`SelectedSolidWasteMaterial = item.Value`, `SelectedStreet = item.Value`); only Providers needs the parent-side refactor.

## Target flow (from design doc)

```mermaid
sequenceDiagram
    participant User
    participant AddNewItemAsync
    participant UIThread
    participant Parent

    User->>AddNewItemAsync: Click 新增
    AddNewItemAsync->>AddNewItemAsync: Create entity via API
    AddNewItemAsync->>UIThread: Insert wrapper into PagedItems, update counts
    UIThread->>UIThread: Post(SelectedItem = wrapper)
    Note over UIThread: SelectedItem is in PagedItems
    AddNewItemAsync->>Parent: SelectedItem changed
    Parent->>Parent: SelectedProvider = item.Value, insert into Providers if missing
    Parent->>Parent: Close popup; optional background LoadProvidersAsync
```



## Implementation

### 1. GenericSelectionPopupViewModel.AddNewItemAsync

**File:** [MaterialClient/ViewModels/GenericSelectionPopupViewModel.cs](MaterialClient/ViewModels/GenericSelectionPopupViewModel.cs)

- Replace the current body (create newItem, set SelectedItem, call RefreshAsync) with the Creatable flow:
  1. Guard: `_createNewItemFunc == null`, `string.IsNullOrWhiteSpace(SearchText?.Trim())` → return.
  2. `var newItem = await _createNewItemFunc(name)`; if null, return.
  3. Build `wrapper = new GenericSelectionItem<T> { Value = newItem, DisplayText = _displayTextSelector(newItem) ?? string.Empty }`.
  4. `await Dispatcher.UIThread.InvokeAsync(() => { ... })`:
    - `PagedItems.Insert(0, wrapper)`.
    - Update: `TotalCount += 1`, `TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1`.
    - Raise property changes: `TotalCountInfo`, `CurrentPageInfo`, `ShowResults`, `ShowAddNewButton`.
    - `Dispatcher.UIThread.Post(() => SelectedItem = wrapper)` so the DataGrid applies the new row before selection.
  5. Do **not** call RefreshAsync() after.
- Add a short comment at the top of the method referencing the design: e.g. "Creatable pattern: insert into list first, then set selection (see docs/design-creatable-selection-react-select.md)."
- Keep the same try/catch and logging.

### 2. AttendedWeighingDetailViewModel — Providers selection sync

**File:** [MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs](MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs)

- In the `ProvidersPopupViewModel.WhenAnyValue(x => x.SelectedItem)` subscription (around 412–435):
  1. After null check, set `selectedId = item.Value.Id` and `selectedProvider = item.Value`.
  2. **Immediate sync:** `SelectedProvider = selectedProvider`. If the new item is not in the list: `if (!Providers.Any(p => p.Id == selectedId)) Providers.Insert(0, selectedProvider)`.
  3. Close popup: `IsProvidersPopupOpen = false`.
  4. **Background refresh:** Keep `Dispatcher.UIThread.Post(async () => { await LoadProvidersAsync(); SelectedProvider = Providers.FirstOrDefault(p => p.Id == selectedId); })` so the Providers list stays in sync for the next open; selection is already set so the form shows the value immediately.

No changes to Materials or Streets subscriptions (Materials already syncs directly; Streets has no add-new).

### 3. Verification

- Build the project.
- No new public API; only behaviour and comments. No doc file edits unless you want to add a one-line “Implemented in …” in the design doc (optional).

## Files to change


| File                                                                                                                         | Change                                                                                                                                                                          |
| ---------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [MaterialClient/ViewModels/GenericSelectionPopupViewModel.cs](MaterialClient/ViewModels/GenericSelectionPopupViewModel.cs)   | Refactor AddNewItemAsync to insert-then-select, add design doc comment.                                                                                                         |
| [MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs](MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs) | In Providers SelectedItem subscription: immediate SelectedProvider + insert into Providers if missing; then close popup and run existing Post(LoadProvidersAsync + sync by Id). |


