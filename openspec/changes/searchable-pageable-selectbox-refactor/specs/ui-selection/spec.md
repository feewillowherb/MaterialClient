## Specification: Searchable Pageable Selection Control - Refactor

This refactor specification references the existing `ui-selection` capability defined in the `implement-creatable-pageable-searchable-selection` change.

**No requirement changes are introduced by this refactor.** The purpose of this change is to fix implementation gaps so that the control correctly implements the existing requirements.

### Reference: Existing Capability

The `ui-selection` capability is fully specified in:
- `openspec/changes/implement-creatable-pageable-searchable-selection/specs/ui-selection/spec.md`

This existing spec defines all requirements for the SearchablePageableSelectBox control, including:
- Searchable, pageable selection with optional "add new"
- Popup opening/closing behavior with reset on cancel
- Debounced search (300ms)
- Keyboard navigation (arrows, Enter, Escape)
- Selected item guarantee via selectedIds parameter
- Control properties for configuration

### Refactor Scope

The `searchable-pageable-selectbox-refactor` change addresses implementation gaps without changing requirements:

| Implementation Gap | Related Requirement | Fix Approach |
|------------------|---------------------|---------------|
| LoadPageAsync signature mismatch | Requirement: Provide LoadPageAsync (spec line 221-227) | Update delegate to accept `selectedIds` and `CancellationToken` |
| Missing reset on popup close | Requirement: Popup Closing and Reset (spec line 62-84) | Implement OnPopupClosed to reset TextBox to SelectedItem display |
| No SelectedItem sync | Requirement: Bind SelectedItem (spec line 195-201) | Add OnPropertyChanged handler to update TextBox |
| CurrentPage not synced | Requirement: Pagination (spec line 142-168) | Convert to StyledProperty, bind TwoWay with pagination |
| SearchText not bindable | Template binding requirement | Convert to StyledProperty |
| selectedIds not passed | Requirement: Selected Item Guarantee (spec line 170-181) | Extract ID via GetItemId and pass to LoadPageAsync |

### Validation Approach

The refactor will be validated against all scenarios defined in the existing `ui-selection` spec. Key validation scenarios include:

| Scenario Group | Scenario Name | Validation Focus |
|---------------|----------------|------------------|
| Control displays | Display selected item | TextBox shows SelectedItem value |
| | Display watermark | TextBox shows Watermark when no selection |
| Popup opening | Open with selected item | Loads with selectedText and selectedIds |
| | Open without selection | Loads with empty searchText and null selectedIds |
| Debounced search | Debounced search triggers after 300ms | Waits 300ms, then loads page 1 |
| | Typing cancels previous debounce | Cancels timer, restarts on new input |
| Reset behavior | Reset on Escape key | Resets to SelectedItem, closes popup |
| | Reset on click outside | Resets to SelectedItem, closes popup |
| Selection | Select item from list | Updates SelectedItem, closes popup, updates TextBox |
| | Select item via Enter key | Same as above |
| | Add new item | Executes AddNewCommand, closes popup |
| Keyboard navigation | Navigate with Arrow Up | Moves highlight to previous item |
| | Navigate with Arrow Down | Moves highlight to next item |
| | Escape key closes and resets | As per reset behavior above |
| Pagination | Navigate to next page | Loads page N with current searchText and selectedIds |
| | Navigate to previous page | Loads page N-1 with current searchText and selectedIds |
| Selected item guarantee | Selected item included despite filter | Service includes ID in results when names match |
| | Normal filtering for non-selected | Normal filter applied when no selection |
| Properties | Bind SelectedItem | Two-way binding works correctly |
| | Configure DisplayMemberPath | Shows correct property value |
| | Configure Watermark | Shows watermark text when no selection |
| | Configure PageSize | Passes correct pageSize to LoadPageAsync |
| | Provide LoadPageAsync | Receives all 5 parameters correctly |
| | Provide GetItemId | Used to extract selected ID |

### Requirement Traceability

This refactor maintains full traceability to the original `ui-selection` spec:

```
searchable-pageable-selectbox-refactor
    ├── Implementation fixes (SearchablePageableSelectBox.axaml.cs)
    │   ├── LoadPageAsync signature update → ui-selection line 221-227
    │   ├── Reset behavior implementation → ui-selection line 62-84
    │   ├── SelectedItem sync → ui-selection line 195-201
    │   ├── SearchText property → Template binding requirement
    │   ├── CurrentPage property → ui-selection line 142-168
    │   └── selectedIds passing → ui-selection line 170-181
    └── ViewModel updates (AttendedWeighingDetailViewModel.cs)
        ├── LoadPagedProvidersAsync signature → ui-selection line 221-227
        └── LoadPagedMaterialsAsync signature → Same pattern
```

### No Requirement Changes

Since this is a refactoring effort to fix implementation gaps:

- **No ADDED Requirements**: All capabilities are already defined in ui-selection spec
- **No MODIFIED Requirements**: Requirement behavior is unchanged
- **No REMOVED Requirements**: No features are being removed
- **No RENAMED Requirements**: Requirement names remain the same

The `ui-selection` spec remains the source of truth for all functional requirements. This spec document serves to:
1. Document that no requirement changes are introduced
2. Trace implementation fixes to existing requirements
3. Provide validation approach for the refactor
4. Maintain traceability to the original spec

### Success Criteria

The refactor is successful when:

1. All scenarios in the existing `ui-selection` spec pass manual testing
2. LoadPageAsync delegate accepts the full signature: `(string?, int, int, IReadOnlyList<int>?, CancellationToken)`
3. TextBox resets to SelectedItem display when popup closes via Escape or click outside
4. SelectedItem changes externally (from ViewModel) correctly update the TextBox
5. CurrentPage syncs bidirectionally with Ursa Pagination component
6. selectedIds are correctly extracted and passed to the LoadPageAsync delegate
7. No compiler warnings or errors after updates
8. The control compiles and renders correctly in the test view
