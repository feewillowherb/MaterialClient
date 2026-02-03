---
name: Option B AuthCodeWindow DefaultWeighingMode
overview: Remove the DefaultWeighingMode bootstrap from MaterialClientModule and appsettings; add a default weighing mode selection (Standard / SolidWaste) in AuthCodeWindow and persist the user's choice to settings on successful verification.
todos: []
isProject: false
---

# Option B: AuthCodeWindow default weighing mode selection

## Goal

- Remove the temporary bootstrap that overwrites `SystemSettings.DefaultWeighingMode` from appsettings on every startup.
- Let the user choose the default weighing mode in the auth flow; persist that choice when verification succeeds.

## 1. Remove bootstrap and optional appsettings key

- **[MaterialClientModule.cs](MaterialClient/MaterialClientModule.cs)**  
Delete the entire block from the comment `// TEMP(2026-01-19): Bootstrap SystemSettings.DefaultWeighingMode...` through the end of the `catch` (lines 217–261). This includes the `try` that reads `configuration["SystemSettings:DefaultWeighingMode"]`, parses it, and overwrites settings, and the `catch` that logs and swallows errors.
- **[appsettings.json](MaterialClient/appsettings.json)** (optional)  
Remove the `SystemSettings` section (lines 47–49) or at least the `DefaultWeighingMode` key so no deployment default is read from config.

## 2. WeighingMode display in UI

- **New converter**  
Add a converter so the auth window can show friendly text for the enum (e.g. Standard → "标准", SolidWaste → "固废"). Options:
  - **A:** New file [MaterialClient/Converters/WeighingModeConverter.cs](MaterialClient/Converters/WeighingModeConverter.cs) (same pattern as [StreamTypeConverter.cs](MaterialClient/Converters/StreamTypeConverter.cs)): `Convert(WeighingMode)` returns the display string; no ConvertBack needed if ComboBox uses enum values directly.
  - **B:** Add `[Description("标准")]` / `[Description("固废")]` to [WeighingMode](MaterialClient.Common/Entities/Enums/WeighingMode.cs) and reuse a generic enum-to-description approach if one exists; otherwise a small converter is simpler.
- **App.axaml**  
Register the new converter in `Style.Resources` (e.g. `WeighingModeConverter`) so AuthCodeWindow can use it via `StaticResource`.

## 3. AuthCodeWindow UI

- **[AuthCodeWindow.axaml](MaterialClient/Views/AuthCodeWindow.axaml)**  
Add a row for default weighing mode, e.g. below the “授权码” row (current Grid.Row="0" input row), before the loading indicator:
  - Label: “默认称重模式”.
  - ComboBox: `ItemsSource` = list of the two enum values (Standard, SolidWaste), `SelectedItem` bound to a ViewModel property (e.g. `DefaultWeighingMode`), `ItemTemplate` or converter so each item shows “标准” / “固废”.
  - Adjust `Grid.RowDefinitions` so the new row has space (e.g. add an `Auto` row and shift existing rows). Keep “确定”/“重试” at the bottom.

## 4. AuthCodeWindowViewModel: property, options, and save on success

- **[AuthCodeWindowViewModel.cs](MaterialClient/ViewModels/AuthCodeWindowViewModel.cs)**  
  - Add `using MaterialClient.Common.Entities.Enums` and `MaterialClient.Common.Services` (for `ISettingsService`).
  - Inject `ISettingsService` in the constructor (in addition to `ILicenseService`).
  - Add `[Reactive] private WeighingMode _defaultWeighingMode = WeighingMode.Standard;`.
  - Expose a collection for the ComboBox: e.g. `public static IList<WeighingMode> DefaultWeighingModeOptions { get; } = new[] { WeighingMode.Standard, WeighingMode.SolidWaste };` (or an instance property if preferred).
  - **Optional:** On construction (or first load), load current settings and set `DefaultWeighingMode` from `settings.SystemSettings.DefaultWeighingMode` so the ComboBox shows the existing default when the window opens. This requires async load: either a small async init method called from the View (e.g. from code-behind when the window is shown) or fire-and-forget in the ViewModel that gets settings and sets the property on the UI thread.
  - In **VerifyAuthorizationCodeAsync**, after `await _licenseService.VerifyAuthorizationCodeAsync(AuthorizationCode)` succeeds (where you currently set `IsVerified = true` and status message):
    - Call `var settings = await _settingsService.GetSettingsAsync();`
    - Set `settings.SystemSettings.DefaultWeighingMode = DefaultWeighingMode` (the selected value from the ComboBox).
    - Call `await _settingsService.SaveSettingsAsync(settings);`
    - Then continue with existing success handling (IsVerified = true, etc.).

No change to the window-close or Hide() behavior: saving happens on “确定” + verification success, not on close.

## 5. Optional: only save when “never set”

- The evaluation doc suggests optionally writing only when the current default has “never been set.” One approach: before saving, if `settings.SystemSettings.DefaultWeighingMode` is already equal to the user’s selection, skip the write; or only call `SaveSettingsAsync` when the selected value differs from the current one. Another approach: introduce a “user has ever set default in auth” flag in settings and only write when that flag is false (then set it to true). For a minimal Option B, the plan assumes we **always persist the chosen value on verification success**; the “only when never set” logic can be added later if required.

## 6. Verification

- After implementation: start the app with a fresh or cleared settings DB; open AuthCodeWindow, select “固废”, enter a valid auth code, click “确定”. After success, confirm in DB or in-app that `SystemSettings.DefaultWeighingMode` is SolidWaste.
- Confirm that removing the bootstrap no longer overwrites the default on every startup; the only source of default is the auth window (and any existing code that reads the persisted value).

## Summary


| Step | Action                                                                                                                                                                                                    |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | Remove bootstrap block in MaterialClientModule.cs (lines 217–261).                                                                                                                                        |
| 2    | Optionally remove `SystemSettings.DefaultWeighingMode` from appsettings.json.                                                                                                                             |
| 3    | Add WeighingModeConverter (or use enum descriptions) and register in App.axaml.                                                                                                                           |
| 4    | Add “默认称重模式” row with ComboBox in AuthCodeWindow.axaml.                                                                                                                                                   |
| 5    | AuthCodeWindowViewModel: inject ISettingsService; add DefaultWeighingMode and options; on VerifyAuthorizationCodeAsync success, load settings, set DefaultWeighingMode from selection, SaveSettingsAsync. |
| 6    | Optionally pre-load current default into the ComboBox when the window opens (async).                                                                                                                      |


