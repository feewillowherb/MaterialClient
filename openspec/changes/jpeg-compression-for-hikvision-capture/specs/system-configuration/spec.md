## ADDED Requirements

### Requirement: JPEG quality configuration property

The system SHALL provide a `JpegQuality` integer property in `SystemSettings` with a default value of 75. This property controls the target JPEG compression quality for Hikvision camera captures.

#### Scenario: Default value
- **WHEN** a new `SystemSettings` instance is created without explicit `JpegQuality` assignment
- **THEN** `JpegQuality` SHALL be 75

#### Scenario: Persisted value
- **WHEN** the user sets `JpegQuality` to a value between 1 and 100 and saves settings
- **THEN** the system SHALL persist the value via `ISettingsService` and restore it on next application launch

### Requirement: JPEG quality UI control in settings window

The system SHALL display a Slider control in the Settings window under the camera settings section (below the stream type selector) that allows the user to adjust the JPEG compression quality.

#### Scenario: Slider range and step
- **WHEN** the Settings window is displayed
- **THEN** the JPEG quality Slider SHALL have a minimum of 1, maximum of 100, and step of 5

#### Scenario: Slider binds to view model
- **WHEN** the user adjusts the JPEG quality Slider
- **THEN** the `JpegQuality` reactive property in `SettingsWindowViewModel` SHALL update immediately via ReactiveUI binding

#### Scenario: Current value display
- **WHEN** the Settings window is displayed
- **THEN** a TextBlock SHALL display the current `JpegQuality` value next to the Slider

#### Scenario: Save and load
- **WHEN** the user clicks save in the Settings window
- **THEN** `systemSettings.JpegQuality` SHALL be set from the ViewModel's `JpegQuality` property
- **WHEN** settings are loaded
- **THEN** the ViewModel's `JpegQuality` SHALL be set from `settings.SystemSettings.JpegQuality`
