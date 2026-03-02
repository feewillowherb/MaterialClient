## 1. Control Implementation

- [x] 1.1 Create `SearchablePageableSelectBox.axaml` with template structure
  - [x] PART_TextBox for display/input
  - [x] PART_Popup with Border container
  - [x] PART_LoadingOverlay for loading indicator
  - [x] PART_ItemsList (ListBox) for results
  - [x] PART_Pager for pagination controls
  - [x] PART_AddNew (Button) - optional
- [x] 1.2 Create `SearchablePageableSelectBox.axaml.cs` code-behind class
  - [x] Define dependency properties
  - [x] Implement PART discovery and attachment
  - [x] Set up event handlers

## 2. Core Properties and Data Binding

- [x] 2.1 Define dependency properties
  - [x] `SelectedItem` (TwoWay, object)
  - [x] `DisplayMemberPath` (string)
  - [x] `GetItemId` (Func<object, int?>)
  - [x] `Watermark` (string)
  - [x] `PageSize` (int, default 10)
  - [x] `LoadPageAsync` (Func<string?, int, int, IReadOnlyList<int>?, CancellationToken, Task<PagedResultDto<object>>>)
  - [x] `AddNewCommand` (ICommand, optional)
  - [x] `IsPopupOpen` (bool, optional)
  - [x] `IsLoading` (bool, read-only) - for loading indicator visibility
- [x] 2.2 Create observable collection for items display

## 3. Popup Lifecycle Management

- [x] 3.1 Implement popup opening logic
  - [x] On click/focus, open popup
  - [x] Set searchText to SelectedItem's display text (or empty)
  - [x] Load page 1 with current searchText and selectedIds
- [x] 3.2 Implement popup closing logic
  - [x] On Escape or click outside, reset to SelectedItem
  - [x] On selection, update SelectedItem and close
  - [x] On Add New, trigger command and close
- [x] 3.3 Implement light dismiss behavior

## 4. Loading Indicator Implementation

- [x] 4.1 Implement loading state management
  - [x] Set IsLoading to true before LoadPageAsync starts
  - [x] Set IsLoading to false in finally block
  - [x] Bind loading overlay visibility to IsLoading
  - [x] Hide items list and pager during loading
  - [x] Show loading spinner and "加载中..." text
- [x] 4.2 Implement loading overlay template
  - [x] PART_LoadingOverlay (Border) with white background
  - [x] ProgressBar with IsIndeterminate="True"
  - [x] TextBlock showing "加载中..."
  - [x] Center layout with horizontal orientation

## 5. Search and Debounce Logic

- [x] 5.1 Implement debounced search
  - [x] 300ms debounce timer
  - [x] Cancel previous debounce on new input
  - [x] Load page 1 with new searchText on debounce complete
- [x] 5.2 Handle search text synchronization
  - [x] Update TextBox on selection changes
  - [x] Reset TextBox on popup close (Escape/click-outside)
  - [x] Keep search text during navigation within popup

## 6. Pagination Implementation

- [x] 6.1 Implement pager controls
  - [x] Previous page button
  - [x] Next page button
  - [x] Page indicator
- [x] 6.2 Handle page navigation
  - [x] Load new page with current searchText and selectedIds
  - [x] Update displayed items
  - [x] Update pagination UI state

## 7. Keyboard Navigation

- [x] 7.1 Implement Arrow Up/Down navigation
  - [x] Move selection highlight in list
  - [x] Scroll as needed
- [x] 7.2 Implement Enter key
  - [x] Confirm current highlighted item
  - [x] Update SelectedItem
  - [x] Close popup
- [x] 7.3 Implement Escape key
  - [x] Reset to SelectedItem
  - [x] Close popup
  - [x] Return focus to TextBox

## 8. Service Layer Integration

- [x] 8.1 Update service methods to accept `selectedIds` parameter
  - [x] `GetPagedProvidersAsync`
  - [x] `GetPagedMaterialsAsync`
  - [x] Other paged selection methods
- [x] 8.2 Implement "selected item guarantee" logic
  - [x] If selectedIds contains items
  - [x] And searchText exactly matches an item's display name
  - [x] Then ignore searchText filter for that item

## 9. ViewModel Updates

- [x] 9.1 Update `AttendedWeighingDetailViewModel`
  - [x] Add `LoadPagedProvidersAsync` method
  - [x] Add `GetProviderId` delegate method
  - [x] Add `LoadPagedMaterialsAsync` method
  - [x] Add `GetMaterialId` delegate method
  - [x] Update SelectedProvider subscription to sync collections
  - [x] Update SelectedSolidWasteMaterial subscription to sync collections

## 10. View Integration

- [x] 10.1 Update `SolidWasteModeFormView.axaml`
  - [x] Remove provider SearchableSelectionBox
  - [x] Remove materials SearchableSelectionBox
  - [x] Remove ProvidersSelectionPopup
  - [x] Remove MaterialsSelectionPopup
  - [x] Add new SearchablePageableSelectBox for provider
  - [x] Add new SearchablePageableSelectBox for materials
  - [x] Configure all required properties

## 11. Testing and Validation

- [ ] 11.1 Manual testing scenarios
  - [ ] Click open with selected item - verify selection appears in results
  - [ ] Click open without selection - verify empty searchText
  - [ ] Type and press Escape - verify reset to previous selection
  - [ ] Type and select - verify SelectedItem updated correctly
  - [ ] Navigate pages - verify correct data loading
  - [ ] Keyboard arrows - verify highlight moves correctly
  - [ ] Enter key - verifies selection confirmed
  - [ ] Add New - verify command triggered (if implemented)
- [ ] 11.2 Edge case testing
  - [ ] Fast typing - verify debounce works
  - [ ] Slow typing - verify search triggers
  - [ ] Click outside - verify reset
  - [ ] Empty results - verify Add New appears (if implemented)
  - [ ] Single page - verify pager disabled
  - [ ] Large datasets - verify pagination works
  - [ ] Loading indicator - verify "加载中..." appears during data fetch
  - [ ] Loading state - verify items hidden while loading

## 12. Documentation

- [ ] 12.1 Document control usage
  - [ ] Property reference
  - [ ] Usage examples
  - [ ] Integration patterns
- [ ] 12.2 Document migration path
  - [ ] How to replace existing SearchableSelectionBox + Popup combos
  - [ ] Before/after examples
  - [ ] Common migration issues and solutions
