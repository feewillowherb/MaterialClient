# Spec: Searchable Selection Component

## ADDED Requirements

### Requirement: Unified Searchable Selection Component

The system SHALL provide a unified `SearchableComboBox` component that combines selection display and search input functionality with a popup list, following Avalonia UI + ReactiveUI patterns.

#### Scenario: Display selected item when popup is closed

- **GIVEN** the component has a selected item
- **WHEN** the popup is closed
- **THEN** the component displays the selected item's display text
- **AND** the dropdown arrow indicator is visible

#### Scenario: Show placeholder when no selection

- **GIVEN** the component has no selected item
- **WHEN** the popup is closed
- **THEN** the component displays the placeholder text (default: "请选择")
- **AND** the placeholder text is gray (#999999)

#### Scenario: Open popup on focus or click

- **GIVEN** the component is rendered and popup is closed
- **WHEN** the user clicks the component or tabs to focus it
- **THEN** the popup opens below the component
- **AND** the search input is focused
- **AND** the first page of data is loaded

#### Scenario: Close popup on escape key

- **GIVEN** the popup is open
- **WHEN** the user presses the Escape key
- **THEN** the popup closes
- **AND** the current selection is preserved
- **AND** the component returns to display mode

#### Scenario: Close popup when clicking outside

- **GIVEN** the popup is open
- **WHEN** the user clicks outside the popup area
- **THEN** the popup closes
- **AND** the current selection is preserved

### Requirement: Search Functionality with Debouncing

The system SHALL provide real-time search functionality with 300ms debouncing to filter the popup list based on user input.

#### Scenario: Filter list after debounce delay

- **GIVEN** the popup is open and data is loaded
- **WHEN** the user types search text
- **THEN** the search is NOT executed immediately
- **AND** after 300ms of no further input, the search executes
- **AND** the list is filtered to show matching items

#### Scenario: Reset search when clearing text

- **GIVEN** the user has entered search text and filtered the list
- **WHEN** the user clears all search text
- **THEN** the full unfiltered list is displayed
- **AND** pagination resets to page 1

#### Scenario: Cancel previous pending search on new input

- **GIVEN** the user has typed and a 300ms debounce timer is active
- **WHEN** the user types another character before the timer expires
- **THEN** the previous timer is cancelled
- **AND** a new 300ms timer starts with the complete search text

### Requirement: Pagination Support

The system SHALL support pagination for both client-side and server-side paging modes with configurable page size.

#### Scenario: Navigate to next page

- **GIVEN** the popup is open and multiple pages of data exist
- **WHEN** the user clicks the next page button
- **THEN** the current page increments by 1
- **AND** the list updates to show the next page of items
- **AND** the page info displays "当前页:X"

#### Scenario: Display total count info

- **GIVEN** the popup is open and data is loaded
- **WHEN** the user views the pagination controls
- **THEN** the total count is displayed as "共X条记录"
- **AND** the current page is displayed as "当前页:X"

#### Scenario: Reset to page 1 on new search

- **GIVEN** the user is on page 3 or higher
- **WHEN** the user types new search text
- **THEN** the pagination resets to page 1
- **AND** the filtered results are displayed starting from page 1

### Requirement: Item Selection

The system SHALL allow users to select a single item from the popup list and update the display accordingly.

#### Scenario: Select item by click

- **GIVEN** the popup is open and items are displayed
- **WHEN** the user clicks on an item in the list
- **THEN** the item becomes the selected item
- **AND** the component display updates to show the selected item's text
- **AND** the popup closes (if CloseOnSelect is enabled)

#### Scenario: Select item by double-tap

- **GIVEN** the popup is open and items are displayed
- **WHEN** the user double-taps on an item in the list
- **THEN** the item becomes the selected item
- **AND** the component display updates to show the selected item's text
- **AND** the popup closes

#### Scenario: Keyboard selection with Enter key

- **GIVEN** the popup is open and an item is focused
- **WHEN** the user presses the Enter key
- **THEN** the focused item becomes the selected item
- **AND** the component display updates
- **AND** the popup closes

### Requirement: Create New Item Functionality

The system SHALL allow users to create new items when search returns no results, provided the feature is enabled.

#### Scenario: Show add new button when no results

- **GIVEN** the AllowAddNew feature is enabled
- **AND** the user has entered search text
- **AND** the search returns zero results
- **WHEN** the popup content is rendered
- **THEN** an "Add New" button is displayed
- **AND** the message "未找到匹配结果" is shown
- **AND** the DataGrid is hidden

#### Scenario: Create and select new item

- **GIVEN** the "Add New" button is visible
- **AND** the user has entered search text "新材料"
- **WHEN** the user clicks the "Add New" button
- **THEN** the system calls the CreateNewItemFunc with "新材料"
- **AND** if creation succeeds, the new item is inserted at the top of the list
- **AND** the new item becomes the selected item
- **AND** the component display updates to show the new item
- **AND** the popup closes

#### Scenario: Do not show add button when search is empty

- **GIVEN** the AllowAddNew feature is enabled
- **AND** the search text is empty or whitespace only
- **WHEN** the popup content is rendered
- **THEN** the "Add New" button is NOT displayed
- **AND** the empty state message is NOT displayed

### Requirement: Client-Side Paging Mode

The system SHALL support client-side paging where all data is loaded once and filtering/pagination happens in memory.

#### Scenario: Load all data on initialization

- **GIVEN** the component is configured for client-side paging
- **AND** a LoadAllFunc is provided
- **WHEN** the component is initialized
- **THEN** the LoadAllFunc is called once to load all items
- **AND** items are stored in memory for filtering and pagination

#### Scenario: Filter in memory on search

- **GIVEN** client-side paging mode is active
- **AND** all items are loaded in memory
- **WHEN** the user types search text
- **THEN** filtering is performed on the in-memory items
- **AND** no API call is made
- **AND** the filtered results are displayed

### Requirement: Server-Side Paging Mode

The system SHALL support server-side paging where each page is loaded on-demand from a data source.

#### Scenario: Load first page on popup open

- **GIVEN** the component is configured for server-side paging
- **AND** a LoadPageFunc is provided
- **WHEN** the popup opens for the first time
- **THEN** LoadPageFunc is called with page=1, search=null, and selectedIds
- **AND** the returned items are displayed in the list

#### Scenario: Load specific page on navigation

- **GIVEN** server-side paging mode is active
- **AND** the user is currently viewing page 1
- **WHEN** the user navigates to page 3
- **THEN** LoadPageFunc is called with page=3, current search text, and selectedIds
- **AND** the returned items are displayed

#### Scenario: Include selected item in all pages

- **GIVEN** server-side paging mode is active
- **AND** an item is currently selected
- **AND** the selected item is on page 5
- **WHEN** the user navigates to page 1
- **THEN** LoadPageFunc includes the selected item's ID in the selectedIds parameter
- **AND** if the API returns it, the selected item remains visible on page 1

### Requirement: Memory Safety

The system SHALL ensure all Rx subscriptions are properly disposed to prevent memory leaks during long-running operation.

#### Scenario: Dispose subscriptions when component is destroyed

- **GIVEN** the component has active Rx subscriptions for search throttling and selection changes
- **WHEN** the component is destroyed (e.g., view closed)
- **THEN** all subscriptions are disposed
- **AND** no references to the component or its ViewModel remain

#### Scenario: Prevent subscription leaks on repeated open/close

- **GIVEN** the user opens and closes the popup 100 times
- **WHEN** memory usage is profiled
- **THEN** memory usage should not increase monotonically
- **AND** subscription count should remain bounded

### Requirement: MVVM Pattern Compliance

The component SHALL follow the Model-View-ViewModel pattern with clear separation between UI (View) and logic (ViewModel).

#### Scenario: ViewModel contains all business logic

- **GIVEN** the component's architecture is reviewed
- **WHEN** examining the View files (.axaml, .axaml.cs)
- **THEN** NO business logic is present in the View code-behind
- **AND** all logic resides in the ViewModel
- **AND** the View only contains UI-specific code (focus management, visual state)

#### Scenario: Use ReactiveUI SourceGenerators

- **GIVEN** the ViewModel is implemented
- **WHEN** reviewing the ViewModel source code
- **THEN** reactive properties use `[Reactive]` attribute from ReactiveUI.SourceGenerators
- **AND** manual `RaiseAndSetIfChanged` calls are NOT used
- **AND** reactive commands use `[ReactiveCommand]` attribute

### Requirement: Keyboard Accessibility

The component SHALL be fully navigable and operable using keyboard only.

#### Scenario: Navigate list items with arrow keys

- **GIVEN** the popup is open and items are displayed
- **WHEN** the user presses ArrowDown or ArrowUp keys
- **THEN** the focus moves to the next or previous item in the list
- **AND** the focused item is visually indicated

#### Scenario: Open popup with Alt+Down Arrow

- **GIVEN** the component is focused and the popup is closed
- **WHEN** the user presses Alt+Down Arrow
- **THEN** the popup opens
- **AND** the search input is focused

#### Scenario: Tab focus moves to next control

- **GIVEN** the popup is open
- **WHEN** the user presses the Tab key
- **THEN** the popup closes
- **AND** focus moves to the next control in the tab order
