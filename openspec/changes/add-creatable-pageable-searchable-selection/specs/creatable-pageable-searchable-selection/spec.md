# creatable-pageable-searchable-selection

**能力**：可创建、可分页、可搜索的单一选择控件。

---

## ADDED Requirements

### Requirement: Single control with embedded popup and data contract

The system SHALL provide one TemplatedControl that embeds a TextBox (single input/display surface) and a Popup (DataGrid, Ursa Pagination, optional "add new" area). The control SHALL connect to callers via LoadPageAsync, SelectedItem, Watermark, PageSize, CurrentPage, TotalCount, and SHALL NOT depend on a specific ViewModel type. Data loading SHALL follow the contract (searchText, page, pageSize, selectedIds, ct) => Task<PagedResultDto<T>>.

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

The system SHALL support search by user input in the TextBox with debounce (e.g. 300 ms), and SHALL load data with (searchText, page, pageSize, selectedIds). The popup SHALL provide proper pagination via Ursa `Pagination` component with page info text ("当前页:X  共N条记录") and page navigation controls, matching the GenericSelectionPopup paging layout.

#### Scenario: Type to search

- **WHEN** the user types in the TextBox while the popup is open
- **THEN** after debounce the system SHALL request the first page with the new search text, reset CurrentPage to 1, and keep the popup open

#### Scenario: Paging via Pagination component

- **WHEN** the user clicks a page number or navigation button in the Ursa Pagination component
- **THEN** the system SHALL update CurrentPage (TwoWay bound), load the corresponding page with the current search text and selectedIds, and update CurrentPageInfo/TotalCountInfo

### Requirement: Popup visual consistency with GenericSelectionPopup

