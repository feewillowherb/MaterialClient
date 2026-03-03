## Why

The SearchablePageableSelectBox control was implemented to replace the fragmented three-part selection pattern (SearchableSelectionBox + Popup + GenericSelectionPopup), but it has critical implementation gaps that prevent it from working correctly. The control cannot properly reset user input, synchronize with SelectedItem changes, or pass selectedIds to the service layer, causing user confusion and broken selection behavior.

## What Changes

- **BREAKING**: Update `LoadPageAsync` delegate signature from `Func<string, int, int, Task<PagedResultDto<object>>>` to `Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<object>>>`
- Add TextBox reset behavior on popup close (Escape or click outside) to discard partial input and restore SelectedItem display
- Implement SelectedItem → TextBox synchronization via OnPropertyChanged handler
- Fix CurrentPage tracking to sync with Ursa Pagination component
- Convert SearchText from private field to styled property for template binding
- Pass selectedIds to LoadPageAsync call to ensure selected item appears in results
- Update ViewModel method signatures to match new LoadPageAsync delegate (if needed)

## Capabilities

This change aligns the implementation with the existing `ui-selection` capability defined in the `implement-creatable-pageable-searchable-selection` change. No new capabilities are introduced; this is an implementation fix to meet existing requirements.

### New Capabilities
None (refactoring to meet existing `ui-selection` spec requirements)

### Modified Capabilities
None (requirements unchanged - fixing implementation only)

## Impact

- **SearchablePageableSelectBox.axaml.cs**: Core control implementation - signature updates, state synchronization, reset logic
- **SearchablePageableSelectBox.axaml**: Template may need binding updates for SearchText property
- **AttendedWeighingDetailViewModel.cs**: LoadPagedProvidersAsync method signature may need adjustment

---

## User Interface

```
┌─────────────────────────────────────────────┐
│  SearchablePageableSelectBox               │
│  ┌───────────────────────────────────────┐ │
│  │ [Provider Name or Watermark]         │ │
│  └───────────────────────────────────────┘ │
│  ┌───────────────────────────────────────┐ │
│  │ PART_Popup (when open)            │ │
│  │ ┌─────────────────────────────────┐ │ │
│  │ │ Loading... [spinner]           │ │ │
│  └─────────────────────────────────┘ │ │
│  │ ┌─────────────────────────────────┐ │ │
│  │ │ Provider A                   │ │ │
│  │ │ Provider B (selected)          │ │ │
│  │ │ Provider C                   │ │ │
│  └─────────────────────────────────┘ │ │
│  │ [<] Page 1/5 [>] [+ Add New]   │ │
│  └───────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

## User Interaction Flow

```mermaid
sequenceDiagram
    participant U as User
    participant C as SearchablePageableSelectBox
    participant TB as TextBox
    participant L as LoadPageAsync
    participant S as Service

    Note over U,S: Scenario 1: Open and select
    U->>TB: Click control
    TB->>C: GotFocus event
    C->>L: LoadPageAsync(selectedText, 1, pageSize, [selectedId], ct)
    L->>S: GetPagedProvidersAsync(searchText, 1, 10, [selectedId])
    S-->>L: Returns results (includes selected)
    L-->>C: Update ItemsSource
    C-->>U: Show popup with results
    U->>C: Click "Provider B"
    C->>C: Set SelectedItem = Provider B
    C->>TB: TB.Text = "Provider B"
    C->>C: Close popup

    Note over U,S: Scenario 2: Type and cancel
    U->>TB: Type "ABC"
    TB->>C: TextChanged event
    C->>C: Start 300ms debounce
    C->>L: LoadPageAsync("ABC", 1, pageSize, [selectedId], ct)
    L-->>C: Returns filtered results
    U->>TB: Press Escape or click outside
    C->>C: PopupClosing event
    C->>TB: TB.Text = "Provider B" (reset to selected)
    C->>C: Close popup

    Note over U,S: Scenario 3: Navigate pages
    U->>C: Click "Next page" button
    C->>L: LoadPageAsync(currentText, 2, pageSize, [selectedId], ct)
    L-->>C: Returns page 2 results
    C-->>U: Update items list
```

## Code Change Inventory

| File Path | Change Type | Change Description | Impact Scope |
|-----------|-------------|-------------------|--------------|
| MaterialClient/Views/SearchablePageableSelectBox.axaml.cs | Update | Fix LoadPageAsync signature, add reset logic, sync SelectedItem, add SearchText property | High |
| MaterialClient/Views/SearchablePageableSelectBox.axaml | Update | Update SearchText binding if needed | Low |
| MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs | Update | Update LoadPagedProvidersAsync signature to match new delegate | Medium |
