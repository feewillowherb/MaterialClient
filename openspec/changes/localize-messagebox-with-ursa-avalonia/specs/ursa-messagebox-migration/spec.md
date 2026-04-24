## ADDED Requirements

### Requirement: All message boxes SHALL use Ursa.Avalonia MessageBox
The system SHALL use `Ursa.Avalonia.Controls.MessageBox.ShowAsync()` for all message dialog displays. No code SHALL reference `MsBox.Avalonia` or `MessageBoxManager`.

#### Scenario: Info message displays via Ursa MessageBox with Chinese labels
- **WHEN** `ShowMessageBoxAsync("some message")` is called from any ViewModel inheriting `AttendedWeighingDetailViewModelBase`
- **THEN** the system SHALL display a message box using `MessageBox.ShowAsync()` with title "提示", icon `MessageBoxIcon.None`, and button `MessageBoxButton.OK`
- **THEN** the OK button SHALL display Chinese label "确定" via the Semi theme zh-CN locale

#### Scenario: Info message with owner window shows as modal
- **WHEN** `ShowMessageBoxAsync("some message")` is called and `GetParentWindow()` returns a non-null window
- **THEN** the system SHALL call `MessageBox.ShowAsync(parentWin, message, "提示", MessageBoxIcon.None, MessageBoxButton.OK)`
- **THEN** the message box SHALL block the parent window (modal behavior)

#### Scenario: Info message without owner window shows non-modal
- **WHEN** `ShowMessageBoxAsync("some message")` is called and `GetParentWindow()` returns null
- **THEN** the system SHALL call `MessageBox.ShowAsync(message, "提示", MessageBoxIcon.None, MessageBoxButton.OK)` without owner parameter

#### Scenario: Non-blocking info message posts to UI thread
- **WHEN** `ShowMessageBoxAsyncWithoutBlocking("some message")` is called
- **THEN** the system SHALL post the message box display to the UI thread via `Dispatcher.UIThread.Post` without awaiting completion
- **THEN** the message box SHALL use the same `Ursa.Avalonia.Controls.MessageBox.ShowAsync()` API as the blocking variant

### Requirement: Confirmation dialogs SHALL preserve Yes/No modal behavior
Confirmation dialogs (logout, abolish order) SHALL use `MessageBoxButton.YesNo` and display modally when a parent window is available.

#### Scenario: Logout confirmation with Yes selected
- **WHEN** the user triggers logout from `AttendedWeighingViewModel`
- **THEN** the system SHALL display a modal message box with title "确认退出登录", message "确定要退出登录吗？", icon `MessageBoxIcon.Question`, and buttons `MessageBoxButton.YesNo`
- **THEN** the Yes button SHALL display Chinese label "是"
- **THEN** the No button SHALL display Chinese label "否"
- **WHEN** the user clicks "是" (Yes)
- **THEN** the system SHALL return `MessageBoxResult.Yes` and proceed with logout

#### Scenario: Logout confirmation with No selected
- **WHEN** the user triggers logout and the confirmation dialog is displayed
- **WHEN** the user clicks "否" (No)
- **THEN** the system SHALL return `MessageBoxResult.No` and cancel the logout operation

#### Scenario: Abolish order confirmation
- **WHEN** the user triggers abolish order from `AttendedWeighingDetailViewModelBase`
- **THEN** the system SHALL display a modal message box with title "确认废单", message "确定要废除此单吗？", icon `MessageBoxIcon.Question`, and buttons `MessageBoxButton.YesNo`
- **WHEN** the user clicks "是" (Yes)
- **THEN** the system SHALL return `MessageBoxResult.Yes` and proceed with abolish
- **WHEN** the user clicks "否" (No)
- **THEN** the system SHALL return `MessageBoxResult.No` and cancel the abolish operation

### Requirement: MessageBox.Avalonia package SHALL be removed
The `MessageBox.Avalonia` NuGet package SHALL be removed from the project dependencies.

#### Scenario: Package reference removed
- **WHEN** the migration is complete
- **THEN** `MaterialClient.csproj` SHALL NOT contain a `PackageReference` to `MessageBox.Avalonia`
- **THEN** no source file SHALL contain `using MsBox.Avalonia` or `using MsBox.Avalonia.Enums`

### Requirement: Button labels SHALL be displayed in Chinese
All MessageBox button labels SHALL be displayed in Chinese via the Ursa.Avalonia Semi theme locale configuration.

#### Scenario: Chinese locale is active via Semi theme
- **WHEN** the application starts with `SemiTheme Locale="zh-CN"` (already configured in App.axaml)
- **THEN** all MessageBox buttons SHALL display Chinese labels per the following mapping:

| Button | English | Chinese |
|---|---|---|
| OK | OK | 确定 |
| Cancel | Cancel | 取消 |
| Yes | Yes | 是 |
| No | No | 否 |
