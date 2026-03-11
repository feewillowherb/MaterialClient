# creatable-pageable-searchable-selection

**能力**：可创建、可分页、可搜索的单一选择控件。

---

## ADDED Requirements

### Requirement: Single control with embedded popup and data contract

The system SHALL provide one TemplatedControl that embeds a TextBox (single input/display surface) and a Popup (DataGrid, Ursa Pagination, optional "add new" area). The control SHALL connect to callers via two pure functions (`LoadPageAsync`, `CreateNewAsync`) and one identity property (`SelectedId: int?`), plus configuration properties (`Watermark`, `PageSize`). The control SHALL NOT depend on a specific ViewModel type. Data loading SHALL follow the contract `(searchText, page, pageSize, selectedIds, ct) => Task<PagedResultDto<SelectionItem>?>`. The control SHALL internally manage `CurrentPage`, `TotalCount`, `ShowResults`, `CurrentPageInfo`, `TotalCountInfo`, `CurrentPageItems` and all SelectionItem state; these SHALL NOT need to be set by the caller.

#### Scenario: Open with selected item

- **WHEN** the user clicks the control and there is a current SelectedId (resolved to a display name internally)
- **THEN** the popup opens, _searchText is set to the selected item's display text, and the first page is loaded with selectedIds containing the current SelectedId; the list SHALL include the selected item

#### Scenario: Open with no selection

- **WHEN** the user clicks the control and SelectedId is null
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

### CHANGED Requirement: SelectedId (int?) as public selection API; SelectionItem is internal

The control's public selection state SHALL be expressed as `SelectedId` (int?, TwoWay). ViewModel SHALL bind directly to its domain Id property (e.g. `SelectedProviderId`). `SelectionItem` (Id + Name) SHALL be used internally by the control for DataGrid display and as the return type of `LoadPageAsync` / `CreateNewAsync`, but SHALL NOT be exposed as a public StyledProperty. The ViewModel SHALL NOT need to create, hold, or push `SelectionItem` objects. Extension methods (`ToSelectionItem()` etc.) SHALL remain for use inside `LoadPageAsync` / `CreateNewAsync` function implementations provided by the ViewModel.

#### Scenario: ViewModel binds SelectedId only

- **WHEN** the control is used for provider selection
- **THEN** the XAML binding SHALL be `SelectedId="{Binding SelectedProviderId, Mode=TwoWay}"`; the ViewModel SHALL NOT have a `SelectedProviderSelectionItem` property or any reactive bridge between domain entities and SelectionItem

#### Scenario: User selects a different item

- **WHEN** the user selects an item from the DataGrid
- **THEN** the control SHALL internally set `SelectedId = item.Id`; the TwoWay binding SHALL update `SelectedProviderId` in the ViewModel; no feedback loop SHALL occur because the ViewModel does not push SelectionItem back

#### Scenario: ViewModel sets SelectedId programmatically

- **WHEN** the ViewModel sets `SelectedProviderId` (e.g. loading an existing record)
- **THEN** the control SHALL receive the new `SelectedId` via binding, resolve the display name from `CurrentPageItems` or by triggering `LoadPageAsync(selectedIds: [id])`, and update the TextBox display accordingly

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

### CHANGED Requirement: Creatable when no results via CreateNewAsync function

When there are no matching results, the system SHALL show an empty state and SHALL provide an "add new" button. The creation logic SHALL be provided by the caller as a pure function `CreateNewAsync: Func<string, CancellationToken, Task<SelectionItem?>>?` (where the string parameter is the current search text, used as a name hint for the new item). The control SHALL internally handle all post-creation orchestration: (1) call `CreateNewAsync(searchText, ct)`, (2) if result is non-null, set `SelectedId = result.Id`, (3) refresh page data via `LoadPageAsync(selectedIds: [result.Id])`, (4) update display text to `result.Name`. The ViewModel SHALL NOT need to perform any post-creation selection or refresh logic.

#### Scenario: Add new when no results

- **WHEN** the user has searched and the result set is empty and `CreateNewAsync` is provided
- **THEN** the user SHALL see an empty state and an "add new" button; clicking it SHALL invoke `CreateNewAsync(searchText, ct)` provided by the caller

#### Scenario: Post-creation orchestration is internal to control

- **WHEN** `CreateNewAsync` returns a non-null `SelectionItem` (Id + Name)
- **THEN** the control SHALL set `SelectedId = result.Id`, refresh page data to include the new item, close the popup, and display the new item's name; the ViewModel SHALL only observe the `SelectedId` change via TwoWay binding

#### Scenario: Search text used as name hint

- **WHEN** the user types "ABC公司" in the search box, gets no results, and clicks "新增"
- **THEN** the control SHALL pass "ABC公司" as the first argument to `CreateNewAsync`, allowing the ViewModel to use it as the name for the new entity

### Requirement: Keyboard and selection behavior

