# Change: Refactor Weighing Item Navigation and Tracking

## Why

The current attended weighing workflow has inconsistent object tracking after operations (Save, Complete, Match, Abolish). Event handlers in `AttendedWeighingDetailViewModel` cannot properly finalize `WeighingListItemDto` objects, leading to:

1. **Lost context after operations**: User completes an order but the UI doesn't navigate to the correct item or view
2. **Incorrect view display**: System doesn't properly switch between MainView (summary) and DetailView (editing) based on item state
3. **Tab navigation confusion**: When items move between states (unmatched → completed), the tab selection doesn't follow the item
4. **Pagination blindness**: If the target item is on a different page, the system cannot find or navigate to it

This breaks the user's workflow and requires manual searching to continue working.

## What Changes

- **Event infrastructure enhancement**: Add complete operation context to event arguments (item ID, type, completion status, operation type)
- **Unified navigation logic**: Create centralized `NavigateToItemAsync` method that handles all post-operation navigation
- **Intelligent tab switching**: Implement rules that respect `IsShowAllRecords` flag and only switch tabs when necessary
- **Cross-page item search**: Add pagination navigation to find and select items across multiple pages
- **View selection automation**: Automatically choose MainView vs DetailView based on item type and completion status

## Impact

### Affected specs
- `attended-weighing` (new capability) - Defines requirements for item tracking and navigation behavior

### Affected code
- `MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs` - Event definitions and invocations
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs` - Navigation logic and event handlers
- `MaterialClient.Common/Events/` - New event argument classes

### Breaking changes
None - This is an internal refactoring that improves existing behavior without changing public APIs.

### User-visible changes
- After completing an order, user stays on the completed waybill in MainView (previously lost selection)
- Tab automatically switches to show the updated item when needed (respects "All Records" mode)
- System finds items across pages automatically (previously only searched current page)
- Correct view (MainView/DetailView) displays based on item state

## Migration

No migration needed - this is a behavior improvement, not a breaking change.
