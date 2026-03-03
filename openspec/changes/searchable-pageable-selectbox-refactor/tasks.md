## 1. Control Signature Updates

- [x] 1.1 Update LoadPageAsyncProperty to accept 5-parameter delegate signature (`string?, int, int, IReadOnlyList<int>?, CancellationToken`)
- [x] 1.2 Add SearchTextProperty as styled property to replace private `_searchText` field
- [x] 1.3 Add CurrentPageProperty as styled property to replace private `_currentPage` field
- [x] 1.4 Update all internal references from `_searchText` to `SearchText` property getter
- [x] 1.5 Update all internal references from `_currentPage` to `CurrentPage` property getter
- [x] 1.6 Remove private field declarations for `_searchText` and `_currentPage`

## 2. State Synchronization Implementation

- [x] 2.1 Add OnPropertyChanged override to detect SelectedItem changes
- [x] 2.2 Create UpdateTextBoxText helper method to sync TextBox with SelectedItem
- [x] 2.3 Call UpdateTextBoxText from OnPropertyChanged when SelectedItemProperty changes
- [x] 2.4 Add CurrentPage property change detection in OnPropertyChanged override
- [x] 2.5 Trigger LoadPageAsyncInternal when CurrentPage changes (excluding initial set)

## 3. Popup Reset Behavior

- [x] 3.1 Modify OnPopupClosed event handler to implement reset logic
- [x] 3.2 Add GetDisplayText helper method to extract display text from SelectedItem
- [x] 3.3 Update OnPopupClosed to call GetDisplayText(SelectedItem) and set TextBox.Text
- [x] 3.4 Ensure OnPopupClosed resets _searchText/SearchText to SelectedItem display value
- [x] 3.5 Verify TextBox retains focus after popup closes

## 4. LoadPageAsync with selectedIds Support

- [x] 4.1 Extract selected item ID using GetItemId delegate in LoadPageAsyncInternal
- [x] 4.2 Build selectedIds array (null if no selection, otherwise single ID array)
- [x] 4.3 Pass selectedIds to LoadPageAsync delegate call as 4th parameter
- [x] 4.4 Pass CancellationToken token as 5th parameter to LoadPageAsync delegate call
- [x] 4.5 Ensure CancellationTokenSource properly cancels previous requests before starting new ones

## 5. Template Binding Updates

- [x] 5.1 Verify SearchTextProperty is registered before template usage
- [x] 5.2 Update PART_TextBox Watermark binding if needed for SearchText property
- [x] 5.3 Verify CurrentPageProperty TwoWay binding with Ursa Pagination component
- [x] 5.4 Test template bindings compile with new properties

## 6. ViewModel Method Signature Updates

- [x] 6.1 Update LoadPagedProvidersAsync in AttendedWeighingDetailViewModel to accept new delegate parameters
- [x] 6.2 Remove manual selectedIds extraction from LoadPagedProvidersAsync (control now handles this)
- [x] 6.3 Update LoadPagedMaterialsAsync in AttendedWeighingDetailViewModel to accept new delegate parameters
- [x] 6.4 Remove manual selectedIds extraction from LoadPagedMaterialsAsync (control now handles this)
- [x] 6.5 Verify both methods pass received parameters directly to service layer calls

## 7. Compilation and Build Verification

- [x] 7.1 Compile MaterialClient project and resolve any compiler errors
- [x] 7.2 Fix any type mismatch errors from signature changes
- [x] 7.3 Fix any missing namespace issues if styled properties require additional imports
- [x] 7.4 Verify zero compiler warnings related to the refactored code
- [ ] 7.5 Ensure project builds successfully (blocked by external file lock - close running application first)

## 8. Manual Testing - Basic Functionality

- [ ] 8.1 Test provider selection: click control, open popup, select item, verify TextBox updates
- [ ] 8.2 Test watermark display: verify control shows Watermark when no item selected
- [ ] 8.3 Test popup opening: click control when item is selected, verify results include selected item
- [ ] 8.4 Test popup opening: click control when no item selected, verify results load with empty search

## 9. Manual Testing - Search Behavior

- [ ] 9.1 Test debounced search: type text, wait 300ms, verify results load
- [ ] 9.2 Test debounce cancel: type partial, type more before 300ms, verify first request cancelled
- [ ] 9.3 Test search results: type "Provider ABC", verify filtered results display
- [ ] 9.4 Test selected item guarantee: select "Provider A", type "Provider A", verify A appears in results

## 10. Manual Testing - Reset Behavior

- [ ] 10.1 Test Escape key reset: type "ABC", press Escape, verify TextBox shows selected item
- [ ] 10.2 Test click-outside reset: type "ABC", click outside popup, verify TextBox shows selected item
- [ ] 10.3 Test SelectedItem persistence: after reset, verify SelectedItem unchanged
- [ ] 10.4 Test focus restoration: after reset, verify TextBox has focus

## 11. Manual Testing - Keyboard Navigation

- [ ] 11.1 Test Arrow Up navigation: press Up, verify highlight moves to previous item
- [ ] 11.2 Test Arrow Down navigation: press Down, verify highlight moves to next item
- [ ] 11.3 Test Enter selection: highlight item, press Enter, verify item selected and popup closes
- [ ] 11.4 Test Escape key close: press Escape, verify popup closes and TextBox resets

## 12. Manual Testing - Pagination

- [ ] 12.1 Test Next Page button: click Next, verify CurrentPage updates and page 2 loads
- [ ] 12.2 Test Previous Page button: on page 2+, click Prev, verify CurrentPage updates and previous page loads
- [ ] 12.3 Test pagination with search: type text, navigate pages, verify search text persists
- [ ] 12.4 Test selectedIds with pagination: navigate pages, verify selected item still included
- [ ] 12.5 Test single page behavior: verify Next/Prev buttons disabled appropriately

## 13. Manual Testing - External SelectedItem Changes

- [ ] 13.1 Test external update: programmatically set SelectedItem from ViewModel, verify TextBox updates
- [ ] 13.2 Test null selection: set SelectedItem to null, verify Watermark displays
- [ ] 13.3 Test two-way binding: change selection via control, verify ViewModel property updates
- [ ] 13.4 Test property notifications: verify OnPropertyChanged correctly handles SelectedItem changes

## 14. Edge Case Testing

- [ ] 14.1 Test rapid typing: type multiple characters quickly, verify only last request fires
- [ ] 14.2 Test concurrent requests: click next page while loading, verify cancellation works
- [ ] 14.3 Test empty results: type non-matching text, verify empty list displays
- [ ] 14.4 Test large result sets: navigate through many pages, verify pagination controls work correctly
- [ ] 14.5 Test network delay: slow service response, verify loading indicator displays

## 15. Documentation and Cleanup

- [ ] 15.1 Review and update XML comments in SearchablePageableSelectBox.axaml if needed
- [ ] 15.2 Review and update code comments in SearchablePageableSelectBox.axaml.cs for clarity
- [ ] 15.3 Remove any debug code or console logging added during development
- [ ] 15.4 Verify code follows project formatting and style conventions
- [ ] 15.5 Ensure no unused using statements remain in the refactored files