The system SHALL support Escape to close and reset. Selecting an item (click or Enter on DataGrid row) SHALL update `SelectedId` to the item's Id and close the popup. The system SHALL NOT programmatically return focus to the TextBox after selection or close; focus SHALL be allowed to move naturally. DataGrid DoubleTapped SHALL also confirm selection.

#### Scenario: Select item and close

- **WHEN** the user selects an item from the DataGrid (click, Enter, or double-tap)
- **THEN** `SelectedId` is updated to `item.Id`, the popup closes, and the TextBox displays the item's name; focus SHALL NOT be programmatically forced to the TextBox

#### Scenario: Escape closes and resets

- **WHEN** the user presses Escape while the popup is open
- **THEN** the popup closes and _searchText/TextBox display SHALL reset to the current selected item's name (resolved from SelectedId, or empty if null); focus SHALL NOT be programmatically forced to the TextBox

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

### Requirement: Popup close cooldown to prevent immediate reopen

When the popup closes (via selection, Escape, or click-outside), the control SHALL suppress `OnRootPointerPressed` from reopening the popup during the same event cycle. A `_suppressNextOpen` flag SHALL be set on close and cleared via `Dispatcher.UIThread.Post` on the next frame. This prevents the scenario where Popup overlay removal causes Avalonia's pointer system to re-dispatch a PointerPressed event to the control area, immediately triggering a reopen.

#### Scenario: Select different item does not reopen popup

- **WHEN** the user selects a different item from the DataGrid and the popup closes
- **THEN** the popup SHALL NOT reopen; the cooldown SHALL suppress any stale PointerPressed events from the closing cycle

#### Scenario: Cooldown expires for next interaction

- **WHEN** the cooldown frame has passed (next Dispatcher frame after close)
- **THEN** `_suppressNextOpen` SHALL be false and the user's next click SHALL open the popup normally

### Requirement: Cancel debounce on popup close

When the popup closes, any pending debounce timer (`_debounceCts`) SHALL be cancelled immediately in `OnIsPopupOpenChanged(false)`. This prevents a scenario where: the user types in the search box (starting a 300ms debounce), then quickly selects an item (closing the popup); the debounce expires after close and its callback executes `if (!IsPopupOpen) IsPopupOpen = true`, causing the popup to reopen unexpectedly.

#### Scenario: Type then quickly select does not reopen

- **WHEN** the user types search text (debounce starts), then selects an item before the debounce expires
- **THEN** the debounce SHALL be cancelled on popup close; the popup SHALL NOT reopen after the debounce delay

### Requirement: Control owns all selection and pagination state

The control SHALL be the sole owner of: `CurrentPageItems` (ObservableCollection), pagination state (`CurrentPage`, `TotalCount`, `ShowResults`, `CurrentPageInfo`, `TotalCountInfo`), internal display text, and internal `SelectionItem` resolution. The ViewModel SHALL NOT hold parallel collections of page items, SHALL NOT hold `SelectionItem` properties, and SHALL NOT perform post-selection or post-creation orchestration. The ViewModel's responsibilities are limited to: (1) providing `LoadPageAsync` and `CreateNewAsync` function implementations, (2) binding `SelectedId` (int?) for reading/writing the selected entity's Id.

#### Scenario: Minimal ViewModel integration

- **GIVEN** a ViewModel with `SelectedProviderId` (int?), `LoadProvidersPageFunc` (Func), and `CreateProviderFunc` (Func)
- **WHEN** the control is configured via XAML bindings (`SelectedId`, `LoadPageAsync`, `CreateNewAsync`, `Watermark`, `PageSize`)
- **THEN** the ViewModel SHALL require zero reactive bridge subscriptions, zero SelectionItem properties, and zero post-selection/post-creation orchestration code

### Requirement: Clean up dead ProvidersPopupViewModel infrastructure

After migrating provider selection to `CreatablePageableSearchableSelectionBox`, the ViewModel SHALL NOT retain dead code from the old `GenericSelectionPopupViewModel<ProviderDto>` infrastructure. Specifically: `ProvidersPopupViewModel` property, `IsProvidersPopupOpen` property, the ~80 lines of initialization + WhenAnyValue subscription in `InitializeSolidWasteSelectionPopups()` for provider popup, and the `ProvidersPopupViewModel.SelectedItem` write-back in `InitializeData()` SHALL be removed. No AXAML view binds these properties; they execute at runtime but produce no visible effect.

#### Scenario: No dead provider popup code in ViewModel

- **WHEN** the ViewModel is inspected after migration
- **THEN** there SHALL be no `ProvidersPopupViewModel` property, no `IsProvidersPopupOpen` property, and no WhenAnyValue subscription for provider popup open/close/selection; only `LoadProvidersPageAsync` (Func) and `CreateProviderFunc` (Func) SHALL remain as provider-selection-related members
