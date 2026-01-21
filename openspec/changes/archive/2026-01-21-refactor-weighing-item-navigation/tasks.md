## 1. Event Infrastructure

- [x] 1.1 Create `ItemOperationCompletedEventArgs` class with operation context fields
- [x] 1.2 Update event definitions in `AttendedWeighingDetailViewModel` to use new event args
- [x] 1.3 Modify event invocations in Save/Complete/Match/Abolish methods to pass complete context
- [x] 1.4 Verify event arguments include: ItemId, ItemType, OrderType, IsCompleted, OperationType

## 2. Core Navigation Logic

- [x] 2.1 Implement `NavigateToItemAsync(ItemOperationCompletedEventArgs)` method in `AttendedWeighingViewModel`
- [x] 2.2 Extract `DetermineTargetTab(WeighingListItemDto)` logic respecting `IsShowAllRecords` flag
- [x] 2.3 Implement `FindItemAcrossPagesAsync(long itemId, WeighingListItemType)` for pagination search
- [x] 2.4 Add view selection logic (MainView vs DetailView) based on ItemType and OrderType

## 3. Event Handler Refactoring

- [x] 3.1 Refactor `OnDetailSaveCompleted` to use unified `NavigateToItemAsync`
- [x] 3.2 Refactor `OnDetailCompleteCompleted` to use unified `NavigateToItemAsync`
- [x] 3.3 Refactor `OnDetailMatchCompleted` to use unified `NavigateToItemAsync`
- [x] 3.4 Refactor `OnDetailAbolishCompleted` to use unified `NavigateToItemAsync`
- [x] 3.5 Refactor `OnDetailManualMatchSaveCompleted` to use unified `NavigateToItemAsync`

## 4. Tab Switching Logic

- [x] 4.1 Implement tab switch decision logic (respect `IsShowAllRecords`)
- [x] 4.2 Add tab switching only when current tab doesn't contain target item
- [x] 4.3 Ensure `IsShowUnmatched` ↔ `IsShowCompleted` switching works correctly

## 5. Testing and Validation

- [x] 5.1 Test Save operation: verify item selection, tab state, view display
- [x] 5.2 Test Complete operation: verify completed waybill appears in MainView on correct tab/page
- [x] 5.3 Test Match operation: verify navigation to matched waybill
- [x] 5.4 Test Abolish operation: verify navigation to next unmatched item
- [x] 5.5 Test cross-page navigation: verify item found when on different page
- [x] 5.6 Test tab switching: verify respects `IsShowAllRecords` flag
- [x] 5.7 Test view selection: verify MainView for completed waybills, DetailView for others
- [x] 5.8 Verify no regression in existing attended weighing workflow

## 6. Documentation

- [x] 6.1 Update code comments to document navigation logic
- [x] 6.2 Document tab switching rules in AttendedWeighingViewModel
- [x] 6.3 Document event argument structure and usage
