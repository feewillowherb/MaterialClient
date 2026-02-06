# Evaluation: Merge Search Box and Trigger Button in GenericSelectionPopup

## 1. Scope

- **Views**: `GenericSelectionPopup.axaml`, `SolidWasteModeFormView.axaml`, `StandardModeFormView.axaml`
- **Proposal**: Merge the search box (inside the popup) and the trigger Button content (on the form) into a single, unified control.
- **Context**: Today the user flow is: (1) Form shows a Button with the current selection (e.g. material name); (2) Click opens a Popup containing a separate search TextBox and a DataGrid. The proposal is to combine “where you type to search” and “what is selected” into one place.

---

## 2. Current State

### 2.1 Form (trigger)

- **Control**: A `Button` with `Content="{Binding SelectedSolidWasteMaterial.Name}"` (or `SelectedProvider.ProviderName`, `SelectedStreet`).
- **Role**: Displays the current selection; click opens the popup.
- **Placement**: Popup uses `PlacementTarget = button` and `Placement="Bottom"` (set in code-behind on click).

### 2.2 Popup content (GenericSelectionPopup)

- **Row 0**: Dedicated search area — `TextBox` with watermark “输入关键字查找”, bound to `SearchText`.
- **Row 1**: DataGrid of `PagedItems` (e.g. name column).
- **Row 2**: Pagination and page info.

So the user sees: **Button (selection)** → click → **Popup (search box + list + paging)**. Search and selection display are in two separate places.

---

## 3. Goal of Merging

- **Single control**: One field that both shows the current selection and accepts search input (e.g. ComboBox/Autocomplete style).
- **Benefits**: Fewer visual elements, clearer mental model (“one box to choose and search”), possible reduction in popup height by removing the separate search row, and alignment with common “searchable dropdown” patterns.

---

## 4. Options

### 4.1 Option A: Trigger as combined search + selection (recommended direction)

**Idea**: Replace the Button with a single control that:
- Shows the selected item’s display text when closed (or a placeholder when none selected).
- On focus/click, becomes an input: user types to search; list opens below (same DataGrid + paging).
- Selection updates the displayed text and closes the list (or keeps it open depending on UX).

**Implementation**: Use or build an **AutocompleteBox**-like control:
- Display area bound to the same VM property as current Button (e.g. `SelectedSolidWasteMaterial.Name`).
- Same `SearchText` and `PagedItems` as today; popup content is only the list + pagination (no separate search row).
- Optional: allow “click to open list without typing” (show first page by default).

**Pros**
- One control for both “see selection” and “search”; no duplicate search box in popup.
- Matches common UX (searchable combo).
- Popup can be smaller (no 50px search row).

**Cons**
- Need to handle “show list on focus without typing” and “clear/placeholder” when no selection.
- Avalonia’s built-in `AutoCompleteBox` may need adaptation for async paging and custom VM binding; custom control is an option.

---

### 4.2 Option B: Keep Button + Popup, move search into trigger row only

**Idea**: Keep a popup with list + paging, but the **trigger** is a TextBox (styled like current button) that both shows the selection and is the search field. Popup has no internal search box; all typing happens in that trigger TextBox.

**Implementation**:
- Form: Replace `Button` with `TextBox` bound to `SearchText` (and display selected item’s name when not focused, or use a separate display + overlay).
- Popup: Remove the search `Border`/`TextBox` from `GenericSelectionPopup`; only DataGrid + pagination.
- When popup opens, focus the trigger TextBox so user can type immediately; `SearchText` already drives filtering.

**Pros**
- Small change to popup (delete search row); reuse existing `SearchText` and filtering.
- Single place to type.

**Cons**
- Distinguishing “showing selection” vs “editing search” in the same TextBox needs clear UX (e.g. focus vs blur, placeholder). Risk of users thinking they are “editing” the selected value instead of searching.

---

### 4.3 Option C: Merge only inside the popup (one row: selection + search)

**Idea**: Keep the Button on the form as-is. Inside the popup, merge the top row so it shows both “current selection” and “search” (e.g. left: selected name or “请选择”, right: search TextBox).

**Implementation**:
- `GenericSelectionPopup`: One row with e.g. `TextBlock` (bound to selected item) + `TextBox` (search), or a single `TextBox` that shows selection when not focused and search when focused.

**Pros**
- Form unchanged; only popup layout changes.
- Selection and search visible in one row inside the popup.

**Cons**
- Does not merge with the “Button content” on the form; the main ask (one control for both search and selection) is only partially met. Button and popup still feel like two separate areas.

---

## 5. Comparison

| Aspect              | Option A (Trigger = search + selection) | Option B (Trigger = TextBox, no popup search) | Option C (Popup row merge only) |
|---------------------|------------------------------------------|-----------------------------------------------|----------------------------------|
| Single control      | Yes                                      | Yes (TextBox as trigger)                      | No (Button + popup)              |
| Popup has no search | Yes (search in trigger)                  | Yes                                            | No (search in popup row)         |
| Form change         | Button → Autocomplete-like               | Button → TextBox                               | None                             |
| UX clarity          | High (standard pattern)                  | Medium (selection vs search in one box)       | Low (no merge with form button)  |
| Implementation      | New or custom control                    | Small (popup + binding)                        | Layout only in popup             |

---

## 6. Recommendation

- **Preferred**: **Option A** — one control that acts as both selection display and search (AutocompleteBox-style), with popup containing only the list and pagination. This best matches “merge search box and Button content” and improves UX.
- **Fallback**: **Option B** if we want minimal change and accept that the trigger is a TextBox that doubles as search; then add clear placeholder/watermark and focus behavior so “selection” vs “search” is obvious.
- **Not recommended**: **Option C** alone, as it does not unify the form Button with the search experience.

---

## 7. Implementation Notes (Option A)

1. **Control choice**
   - Evaluate Avalonia `AutoCompleteBox` (or community package) for `ItemsSource`, `SelectedItem`, and async filtering/paging.
   - If not suitable, implement a small custom control: content = TextBox + Popup (list + pagination), display value from VM, `Text` bound to `SearchText` when popup is open.

2. **ViewModel**
   - `GenericSelectionPopupViewModel` already exposes `SearchText`, `SelectedItem`, `PagedItems`, pagination; the new control would bind to the same VM. Optional: add a “display text” property for the closed state (e.g. selected item’s name or empty).

3. **Popup content**
   - Remove the search `Border` (row 0) from `GenericSelectionPopup.axaml`; keep DataGrid and pagination. Popup opens on focus/click of the trigger; search runs from the trigger’s text.

4. **Call sites**
   - Replace the three Buttons in `SolidWasteModeFormView.axaml` (材料名称, 供应商, 所属镇街) and the material Button in `StandardModeFormView.axaml` with the new control, keeping the same Popup/VM and `PlacementTarget` (or equivalent) so the list still opens below the trigger.

5. **Accessibility**
   - Ensure the combined control has a single focusable element and that screen readers announce it as a searchable list/combobox, not a plain text field.

---

## 8. Summary

| Item        | Description                                                                 |
|------------|-----------------------------------------------------------------------------|
| **Scope**  | GenericSelectionPopup + trigger Buttons in SolidWasteModeFormView, StandardModeFormView |
| **Goal**   | One control that shows selection and allows search, instead of Button + separate search box in popup |
| **Preferred** | Option A: Autocomplete-style trigger; popup = list + pagination only       |
| **Next steps** | Prototype Option A (control + popup layout), then migrate one form (e.g. 材料名称) and validate before rolling out. |
