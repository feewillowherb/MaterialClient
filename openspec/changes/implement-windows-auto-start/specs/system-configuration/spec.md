# system-configuration Specification

## ADDED Requirements

### Requirement: Windows Auto-Start Configuration

The system SHALL provide functionality to enable or disable automatic application startup on Windows boot, with synchronization between database settings and Windows registry.

#### Scenario: Enable auto-start from settings
- **WHEN** user enables "开机自动启动" checkbox in SettingsWindow
- **AND** user clicks "保存" (Save) button
- **THEN** the system SHALL:
  - Save `EnableAutoStart = true` to database
  - Create registry entry in `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
  - Registry value name: Application name (e.g., "MaterialClient")
  - Registry value data: Full path to executable
  - Log successful registry operation

#### Scenario: Disable auto-start from settings
- **WHEN** user disables "开机自动启动" checkbox in SettingsWindow
- **AND** user clicks "保存" (Save) button
- **THEN** the system SHALL:
  - Save `EnableAutoStart = false` to database
  - Remove registry entry from `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
  - Log successful registry operation

#### Scenario: Check auto-start status
- **WHEN** system needs to verify current auto-start state
- **THEN** the system SHALL:
  - Read registry entry from `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
  - Return `true` if entry exists and matches executable path
  - Return `false` if entry does not exist or path mismatch

### Requirement: Settings-Registry Synchronization

The system SHALL maintain consistency between database settings and Windows registry state, automatically repairing inconsistencies when detected.

#### Scenario: Synchronize on settings save
- **WHEN** settings are saved via `SettingsService.SaveSettingsAsync()`
- **THEN** the system SHALL:
  - Save settings to database first
  - If `EnableAutoStart = true`, call `WindowsAutoStartService.EnableAutoStartAsync()`
  - If `EnableAutoStart = false`, call `WindowsAutoStartService.DisableAutoStartAsync()`
  - Ensure database and registry states match after save operation

#### Scenario: Repair inconsistency on startup
- **WHEN** application starts up
- **AND** database setting `EnableAutoStart` does not match registry state
- **THEN** the system SHALL:
  - Detect inconsistency by comparing database setting with registry state
  - Apply database setting to registry (repair inconsistency)
  - Log repair operation for troubleshooting
  - Continue application startup normally (do not block on sync failure)

#### Scenario: Consistent state on startup
- **WHEN** application starts up
- **AND** database setting `EnableAutoStart` matches registry state
- **THEN** the system SHALL:
  - Log that state is consistent
  - Continue startup without registry modifications

### Requirement: Error Handling for Registry Operations

The system SHALL handle registry operation failures gracefully without blocking application functionality.

#### Scenario: Registry permission denied
- **WHEN** registry write operation fails due to insufficient permissions
- **THEN** the system SHALL:
  - Catch `UnauthorizedAccessException` or `SecurityException`
  - Log warning message with exception details
  - Continue application flow without throwing exception
  - Allow settings save to complete (database update succeeds even if registry fails)

#### Scenario: Registry unavailable
- **WHEN** registry is unavailable or corrupted
- **THEN** the system SHALL:
  - Catch registry-related exceptions (`IOException`, `ArgumentException`, etc.)
  - Log error message with exception details
  - Continue application flow without throwing exception
  - Allow application to start and function normally

#### Scenario: Registry read failure
- **WHEN** reading registry entry fails during status check
- **THEN** the system SHALL:
  - Catch exception and log warning
  - Return `false` as conservative default (assume auto-start disabled)
  - Continue application flow without throwing exception

### Requirement: Windows Auto-Start Service Interface

The system SHALL provide `IWindowsAutoStartService` interface for managing Windows auto-start functionality.

#### Scenario: Service registration
- **WHEN** application initializes dependency injection container
- **THEN** the system SHALL:
  - Register `WindowsAutoStartService` as implementation of `IWindowsAutoStartService`
  - Make service available for injection into other services

#### Scenario: Service methods
- **WHEN** `IWindowsAutoStartService` is used
- **THEN** the system SHALL provide:
  - `Task EnableAutoStartAsync()` - Enable auto-start in registry
  - `Task DisableAutoStartAsync()` - Disable auto-start in registry
  - `Task<bool> IsAutoStartEnabledAsync()` - Check current registry state
  - All methods SHALL be async and return appropriate types
  - All methods SHALL handle exceptions internally (don't throw to caller)