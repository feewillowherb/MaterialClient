## ADDED Requirements

### Requirement: Searchable Pageable Selection Control

The system SHALL provide a `SearchablePageableSelectBox` TemplatedControl that integrates search, pagination, and optional "add new" functionality in a single self-contained component.

#### Scenario: Control displays selected item

- **GIVEN** a SearchablePageableSelectBox with a selected item
- **WHEN** the control renders
- **THEN** the TextBox SHALL display the selected item's DisplayMemberPath value
- **AND** the TextBox SHALL be editable for search input

#### Scenario: Control displays watermark when no selection

- **GIVEN** a SearchablePageableSelectBox with no selected item
- **WHEN** the control renders
- **THEN** the TextBox SHALL display the Watermark text
- **AND** the TextBox SHALL be editable for search input

### Requirement: Popup Opening Behavior

The control SHALL open its popup when the user clicks or focuses the TextBox.

#### Scenario: Open popup with selected item

- **GIVEN** a SearchablePageableSelectBox with a selected item
- **WHEN** the user clicks the TextBox
- **THEN** the popup SHALL open
- **AND** the system SHALL load page 1 with searchText equal to the selected item's display name
- **AND** the system SHALL include the selected item's ID in selectedIds parameter
- **AND** the list SHALL include the selected item in the results

#### Scenario: Open popup without selection

- **GIVEN** a SearchablePageableSelectBox with no selected item
- **WHEN** the user clicks the TextBox
- **THEN** the popup SHALL open
- **AND** the system SHALL load page 1 with empty searchText
- **AND** the system SHALL pass null for selectedIds parameter

### Requirement: Debounced Search

The control SHALL perform debounced search when the user types in the TextBox.

#### Scenario: Debounced search triggers after 300ms

- **GIVEN** a SearchablePageableSelectBox with the popup open
- **WHEN** the user types text in the TextBox
- **THEN** the system SHALL wait 300ms without additional input
- **AND** the system SHALL load page 1 with the typed searchText
- **AND** the popup SHALL remain open
- **AND** the results SHALL update in the list

#### Scenario: Typing cancels previous debounce

- **GIVEN** a SearchablePageableSelectBox with a debounce timer running
- **WHEN** the user types additional text
- **THEN** the system SHALL cancel the previous debounce timer
- **AND** the system SHALL start a new 300ms debounce timer with the updated searchText

### Requirement: Popup Closing and Reset

The control SHALL reset the TextBox to the selected item's display text when closed via Escape or clicking outside, discarding any user input.

#### Scenario: Reset on Escape key

- **GIVEN** a SearchablePageableSelectBox with a selected item named "Provider A"
- **WHEN** the user types "Provider B" in the TextBox
- **AND** the user presses Escape
- **THEN** the popup SHALL close
- **AND** the TextBox SHALL display "Provider A" (the selected item)
- **AND** the SelectedItem SHALL remain unchanged
- **AND** the TextBox SHALL have focus

#### Scenario: Reset on click outside

- **GIVEN** a SearchablePageableSelectBox with a selected item named "Provider A"
- **WHEN** the user types "Provider B" in the TextBox
- **AND** the user clicks outside the popup area
- **THEN** the popup SHALL close
- **AND** the TextBox SHALL display "Provider A" (the selected item)
- **AND** the SelectedItem SHALL remain unchanged
- **AND** the TextBox SHALL have focus

### Requirement: Selection and Popup Close

The control SHALL update the SelectedItem and close the popup when the user selects an item from the list or adds a new item.

#### Scenario: Select item from list

- **GIVEN** a SearchablePageableSelectBox with the popup open
- **WHEN** the user clicks an item in the list
- **THEN** the SelectedItem SHALL be set to the clicked item
- **AND** the popup SHALL close
- **AND** the TextBox SHALL display the new item's DisplayMemberPath value
- **AND** the TextBox SHALL have focus

#### Scenario: Select item via Enter key

- **GIVEN** a SearchablePageableSelectBox with the popup open
- **AND** an item in the list is highlighted
- **WHEN** the user presses Enter
- **THEN** the SelectedItem SHALL be set to the highlighted item
- **AND** the popup SHALL close
- **AND** the TextBox SHALL display the new item's DisplayMemberPath value
- **AND** the TextBox SHALL have focus

#### Scenario: Add new item

- **GIVEN** a SearchablePageableSelectBox with AddNewCommand configured
- **WHEN** the user triggers the Add New button
- **THEN** the AddNewCommand SHALL execute
- **AND** the popup SHALL close

### Requirement: Keyboard Navigation

The control SHALL support keyboard navigation within the popup.

#### Scenario: Navigate with Arrow Up