When the popup is open, its content SHALL visually match the existing GenericSelectionPopup layout: Border (Background White, BorderBrush #E5E7EB, BorderThickness 3, CornerRadius 4, Width 400, Height 250), containing a Grid with two rows: Row 0 for the DataGrid area (Height 200, single "名称" column with centered text, RowHeight 30, horizontal grid lines, white background column header with black foreground via local style override) and an overlapping empty-state/add-new panel; Row 1 for the pagination area (Height 50, page info text on the left, Ursa Pagination on the right).

#### Scenario: Popup matches GenericSelectionPopup

- **WHEN** the popup is open
- **THEN** the popup SHALL have the same dimensions (400×250), border style (3px #E5E7EB), DataGrid layout (single column, centered text, horizontal grid lines), and pagination bar (page info + Ursa Pagination) as GenericSelectionPopup

#### Scenario: DataGrid column header local style

- **WHEN** the popup DataGrid renders its column header
- **THEN** the column header SHALL have white background and black foreground (overriding the global blue header style), matching GenericSelectionPopup's local style override

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

When the popup is closed, the control SHALL match the current SearchableSelectionBox appearance: Height 32, background #FFFFFF, border #E5E7EB, BorderThickness 1, horizontal padding ~6,0,6,0; left content single-line text, font size 12, foreground #333333, vertical center, TextTrimming CharacterEllipsis; right side dropdown arrow ~10×6, color #666666. Hover/Focus/Error SHALL reuse or lightly adapt existing style resources. When the popup is closed, the TextBox SHALL be read-only; the user SHALL NOT be able to edit text while the popup is not visible.

#### Scenario: Closed state appearance

- **WHEN** the popup is closed
- **THEN** the control SHALL match the above dimensions, colors, and layout so that it is visually consistent with SearchableSelectionBox; the TextBox SHALL be read-only

### Requirement: Creatable when no results

When there are no matching results, the system SHALL show an empty state and SHALL provide an "add new" entry (e.g. button or command), consistent with existing GenericSelectionPopup "add new" behavior. The empty panel overlays the DataGrid area and is visible only when ShowAddNew is true.

#### Scenario: Add new when no results

- **WHEN** the user has searched and the result set is empty
- **THEN** the user SHALL see an empty state and an option to add new (AddNewCommand or equivalent); invoking it SHALL behave like the existing add-new flow where applicable

### Requirement: Keyboard and selection behavior

The system SHALL support Escape to close and reset. Selecting an item (click or Enter on DataGrid row) SHALL update SelectedItem and close the popup. The system SHALL NOT programmatically return focus to the TextBox after selection or close; focus SHALL be allowed to move naturally. DataGrid DoubleTapped SHALL also confirm selection.

#### Scenario: Select item and close

- **WHEN** the user selects an item from the DataGrid (click, Enter, or double-tap)
- **THEN** SelectedItem is updated and the popup closes; focus SHALL NOT be programmatically forced to the TextBox

#### Scenario: Escape closes and resets

- **WHEN** the user presses Escape while the popup is open
- **THEN** the popup closes and _searchText/TextBox display SHALL reset to the current selected item (or empty); focus SHALL NOT be programmatically forced to the TextBox

### Requirement: Pagination state properties

The control SHALL expose the following StyledProperties for template binding: `CurrentPage` (int, TwoWay with Pagination), `TotalCount` (int), `ShowResults` (bool, true when items exist), `CurrentPageInfo` (string, formatted as "当前页:X"), `TotalCountInfo` (string, formatted as "共N条记录"). These properties SHALL be updated after each data load via `UpdatePageInfo()`.

#### Scenario: Page info reflects loaded data

- **WHEN** data is loaded and TotalCount is 25
- **THEN** CurrentPageInfo SHALL be "当前页:1", TotalCountInfo SHALL be "共25条记录", TotalCount SHALL be 25, ShowResults SHALL be true

### Requirement: Popup open trigger — explicit user interaction only

The popup SHALL open ONLY in response to explicit user interaction: (1) mouse click on the control area, or (2) text input in the TextBox (handled by debounce). The popup SHALL NOT open due to programmatic focus, initial render focus, or GotFocus events. The control SHALL NOT use `OnGotFocus` as a popup trigger. The control's `Focusable` property SHALL be set so that initial rendering does not cause the popup to appear.

#### Scenario: Initial render does not open popup

- **WHEN** the `SolidWasteModeFormView` (or any parent view) is rendered for the first time
- **THEN** the popup SHALL NOT open; the TextBox SHALL display the watermark or the current selected item name in read-only mode

#### Scenario: Click opens popup

- **WHEN** the user clicks anywhere on the control (including the TextBox area or the dropdown arrow)
- **THEN** the popup opens, the TextBox becomes editable, and data is loaded

#### Scenario: Re-click after close reopens popup

- **WHEN** the user closes the popup (via Escape or selection) and then clicks the control again
- **THEN** the popup SHALL reopen; there SHALL NOT be a dead click where the TextBox is editable but the popup is not shown

### Requirement: Popup and TextBox editability synchronization

The popup open/close state and the TextBox editability SHALL always be in sync. When the popup is open, the TextBox SHALL be editable (IsReadOnly=false, Focusable=true, IsHitTestVisible=true). When the popup is closed, the TextBox SHALL be fully inert (IsReadOnly=true, Focusable=false, IsHitTestVisible=false) — behaving like a TextBlock that only displays text. This synchronization SHALL be driven by the `IsPopupOpen` property change handler.

#### Scenario: Popup open → TextBox fully interactive

- **WHEN** the popup transitions from closed to open
- **THEN** the TextBox SHALL become editable (IsReadOnly=false), focusable (Focusable=true), and hit-testable (IsHitTestVisible=true), and SHALL receive focus, allowing the user to type search text

#### Scenario: Popup close → TextBox fully inert

- **WHEN** the popup transitions from open to closed (via selection, Escape, or click-outside)
- **THEN** the TextBox SHALL become read-only (IsReadOnly=true), non-focusable (Focusable=false), and non-hit-testable (IsHitTestVisible=false), displaying the selected item name or empty text; focus SHALL be automatically released by the framework when Focusable is set to false

#### Scenario: No desynchronized state

- **GIVEN** any sequence of user interactions (clicks, typing, Escape, selection, re-clicks)
- **THEN** at every point in time, exactly one of these invariants holds: (a) popup is open AND TextBox is editable/focusable/hit-testable, or (b) popup is closed AND TextBox is read-only/non-focusable/non-hit-testable

### Requirement: Closed-state TextBox exits focus system

When the popup is closed, the inner TextBox SHALL NOT participate in the focus system. Specifically: (1) Tab navigation SHALL skip the TextBox (Focusable=false), (2) clicking the TextBox area SHALL pass through to the parent Border (IsHitTestVisible=false) triggering popup open, (3) the focus system SHALL NOT auto-focus the TextBox during initial rendering or layout. This ensures the closed control behaves visually like `SearchableSelectionBox`'s TextBlock display panel — a pure display surface with no caret, no focus ring, and no keyboard interaction.

#### Scenario: Tab navigation skips closed control

- **WHEN** the popup is closed and the user presses Tab to cycle focus
- **THEN** focus SHALL skip the TextBox inside the control; the TextBox SHALL NOT receive focus

#### Scenario: Click-outside releases focus

- **WHEN** the popup is closed (via Escape, selection, or click-outside) and the TextBox previously had focus
- **THEN** focus SHALL be released from the TextBox (due to Focusable=false); the control SHALL NOT retain a visible caret or focus ring

#### Scenario: Click on closed control opens popup via Border

- **WHEN** the popup is closed and the user clicks on the TextBox display area
- **THEN** the click SHALL pass through the TextBox (IsHitTestVisible=false) to the parent Border, which triggers OnRootPointerPressed → popup opens

### Requirement: Popup open via Dispatcher.Post to avoid LightDismiss race

The `OnRootPointerPressed` handler SHALL open the popup via `Dispatcher.UIThread.Post()` (deferred to the next dispatcher frame) rather than synchronously. This ensures the Popup's `IsLightDismissEnabled` mechanism does not treat the same click that opens the popup as an "outside click" that immediately closes it. Without this deferral, the Tunnel-phase open and the Bubble-phase LightDismiss would conflict within a single pointer event.

#### Scenario: Single click opens popup without immediate close

- **WHEN** the user clicks the control to open the popup
- **THEN** the popup SHALL open and remain open; the same click SHALL NOT trigger LightDismiss closure
