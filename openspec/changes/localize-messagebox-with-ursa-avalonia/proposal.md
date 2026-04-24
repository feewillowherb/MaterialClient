## Why

MaterialClient currently uses `MessageBox.Avalonia` (MsBox.Avalonia) for all message dialogs, while Ursa.Avalonia is already a project dependency and provides its own modern MessageBox component. This creates UI inconsistency and lacks Chinese localization for button labels (OK/Yes/No/Cancel are shown in English). Migrating to Ursa.Avalonia MessageBox unifies the component library and leverages its built-in zh-CN locale support.

## What Changes

- **BREAKING**: Replace all `MessageBoxManager.GetMessageBoxStandard()` calls with `MessageBox.ShowAsync()` from `Ursa.Avalonia.Controls`
- **BREAKING**: Replace `MsBox.Avalonia.Enums.ButtonEnum` with `Ursa.Avalonia.Controls.MessageBoxButton`
- **BREAKING**: Replace `MsBox.Avalonia.Enums.Icon` with `Ursa.Avalonia.Controls.MessageBoxIcon`
- Remove `MessageBox.Avalonia` NuGet package dependency
- Remove `using MsBox.Avalonia` and `using MsBox.Avalonia.Enums` imports
- Update `ShowMessageBoxAsync` and `ShowMessageBoxAsyncWithoutBlocking` helper methods in `AttendedWeighingDetailViewModelBase` to use Ursa.Avalonia API
- Update direct `MessageBoxManager` calls in `AttendedWeighingViewModel` to use Ursa.Avalonia API
- Configure Ursa.Avalonia Semi theme locale to `zh-CN` for Chinese button label localization

## Capabilities

### New Capabilities

- `ursa-messagebox-migration`: Migration from MessageBox.Avalonia to Ursa.Avalonia MessageBox across all ViewModels, including API adaptation and Chinese locale configuration

### Modified Capabilities

_None — this change is purely an internal API swap; no spec-level behavioral requirements change._

## Impact

- **Affected files** (2 source files):
  - `MaterialClient/ViewModels/AttendedWeighingViewModel.cs` — 2 direct `GetMessageBoxStandard` calls, `MsBox.Avalonia` imports
  - `MaterialClient/ViewModels/AttendedWeighingDetailViewModelBase.cs` — 3 direct `GetMessageBoxStandard` calls, `ShowMessageBoxAsync` / `ShowMessageBoxAsyncWithoutBlocking` helpers, `MsBox.Avalonia` imports
- **Indirect callers** (use helpers, no code changes needed):
  - `StandardWeighingDetailViewModel.cs`
  - `SolidWasteWeighingDetailViewModel.cs`
  - `AttendedWeighingViewModel.cs` (via helpers)
- **Dependencies**: Remove `MessageBox.Avalonia` NuGet package; `Irihi.Ursa` and `Irihi.Ursa.Themes.Semi` already referenced
- **Locale config**: UrsaSemiTheme `Locale` property set to `zh-CN` in App.axaml or theme initialization
