## ADDED Requirements

### Requirement: All message boxes SHALL use Ursa.Avalonia MessageBox
The system SHALL use `Ursa.Avalonia.Controls.MessageBox.ShowAsync()` for all message dialog displays. No code SHALL reference `MsBox.Avalonia` or `MessageBoxManager`.

#### Scenario: Info message displays via Ursa MessageBox
- **WHEN** `ShowMessageBoxAsync("some message")` is called from any ViewModel inheriting `AttendedWeighingDetailViewModelBase`
- **THEN** the system SHALL display a message box using `MessageBox.ShowAsync()` with title "提示", icon `MessageBoxIcon.None`, and button `MessageBoxButton.OK`
- **THEN** the message box SHALL display Chinese button labels (e.g., "确定" for OK) via the Semi theme zh-CN locale

#### Scenario: Non-blocking info message displays via Ursa MessageBox
- **WHEN** `ShowMessageBoxAsyncWithoutBlocking("some message")` is called
- **THEN** the system SHALL post the message box display to the UI thread without awaiting completion
- **THEN** the message box SHALL use the same Ursa.Avalonia MessageBox API as the blocking variant

### Requirement: Confirmation dialogs SHALL preserve Yes/No modal behavior
Confirmation dialogs (logout, abolish order) SHALL use `MessageBoxButton.YesNo` and display modally when a parent window is available.

#### Scenario: Logout confirmation with Yes selected
- **WHEN** the user triggers logout confirmation
- **THEN** the system SHALL display a message box with title "确认退出登录", message "确定要退出登录吗？", icon `MessageBoxIcon.Question`, and buttons `MessageBoxButton.YesNo`
- **WHEN** the user clicks "是" (Yes)
- **THEN** the system SHALL return `MessageBoxResult.Yes` and proceed with logout

#### Scenario: Abolish order confirmation with No selected
- **WHEN** the user triggers abolish order confirmation
- **THEN** the system SHALL display a message box with title "确认废单", message "确定要废除此单吗？", icon `MessageBoxIcon.Question`, and buttons `MessageBoxButton.YesNo`
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

#### Scenario: Chinese locale is active
- **WHEN** the application starts with `SemiTheme Locale="zh-CN"` (already configured in App.axaml)
- **THEN** all MessageBox buttons SHALL display Chinese labels: "确定" (OK), "取消" (Cancel), "是" (Yes), "否" (No)
