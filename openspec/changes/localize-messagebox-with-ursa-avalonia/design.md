## Context

MaterialClient uses `MessageBox.Avalonia` (namespace `MsBox.Avalonia`) for message dialogs across two ViewModel files. The project already depends on `Irihi.Ursa` and `Irihi.Ursa.Themes.Semi`, which provide a modern `MessageBox` component with built-in localization. The Ursa.Semi theme is already configured with `Locale="zh-CN"` in `App.axaml`, so Chinese button labels (OK/Yes/No/Cancel) will display automatically.

**Current state:**
- 2 source files use `MessageBoxManager.GetMessageBoxStandard()`: `AttendedWeighingViewModel.cs` and `AttendedWeighingDetailViewModelBase.cs`
- Helper methods `ShowMessageBoxAsync` and `ShowMessageBoxAsyncWithoutBlocking` in the base class wrap the old API
- `MessageBox.Avalonia` NuGet package is a direct dependency in `MaterialClient.csproj`
- 3 additional ViewModels call the helpers indirectly but require no code changes

**API mapping (old → new):**

| MsBox.Avalonia | Ursa.Avalonia |
|---|---|
| `MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum, Icon)` | `MessageBox.ShowAsync(owner, message, title, MessageBoxIcon, MessageBoxButton)` |
| `ButtonEnum.Ok` | `MessageBoxButton.OK` |
| `ButtonEnum.YesNo` | `MessageBoxButton.YesNo` |
| `Icon.None` | `MessageBoxIcon.None` |
| `Icon.Question` | `MessageBoxIcon.Question` |
| `.ShowWindowDialogAsync(window)` | `MessageBox.ShowAsync(window, ...)` (overload with owner) |
| `.ShowAsync()` | `MessageBox.ShowAsync(message, title, ...)` (overload without owner) |

## Goals / Non-Goals

**Goals:**
- Replace all `MessageBox.Avalonia` usage with `Ursa.Avalonia.Controls.MessageBox`
- Ensure Chinese localization for MessageBox button labels via existing Semi theme locale
- Remove the `MessageBox.Avalonia` NuGet package dependency
- Maintain identical UX behavior (blocking for confirmations, non-blocking for info messages)

**Non-Goals:**
- Changing the visual style or layout of message dialogs beyond what Ursa.Avalonia provides by default
- Adding new localization languages beyond the already-configured zh-CN
- Refactoring the helper method signatures or introducing a MessageBox service abstraction
- Modifying any View (.axaml) files

## Decisions

### D1: Direct API replacement over abstraction layer

**Decision:** Replace `MessageBoxManager.GetMessageBoxStandard()` calls directly with `MessageBox.ShowAsync()` rather than introducing an `IMessageBoxService` abstraction.

**Rationale:** The MessageBox API surface is minimal (2 files, 2 helpers). An abstraction would add complexity without clear benefit since there is only one consumer library. The Ursa.Avalonia MessageBox is already well-tested and stable. If future needs require mocking in tests, an abstraction can be introduced then.

### D2: Preserve helper method structure

**Decision:** Keep `ShowMessageBoxAsync` and `ShowMessageBoxAsyncWithoutBlocking` in `AttendedWeighingDetailViewModelBase` but update their internals to call `MessageBox.ShowAsync()`.

**Rationale:** These helpers encapsulate owner-window resolution (`GetParentWindow()`) and thread dispatch logic. Removing them would force all callers to handle these concerns. The helpers are used by 3+ subclasses — changing their signatures would be a larger refactor with no value.

### D3: Use owner-window overload for modal behavior

**Decision:** When `GetParentWindow()` returns a non-null window, use `MessageBox.ShowAsync(Window owner, string message, string title, ...)` to maintain modal (window-blocking) behavior. When null, use the parameterless-owner overload.

**Rationale:** The old code used `ShowWindowDialogAsync(parentWin)` for modal behavior. Ursa.Avalonia's owner-based overload provides the same modal semantics. The fallback to non-modal display when no parent is found preserves existing behavior.

### D4: Leverage existing Semi theme locale

**Decision:** No additional locale configuration needed. The `zh-CN` locale is already set in `App.axaml` via `<semi:SemiTheme Locale="zh-CN" />` and `<u-semi:SemiTheme Locale="zh-CN" />`.

**Rationale:** Ursa.Avalonia MessageBox reads the Semi theme's locale for button text labels. The configuration is already in place.

## Risks / Trade-offs

- **[Risk] MessageBox visual difference** → Ursa.Avalonia MessageBox has a different visual style compared to MessageBox.Avalonia. Users may notice the change. Mitigation: The new style is more modern and consistent with the rest of the Ursa-based UI.

- **[Risk] Return type difference** → MsBox returns `ButtonResult` (Ok, Yes, No, Cancel, None); Ursa returns `MessageBoxResult` (OK, Yes, No, Cancel, None). The mapping is straightforward but callers checking results must use the new enum. Mitigation: Only 2 call sites check results (logout and abolish confirmations), both use Yes/No pattern which maps directly.

- **[Risk] Transitive dependency removal** → Removing `MessageBox.Avalonia` may break other packages that depend on it transitively. Mitigation: Only this project references it directly; grep confirms no other package in .csproj depends on it.
