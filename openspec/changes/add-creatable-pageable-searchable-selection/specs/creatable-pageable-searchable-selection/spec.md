# creatable-pageable-searchable-selection

**能力**：可创建、可分页、可搜索的单一选择控件。

---

## ADDED Requirements

### Requirement: Single control with embedded popup and data contract

The system SHALL provide one TemplatedControl that embeds a TextBox (single input/display surface) and a Popup (list, pager, optional "add new" area). The control SHALL connect to callers via LoadPageAsync, SelectedItem, DisplayMemberPath, GetItemId, Watermark, PageSize, and SHALL NOT depend on a specific ViewModel type. Data loading SHALL follow the contract (searchText, page, pageSize, selectedIds, ct) => Task<PagedResultDto<T>>.

#### Scenario: Open with selected item

- **WHEN** the user clicks the control and there is a current SelectedItem
- **THEN** the popup opens, _searchText is set to the selected item's display text, and the first page is loaded with selectedIds (and that searchText); the list SHALL include the selected item

#### Scenario: Open with no selection

- **WHEN** the user clicks the control and there is no SelectedItem
- **THEN** the popup opens, _searchText is empty, selectedIds is null, and the first page is loaded

#### Scenario: Close without selecting resets display

- **WHEN** the user has typed in the TextBox and then closes the popup via Escape or click-outside without selecting
- **THEN** _searchText and the TextBox display SHALL be reset to the current selected item's display text (or empty if none); user input SHALL NOT be retained

### Requirement: Searchable and pageable behavior

The system SHALL support search by user input in the TextBox with debounce (e.g. 300 ms), and SHALL load data with (searchText, page, pageSize, selectedIds). The popup SHALL support paging (e.g. pager or "load more").

#### Scenario: Type to search

- **WHEN** the user types in the TextBox while the popup is open
- **THEN** after debounce the system SHALL request the first page with the new search text and keep the popup open

#### Scenario: Paging

- **WHEN** the user triggers a page change (pager or load more)
- **THEN** the system SHALL load the new page with the current search text and selectedIds

### Requirement: SelectedItem as SelectionItem and extension methods

The system SHALL use a single model for the selected value: SelectionItem with Id and Name. The UI and callers SHALL interact only with SelectionItem. Business entities (e.g. Provider, Material, Street) SHALL be converted via FromX/ToX extension methods (e.g. Provider.ToSelectionItem(), SelectionItem.ToProviderId()).

#### Scenario: Binding to Provider

- **WHEN** the control is bound to a provider list and the user selects an item
- **THEN** SelectedItem is a SelectionItem; the ViewModel SHALL be able to convert to/from Provider (or provider id) via extension methods

### Requirement: Service layer selectedIds and filter bypass

Services that provide paged data (e.g. GetPagedProvidersAsync) SHALL accept an optional IReadOnlyList<int>? selectedIds. When any item in selectedIds has its Name equal to searchText (exact match), the service SHALL ignore the searchText filter so that the selected item is still returned.

#### Scenario: Open with selected item name as searchText

- **WHEN** the popup is opened with a selected item and the TextBox shows that item's name (used as searchText for the first load)
- **THEN** the backend SHALL not filter by that searchText when the selected item's name equals searchText, so the list SHALL include the selected item

### Requirement: Visual consistency when closed

When the popup is closed, the control SHALL match the current SearchableSelectionBox appearance: Height 32, background #FFFFFF, border #E5E7EB, BorderThickness 1, horizontal padding ~6,0,6,0; left content single-line text, font size 12, foreground #333333, vertical center, TextTrimming CharacterEllipsis; right side dropdown arrow ~10×6, color #666666. Hover/Focus/Error SHALL reuse or lightly adapt existing style resources.

#### Scenario: Closed state appearance

- **WHEN** the popup is closed
- **THEN** the control SHALL match the above dimensions, colors, and layout so that it is visually consistent with SearchableSelectionBox

### Requirement: Creatable when no results

When there are no matching results, the system SHALL show an empty state and SHALL provide an "add new" entry (e.g. button or command), consistent with existing GenericSelectionPopup "add new" behavior.

#### Scenario: Add new when no results

- **WHEN** the user has searched and the result set is empty
- **THEN** the user SHALL see an empty state and an option to add new (AddNewCommand or equivalent); invoking it SHALL behave like the existing add-new flow where applicable

### Requirement: Keyboard and selection behavior

The system SHALL support Arrow Up/Down in the popup to move highlight, Enter to confirm the current item, and Escape to close and reset. Selecting an item or executing "add new" SHALL update SelectedItem, close the popup, and return focus to the TextBox.

#### Scenario: Select item and close

- **WHEN** the user selects an item from the list (click or Enter)
- **THEN** SelectedItem is updated, the popup closes, and focus returns to the TextBox

#### Scenario: Escape closes and resets

- **WHEN** the user presses Escape while the popup is open
- **THEN** the popup closes and _searchText/TextBox display SHALL reset to the current selected item (or empty)
