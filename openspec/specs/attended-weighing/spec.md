# attended-weighing Specification

## Purpose
TBD - created by archiving change refactor-weighing-item-navigation. Update Purpose after archive.
## Requirements
### Requirement: Item Navigation After Operations

The system SHALL provide consistent navigation to WeighingListItemDto objects after operations (Save, Complete, Match, Abolish) are performed.

#### Scenario: Complete operation navigation
- **WHEN** user completes a waybill (FirstWeight → Completed) in AttendedWeighingDetailView
- **THEN** the system SHALL:
  - Refresh the list data to reflect the completed state
  - Select the newly completed waybill in the list
  - Display the completed waybill in AttendedWeighingMainView (not DetailView)
  - Navigate to the correct page if the item is not on the current page
  - Switch to the appropriate tab only if necessary (respect IsShowAllRecords flag)

#### Scenario: Save operation navigation
- **WHEN** user saves changes to a weighing record or waybill in AttendedWeighingDetailView
- **THEN** the system SHALL:
  - Refresh the list data to reflect the saved changes
  - Keep the saved item selected in the list
  - Remain in AttendedWeighingDetailView (allow continued editing)
  - Stay on the current tab (item state hasn't changed)
  - Navigate to the correct page if the item moved due to sorting

#### Scenario: Match operation navigation
- **WHEN** user manually matches a weighing record with another record
- **THEN** the system SHALL:
  - Refresh the list data to show the newly created waybill
  - Select the next unmatched item in the list
  - Display the next unmatched item in AttendedWeighingDetailView
  - Switch to unmatched tab if currently on completed tab and not showing all records
  - Navigate to the correct page for the next item

#### Scenario: Abolish operation navigation
- **WHEN** user abolishes (deletes) a weighing record
- **THEN** the system SHALL:
  - Refresh the list data to remove the abolished record
  - Select the next unmatched item in the list
  - Display the next unmatched item in AttendedWeighingDetailView
  - Stay on the current tab (item was removed, not moved)
  - Navigate to the correct page for the next item

### Requirement: Tab Switching Rules

The system SHALL implement intelligent tab switching that respects user context and only switches when necessary.

#### Scenario: Tab switching respects "All Records" mode
- **WHEN** IsShowAllRecords flag is true (user selected "All Records" tab)
- **THEN** the system SHALL NOT automatically switch tabs after any operation
- **BECAUSE** all items are visible on this tab regardless of completion status

#### Scenario: Tab switching when item moves to completed state
- **WHEN** an item becomes completed (OrderType changes to Completed)
- **AND** IsShowUnmatched is true (user is on "Unmatched" tab)
- **AND** IsShowAllRecords is false
- **THEN** the system SHALL switch to IsShowCompleted = true ("Completed" tab)

#### Scenario: Tab switching when item moves to unmatched state
- **WHEN** an item becomes unmatched (OrderType changes to FirstWeight or Unmatch)
- **AND** IsShowCompleted is true (user is on "Completed" tab)
- **AND** IsShowAllRecords is false
- **THEN** the system SHALL switch to IsShowUnmatched = true ("Unmatched" tab)

#### Scenario: No tab switching when current tab contains target item
- **WHEN** an operation completes and the resulting item's state matches the current tab filter
- **THEN** the system SHALL NOT switch tabs
- **EXAMPLE**: User on "Completed" tab saves a completed waybill → stay on "Completed" tab

### Requirement: Cross-Page Item Navigation

The system SHALL find and navigate to items across pagination boundaries.

#### Scenario: Item found on current page
- **WHEN** navigating to a target item after an operation
- **AND** the target item is present on the current page
- **THEN** the system SHALL:
  - Select the item immediately without changing pages
  - Complete navigation in O(1) time

#### Scenario: Item found on different page
- **WHEN** navigating to a target item after an operation
- **AND** the target item is NOT present on the current page
- **THEN** the system SHALL:
  - Search across pages starting from page 1
  - Navigate to the page containing the target item
  - Select the item once found
  - Limit search to a maximum of 10 pages to prevent excessive loading

#### Scenario: Item not found after search
- **WHEN** navigating to a target item after an operation
- **AND** the target item cannot be found after searching available pages
- **THEN** the system SHALL:
  - Fall back to selecting the first item in the current list
  - Log a warning about the missing item
  - Not display an error to the user (graceful degradation)

### Requirement: View Selection Based on Item State

The system SHALL automatically select the appropriate view (MainView or DetailView) based on the item's type and completion status.

#### Scenario: Completed waybill displays in MainView
- **WHEN** navigating to an item that is a Waybill
- **AND** the waybill's OrderType is Completed
- **THEN** the system SHALL display AttendedWeighingMainView (read-only summary view)

#### Scenario: Editable items display in DetailView
- **WHEN** navigating to an item that is NOT a completed waybill
- **EXAMPLES**: WeighingRecord (unmatched), Waybill with OrderType = FirstWeight
- **THEN** the system SHALL display AttendedWeighingDetailView (editable form view)

#### Scenario: View selection after Complete operation
- **WHEN** user completes a waybill (changes OrderType from FirstWeight to Completed)
- **THEN** the system SHALL switch from AttendedWeighingDetailView to AttendedWeighingMainView
- **BECAUSE** the item is now read-only and optimized for viewing in MainView

### Requirement: Operation Event Context

The system SHALL provide complete context information in operation events to enable proper navigation.

#### Scenario: Event arguments include operation context
- **WHEN** an operation (Save, Complete, Match, Abolish) completes in AttendedWeighingDetailView
- **THEN** the raised event SHALL include:
  - ItemId: The ID of the resulting item
  - ItemType: Whether the item is a WeighingRecord or Waybill
  - OrderType: The current order type (Unmatch, FirstWeight, Completed)
  - IsCompleted: Boolean flag for quick completion status check
  - OperationType: String identifying which operation was performed

#### Scenario: Complete operation event
- **WHEN** user completes a waybill successfully
- **THEN** CompleteCompleted event SHALL be raised with:
  - ItemId = the waybill ID
  - ItemType = Waybill
  - OrderType = Completed
  - IsCompleted = true
  - OperationType = "Complete"

#### Scenario: Save operation event
- **WHEN** user saves changes to a record or waybill
- **THEN** SaveCompleted event SHALL be raised with:
  - ItemId = the saved item's ID
  - ItemType = the item's current type
  - OrderType = the item's current order type
  - IsCompleted = based on OrderType
  - OperationType = "Save"

### Requirement: Unified Navigation Logic

The system SHALL use a single unified method for all post-operation navigation to ensure consistency.

#### Scenario: All operations use NavigateToItemAsync
- **WHEN** any operation event handler is triggered (Save, Complete, Match, Abolish, ManualMatch)
- **THEN** the handler SHALL call the unified NavigateToItemAsync method
- **AND** NavigateToItemAsync SHALL handle:
  - Data refresh
  - Tab switching decision
  - Page navigation
  - Item selection
  - View selection

#### Scenario: Navigation logic is predictable and testable
- **WHEN** testing navigation behavior
- **THEN** all navigation paths SHALL go through NavigateToItemAsync
- **ALLOWING** single point of testing and maintenance
- **ENSURING** consistent behavior across all operations

