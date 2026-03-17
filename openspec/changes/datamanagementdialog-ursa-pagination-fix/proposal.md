## Why

Data management (solid waste ledger) dialog currently integrates Ursa `Pagination` but the paging interaction is broken: clicking page buttons does not reload data. The view uses the same Ursa component pattern as working popups, but the ViewModel wires `PageChangeCommand` with a mismatched command signature so Ursa cannot trigger server-side paging correctly.

## What Changes

- Align `DataManagementDialogViewModel` paging behavior with the shared Ursa paging pattern used by existing selection popups.
- Update the paging command to be parameterless and driven by `CurrentPage` (TwoWay bound from Ursa), instead of expecting an `int` page parameter.
- Ensure paging always reloads data based on the current filter and `CurrentPage`, updating `Records`, `TotalCount`, and `TotalPages` consistently.
- Keep the XAML `u:Pagination` binding surface unchanged so the fix is localized to the ViewModel and service usage.

## Capabilities

### New Capabilities
- `datamanagement-ursa-pagination`: Spec for how the data management dialog integrates Ursa pagination with server-side paging, including bindings, ViewModel behavior, and expected UX when navigating between pages.

### Modified Capabilities
- `solidwaste-datamanagement-dialog`: Clarify that the dialog must support working Ursa-based paging over the solid waste export rows, and that paging behavior must match the selection popups’ UX (page buttons always reload data and reflect total page count).

## Impact

- **ViewModels**: `DataManagementDialogViewModel` paging command wiring and behavior.
- **Views**: `DataManagementDialogWindow` uses existing `u:Pagination` bindings; behavior changes come from ViewModel alignment, not XAML surface changes.
- **Services**: Continued use of `ISolidWasteService.GetPagedExportRowsAsync` for server-side paging; no API shape change, but stricter expectations on `TotalCount` correctness.
- **UX**: Users can reliably navigate between pages in the data management dialog, with Ursa pagination behaving the same way as in selection popups.

