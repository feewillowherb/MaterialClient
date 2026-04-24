## 1. Update AttendedWeighingDetailViewModelBase

- [x] 1.1 Replace `using MsBox.Avalonia` and `using MsBox.Avalonia.Enums` with `using Ursa.Controls` in `AttendedWeighingDetailViewModelBase.cs`
- [x] 1.2 Rewrite `ShowMessageBoxAsync` helper: replace `MessageBoxManager.GetMessageBoxStandard("提示", message, ButtonEnum.Ok, Icon.None)` + `ShowWindowDialogAsync`/`ShowAsync` with `MessageBox.ShowAsync(parentWin, message, "提示", MessageBoxIcon.None, MessageBoxButton.OK)` (owner overload when parentWin is non-null) or `MessageBox.ShowAsync(message, "提示", MessageBoxIcon.None, MessageBoxButton.OK)` (no-owner fallback)
- [x] 1.3 Rewrite `ShowMessageBoxAsyncWithoutBlocking` helper with the same Ursa MessageBox API replacement as task 1.2
- [x] 1.4 Replace the abolish order confirmation call (~line 536): use `MessageBox.ShowAsync(parentWin, "确定要废除此单吗？", "确认废单", MessageBoxIcon.Question, MessageBoxButton.YesNo)` and check result against `MessageBoxResult.Yes`

## 2. Update AttendedWeighingViewModel

- [x] 2.1 Replace `using MsBox.Avalonia` and `using MsBox.Avalonia.Enums` with `using Ursa.Controls` in `AttendedWeighingViewModel.cs`
- [x] 2.2 Rewrite the local `ShowMessageBoxAsync` helper (~line 2535): replace `MessageBoxManager.GetMessageBoxStandard(...)` + `ShowWindowDialogAsync`/`ShowAsync` with `MessageBox.ShowAsync(parentWin, message, "提示", MessageBoxIcon.None, MessageBoxButton.OK)` or no-owner fallback
- [x] 2.3 Replace the logout confirmation call (~line 2246): use `MessageBox.ShowAsync(parentWin, "确定要退出登录吗？", "确认退出登录", MessageBoxIcon.Question, MessageBoxButton.YesNo)` and check result against `MessageBoxResult.Yes`

## 3. Clean up dependencies

- [x] 3.1 Remove `<PackageReference Include="MessageBox.Avalonia" />` from `MaterialClient.csproj`
- [x] 3.2 Verify no remaining references to `MsBox.Avalonia` or `MessageBoxManager` across the codebase via grep

## 4. Build verification

- [x] 4.1 Run `dotnet build` to confirm the project compiles without errors after migration
