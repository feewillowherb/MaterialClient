## 1. Update AttendedWeighingDetailViewModelBase

- [ ] 1.1 Replace `using MsBox.Avalonia` and `using MsBox.Avalonia.Enums` with `using Ursa.Avalonia.Controls` in `AttendedWeighingDetailViewModelBase.cs`
- [ ] 1.2 Rewrite `ShowMessageBoxAsync` helper: replace `MessageBoxManager.GetMessageBoxStandard("提示", message, ButtonEnum.Ok, Icon.None)` + `ShowWindowDialogAsync`/`ShowAsync` with `MessageBox.ShowAsync(parentWin, message, "提示", MessageBoxIcon.None, MessageBoxButton.OK)` (owner overload when parentWin is non-null) or `MessageBox.ShowAsync(message, "提示", MessageBoxIcon.None, MessageBoxButton.OK)` (no-owner fallback)
- [ ] 1.3 Rewrite `ShowMessageBoxAsyncWithoutBlocking` helper with the same Ursa.Avalonia MessageBox API replacement as task 1.2
- [ ] 1.4 Replace the 3 direct `MessageBoxManager.GetMessageBoxStandard` calls (lines ~213, ~240, ~536) with `MessageBox.ShowAsync` using appropriate owner/title/icon/button parameters

## 2. Update AttendedWeighingViewModel

- [ ] 2.1 Replace `using MsBox.Avalonia` and `using MsBox.Avalonia.Enums` with `using Ursa.Avalonia.Controls` in `AttendedWeighingViewModel.cs`
- [ ] 2.2 Replace the logout confirmation call (~line 2246): use `MessageBox.ShowAsync(parentWin, "确定要退出登录吗？", "确认退出登录", MessageBoxIcon.Question, MessageBoxButton.YesNo)` and check result against `MessageBoxResult.Yes`
- [ ] 2.3 Replace the info message call (~line 2541): use `MessageBox.ShowAsync(message, "提示", MessageBoxIcon.None, MessageBoxButton.OK)` with appropriate owner handling
- [ ] 2.4 Update the local `GetParentWindow()` method's MessageBox call site to use the new API

## 3. Clean up dependencies

- [ ] 3.1 Remove `<PackageReference Include="MessageBox.Avalonia" />` from `MaterialClient.csproj`
- [ ] 3.2 Verify no remaining references to `MsBox.Avalonia` or `MessageBoxManager` across the codebase via grep

## 4. Build verification

- [ ] 4.1 Run `dotnet build` to confirm the project compiles without errors after migration
