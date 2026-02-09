# Implementation Tasks

## 1. Foundation and Design

- [ ] 1.1 Review current implementation patterns in `SearchableSelectionBox`, `GenericSelectionPopup`, and related ViewModels
- [ ] 1.2 Research Avalonia UI custom control best practices for composite controls (trigger + popup)
- [ ] 1.3 Define new component API surface (properties, events, commands) following Avalonia UI + ReactiveUI patterns
- [ ] 1.4 Design state management approach using ReactiveUI (search throttling, selection state, popup visibility)
- [ ] 1.5 Plan memory management strategy (subscription disposal, `DisposeWith()` patterns, `RefCount()` for shared observables)

## 2. New Component Architecture

- [ ] 2.1 Create base interfaces and abstractions for selectable, searchable, and createable behaviors
- [ ] 2.2 Implement new `SearchableComboBox` (or `SearchableSelectionBox`) UserControl with:
  - Unified display/search state (shows selection when closed, search input when open)
  - Auto-popup management on focus/click
  - Proper `IsPopupOpen` property with two-way binding support
  - Placeholder text support
- [ ] 2.3 Implement `SelectionListPopup` UserControl (simplified from `GenericSelectionPopup`):
  - Remove internal search box (search happens in trigger)
  - Keep DataGrid with single selection mode
  - Keep pagination controls (Ursa `Pagination`)
  - Add "Create New" button shown when no results + search text
- [ ] 2.4 Refactor `GenericSelectionPopupViewModel<T>` or create new `SearchableSelectionViewModel<T>`:
  - Use `ReactiveUI.SourceGenerators` for property generation (`[Reactive]`)
  - Implement search throttling (300ms) with `Throttle()`
  - Ensure proper subscription disposal
  - Support both ClientSide and ServerSide paging modes
  - Expose clear command properties for selection, page change, and create new
- [ ] 2.5 Create factory/helper methods for component initialization (reduce boilerplate for common use cases)

## 3. State Management and Reactive Patterns

- [ ] 3.1 Implement search text debouncing using `WhenAnyValue(x => x.SearchText).Throttle()`
- [ ] 3.2 Implement selection state propagation (trigger display updates when `SelectedItem` changes)
- [ ] 3.3 Implement popup open/close state management with proper focus handling
- [ ] 3.4 Add memory-safe observable chains with `DisposeWith()` disposables
- [ ] 3.5 Implement "Createable" pattern (show add button when no results + search text, insert into list, select new item)

## 4. Testing Infrastructure

- [ ] 4.1 Create unit tests for `SearchableSelectionViewModel<T>`:
  - Search filtering (client-side and server-side)
  - Pagination state changes
  - Selection state changes
  - Create new item behavior
- [ ] 4.2 Create UI integration tests for `SearchableComboBox` + `SelectionListPopup`:
  - Popup open/close on focus/click
  - Search triggers filtering
  - Selection updates display
  - Create new item flow
- [ ] 4.3 Create memory leak tests using long-running simulation pattern from `AttendedWeighingServiceMemoryLeakTests`
- [ ] 4.4 Add tests for proper Rx subscription disposal
- [ ] 4.5 Add tests for edge cases (empty list, search with no results, rapid search changes)

## 5. Migration - Proof of Concept

- [ ] 5.1 Update `AttendedWeighingDetailViewModel` to use new component for one field (e.g., Provider selection)
- [ ] 5.2 Update `SolidWasteModeFormView.axaml` to use new `SearchableComboBox` for Provider field
- [ ] 5.3 Test Provider selection flow end-to-end (search, select, create new)
- [ ] 5.4 Verify backward compatibility with existing data loading logic
- [ ] 5.5 Document any issues found and adjust design based on PoC feedback

## 6. Migration - Remaining Usage Sites

- [ ] 6.1 Migrate Material selection in `SolidWasteModeFormView.axaml`
- [ ] 6.2 Migrate Street selection in `SolidWasteModeFormView.axaml`
- [ ] 6.3 Migrate Material selection in `StandardModeFormView.axaml`
- [ ] 6.4 Update all three ViewModels in `AttendedWeighingDetailViewModel.cs`
- [ ] 6.5 Remove old popup state management code (`IsProvidersPopupOpen`, etc.) after migration

## 7. Deprecation and Cleanup

- [ ] 7.1 Add `[Obsolete]` attribute to `SearchableSelectionBox` (old version)
- [ ] 7.2 Add `[Obsolete]` attribute to `GenericSelectionPopup` (old version)
- [ ] 7.3 Add migration guide comments to obsolete attributes pointing to new component
- [ ] 7.4 Update project documentation with new component usage examples
- [ ] 7.5 Create "Before and After" comparison documentation for reference

## 8. Validation and Documentation

- [ ] 8.1 Run full test suite and ensure all tests pass
- [ ] 8.2 Perform manual UI testing for all selection scenarios:
  - Open popup, search, select item
  - Search with no results, create new item
  - Pagination navigation
  - Keyboard navigation (Tab, Enter, Escape)
- [ ] 8.3 Run memory leak tests with 1-hour simulation
- [ ] 8.4 Verify no console warnings or errors from Avalonia/ReactiveUI bindings
- [ ] 8.5 Document API surface (properties, commands, events) in XML comments
- [ ] 8.6 Create usage examples for common scenarios (client-side paging, server-side paging, with/without create new)
- [ ] 8.7 Update `openspec/project.md` if new patterns or conventions are introduced

## 9. Code Review and Refinement

- [ ] 9.1 Peer review of new component architecture
- [ ] 9.2 Address code review feedback
- [ ] 9.3 Final cleanup of any remaining TODO comments or temporary code
- [ ] 9.4 Ensure all files follow project coding style (nullable reference types, implicit usings, naming conventions)
- [ ] 9.5 Verify all Rx subscriptions are properly disposed

## Dependencies and Notes

**Task dependencies**:
- Tasks 2.x and 3.x can be done in parallel after 1.x is complete
- Task 4.x should be done alongside 2.x and 3.x (test-driven approach)
- Task 5.x must complete before 6.x (proof of concept validation)
- Tasks 7.x and 8.x happen after all migrations are complete

**Parallel work opportunities**:
- Unit tests (4.1) can be written alongside ViewModel implementation (2.4)
- UI integration tests (4.2) can be written alongside UserControl implementation (2.2, 2.3)
- Memory leak tests (4.3) can be set up early and run continuously

**Key validation checkpoints**:
- After task 2.4: Verify ViewModel compiles and follows ReactiveUI patterns
- After task 5.5: Proof of concept approval before full migration
- After task 8.3: Memory leak test must pass before proceeding to deprecation
