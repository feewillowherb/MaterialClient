## Context

The solid waste data management dialog (`DataManagementDialogWindow` + `DataManagementDialogViewModel`) already uses Ursa `Pagination` in XAML, mirroring the pattern used in `GenericSelectionPopup` and `MaterialsSelectionPopup`. Those popups have working paging because their ViewModels expose:

- A `CurrentPage` property with TwoWay binding to `u:Pagination.CurrentPage`
- A parameterless `PageChangeCommand` (`ICommand`) that refreshes data based on the current `CurrentPage`

In contrast, `DataManagementDialogViewModel` wires `PageChangeCommand` as `ReactiveCommand<int>` expecting an explicit page argument. Ursa updates `CurrentPage` but does not provide the page as a command parameter, so the command is not executed as designed and server-side paging does not occur.

The dialog relies on `ISolidWasteService.GetPagedExportRowsAsync(filter, page, pageSize)` for server-side paging and maintains `Records`, `TotalCount`, and `TotalPages` as state for the grid and footer.

## Goals / Non-Goals

**Goals:**
- Make Ursa pagination in the data management dialog behave identically to the existing selection popups.
- Ensure clicking any paging control always reloads data for the correct `CurrentPage` using server-side paging.
- Keep the XAML surface (`u:Pagination` bindings) stable; fix should be localized to the ViewModel behavior.

**Non-Goals:**
- No changes to the `ISolidWasteService` interface or DTO contracts.
- No redesign of the dialog layout, filters, or grid columns.
- No new client-side paging; paging remains server-side via the application service.

## Decisions

- **Align command signature with Ursa expectations**
  - Change `PageChangeCommand` from `ReactiveCommand<int>` to a parameterless `ICommand` (e.g., generated via `[ReactiveCommand] PageChangeAsync()`), matching the pattern used by `GenericSelectionPopupViewModel`.
  - Rationale: Ursa already provides the new page through the TwoWay binding to `CurrentPage`; a parameterless command is sufficient for triggering reload logic and is consistent across the codebase.

- **Drive paging from `CurrentPage`**
  - Use `CurrentPage` as the single source of truth for the current page index:
    - Either: keep `CurrentPage` as a reactive auto-property and have the paging command call `LoadDataAsync()` which uses `CurrentPage`.
    - Or: add logic in the `CurrentPage` setter to call `LoadDataAsync()` whenever the page changes, and keep a lightweight command hook for Ursa to trigger.
  - Rationale: Centralizing paging behavior around `CurrentPage` simplifies reasoning and mirrors the selection popup implementation.

- **Preserve existing `LoadDataAsync` semantics**
  - Reuse the existing `LoadDataAsync` method that builds the filter, calls `_solidWasteService.GetPagedExportRowsAsync`, and updates `Records`, `TotalCount`, and `TotalPages`.
  - Rationale: Service contract and DTOs are already designed for paging; only the trigger mechanism is faulty.

## Risks / Trade-offs

- **Risk: double-loading on page change**
  - If both the `CurrentPage` setter and the paging command call `LoadDataAsync()`, a single user action might trigger two loads.
  - Mitigation: Choose a single responsibility pattern—either have only the command call `LoadDataAsync()` (and let Ursa modify `CurrentPage`), or have only the setter call `LoadDataAsync()` and keep the command as a no-op or simple wrapper.

- **Risk: inconsistent `TotalPages` vs. `CurrentPage`**
  - If `TotalPages` is not updated correctly from the backend result, guards like `if (page < 1 || page > TotalPages)` may reject valid page changes.
  - Mitigation: Keep the existing normalization logic (`CurrentPage` clamped to `[1, TotalPages]`) and rely on server-reported `TotalCount` to compute `TotalPages`.

- **Risk: backend returns incorrect `TotalCount`**
  - If the backend returns `TotalCount = 0` while items are present, Ursa and the footer will present misleading page information.
  - Mitigation: This design assumes the service returns consistent paging metadata; issues at that layer should be solved via separate changes if observed.

