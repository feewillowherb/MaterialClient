## ADDED Requirements

### Requirement: Logout button visible in AttendedWeighingWindow top menu bar
The system SHALL display a "退出登录" (Logout) button in the `AttendedWeighingWindow` top menu bar, positioned next to the "数据同步" button. The button SHALL use the same `transparent-button` style as other menu items.

#### Scenario: Logout button is visible
- **WHEN** the user is on the `AttendedWeighingWindow`
- **THEN** a "退出登录" button SHALL be visible in the top menu bar, positioned after the "数据同步" button

### Requirement: Logout confirmation dialog
The system SHALL show a confirmation dialog before executing the logout action. The dialog SHALL ask the user to confirm the logout intent.

#### Scenario: User cancels logout confirmation
- **WHEN** user clicks the "退出登录" button
- **AND** the confirmation dialog is displayed
- **AND** user selects "Cancel"
- **THEN** no logout action SHALL be performed
- **AND** the `AttendedWeighingWindow` SHALL remain open

#### Scenario: User confirms logout
- **WHEN** user clicks the "退出登录" button
- **AND** the confirmation dialog is displayed
- **AND** user selects "Confirm"
- **THEN** the system SHALL proceed with the logout flow

### Requirement: Session cleared on logout
The system SHALL clear the user session from the local database when logout is confirmed. The system SHALL also clear any saved credentials (remember-me data).

#### Scenario: Successful session and credential clearance
- **WHEN** user confirms logout
- **THEN** the system SHALL call `IAuthenticationService.LogoutAsync()` to delete the `UserSession`
- **AND** the system SHALL call `IAuthenticationService.ClearSavedCredentialAsync()` to delete the `UserCredential`
- **AND** license data (`LicenseInfo`) SHALL NOT be affected

### Requirement: Navigate to LoginWindow after logout
The system SHALL navigate the user to the `LoginWindow` after a successful logout. The `AttendedWeighingWindow` SHALL be hidden and the `LoginWindow` SHALL be shown with a clean (reset) login form.

#### Scenario: Window transition after logout
- **WHEN** logout is successfully completed
- **THEN** the `AttendedWeighingWindow` SHALL be hidden
- **AND** the `LoginWindow` SHALL be shown
- **AND** the login form SHALL have empty username and password fields

### Requirement: LogoutRequestedMessage event
The system SHALL publish a `LogoutRequestedMessage` marker class via `MessageBus` after the logout auth cleanup is complete, so that other components (e.g., background sync services) can react to the logout event.

#### Scenario: MessageBus event propagation
- **WHEN** `AttendedWeighingViewModel` completes the logout (session cleared, credentials cleared)
- **THEN** it SHALL publish a `LogoutRequestedMessage` via `MessageBus.Current.SendMessage()`

#### Scenario: MessageBus subscription lifecycle
- **WHEN** any component subscribes to `LogoutRequestedMessage`
- **THEN** the subscription SHALL be managed via `CompositeDisposable` + `DisposeWith()`
- **AND** the subscription SHALL be disposed when the subscribing component is disposed