- **GIVEN** a SearchablePageableSelectBox with the popup open and multiple items in the list
- **WHEN** the user presses Arrow Up
- **THEN** the highlight SHALL move to the previous item in the list
- **AND** the list SHALL scroll if the previous item is not visible

#### Scenario: Navigate with Arrow Down

- **GIVEN** a SearchablePageableSelectBox with the popup open and multiple items in the list
- **WHEN** the user presses Arrow Down
- **THEN** the highlight SHALL move to the next item in the list
- **AND** the list SHALL scroll if the next item is not visible

#### Scenario: Escape key closes popup and resets

- **GIVEN** a SearchablePageableSelectBox with the popup open
- **AND** the TextBox contains user input that differs from the selected item
- **WHEN** the user presses Escape
- **THEN** the popup SHALL close
- **AND** the TextBox SHALL reset to the selected item's display text

### Requirement: Pagination

The control SHALL support pagination through the data source.

#### Scenario: Navigate to next page

- **GIVEN** a SearchablePageableSelectBox with the popup open and results spanning multiple pages
- **WHEN** the user clicks the Next Page button
- **THEN** the system SHALL load the next page with the current searchText
- **AND** the system SHALL include selectedIds in the request
- **AND** the list SHALL update with the new page of results

#### Scenario: Navigate to previous page

- **GIVEN** a SearchablePageableSelectBox with the popup open on page 2 or higher
- **WHEN** the user clicks the Previous Page button
- **THEN** the system SHALL load the previous page with the current searchText
- **AND** the system SHALL include selectedIds in the request
- **AND** the list SHALL update with the new page of results

#### Scenario: Pager disabled for single page

- **GIVEN** a SearchablePageableSelectBox with the popup open
- **AND** all results fit on a single page
- **WHEN** the user views the pager controls
- **THEN** the Next Page button SHALL be disabled
- **AND** the Previous Page button SHALL be disabled

### Requirement: Selected Item Guarantee

When selectedIds is provided and the searchText exactly matches the display name of a selected item, the service layer SHALL include that selected item in the results regardless of the filter criteria.

#### Scenario: Selected item included despite filter

- **GIVEN** a SearchablePageableSelectBox with a selected item named "Provider A" with ID 123
- **WHEN** the user types "Provider A" in the TextBox
- **AND** the system requests data with searchText="Provider A" and selectedIds=[123]
- **THEN** the service SHALL include the item with ID 123 in the results
- **AND** the service SHALL NOT filter out the item based on the searchText match
- **AND** the results SHALL contain the selected item

#### Scenario: Normal filtering for non-selected items

- **GIVEN** a SearchablePageableSelectBox with no selected items
- **WHEN** the user types "Provider X" in the TextBox
- **AND** the system requests data with searchText="Provider X" and selectedIds=null
- **THEN** the service SHALL apply normal filter criteria
- **AND** the service SHALL only return items matching "Provider X"

### Requirement: Control Properties

The control SHALL expose properties for configuration and data binding.

#### Scenario: Bind SelectedItem

- **GIVEN** a ViewModel with a `SelectedProvider` property
- **WHEN** the developer sets `SelectedItem="{Binding SelectedProvider}"`
- **THEN** the control SHALL display the `SelectedProvider` value
- **AND** changes to `SelectedProvider` SHALL update the control
- **AND** user selections in the control SHALL update `SelectedProvider`

#### Scenario: Configure DisplayMemberPath

- **GIVEN** a data object with a property `ProviderName`
- **WHEN** the developer sets `DisplayMemberPath="ProviderName"`
- **THEN** the control SHALL display the `ProviderName` value for each item

#### Scenario: Configure Watermark

- **GIVEN** a SearchablePageableSelectBox with no selected item
- **WHEN** the developer sets `Watermark="请选择供应商"`
- **THEN** the control SHALL display "请选择供应商" in the TextBox

#### Scenario: Configure PageSize

- **GIVEN** a SearchablePageableSelectBox
- **WHEN** the developer sets `PageSize="10"`
- **THEN** each data load request SHALL request 10 items per page

#### Scenario: Provide LoadPageAsync

- **GIVEN** a ViewModel with a `LoadPagedProvidersAsync` method
- **WHEN** the developer sets `LoadPageAsync="{Binding LoadPagedProvidersAsync}"`
- **THEN** the control SHALL call this method when loading data
- **AND** the method SHALL receive searchText, page, pageSize, selectedIds, and cancellationToken parameters

#### Scenario: Provide GetItemId

- **GIVEN** a data object with a property `ProviderId`
- **WHEN** the developer provides a `GetItemId` delegate that extracts the ID
- **THEN** the control SHALL use this delegate to populate the selectedIds parameter
- **AND** the selectedIds SHALL contain the ID of the currently selected item
