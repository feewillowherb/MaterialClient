# License Plate Recognition Specification Delta

**Capability**: `license-plate-recognition`
**Change ID**: `hikvision-lpr-integration`
**Type**: ADDED

---

## ADDED Requirements

### Requirement: Hikvision Device Configuration Fields

The system SHALL support Hikvision-specific configuration fields for license plate recognition devices.

#### Scenario: User adds Hikvision LPR device configuration
- **GIVEN** the system is configured with `LprDeviceType = Hikvision`
- **WHEN** user adds a new license plate recognition device in Settings window
- **THEN** the system SHALL:
  - Display Hikvision-specific configuration fields: UserName, Password, Port, Channel
  - Set default value "1" for Channel field
  - Display Channel field as read-only (disabled)
  - Allow user to input UserName, Password, and Port values

#### Scenario: User views existing Hikvision LPR configuration
- **GIVEN** an existing LPR configuration with Hikvision-specific fields populated
- **AND** `LprDeviceType = Hikvision`
- **WHEN** user opens Settings window
- **THEN** the system SHALL:
  - Display all Hikvision-specific fields with their saved values
  - Show UserName field with configured value
  - Show Password field with masked value (PasswordChar="●")
  - Show Port field with configured value
  - Show Channel field as read-only with value "1"

#### Scenario: User switches device type to LprAllInOne
- **GIVEN** current `LprDeviceType = Hikvision`
- **AND** Hikvision-specific fields are visible
- **WHEN** user changes `LprDeviceType` to `LprAllInOne`
- **THEN** the system SHALL:
  - Hide all Hikvision-specific fields (UserName, Password, Port, Channel)
  - Preserve Hikvision field values in memory (not lost)
  - Display only generic LPR fields (Name, Ip, Direction)
  - Update UI without requiring window restart

---

### Requirement: Dynamic Field Visibility Based on Device Type

The system SHALL dynamically show or hide Hikvision-specific configuration fields based on the selected `LprDeviceType` value.

#### Scenario: Hikvision fields visibility when device type is Hikvision
- **GIVEN** user is in Settings window
- **AND** `LprDeviceType = Hikvision`
- **THEN** the system SHALL show the following fields:
  - UserName (editable TextBox)
  - Password (editable TextBox with PasswordChar masking)
  - Port (editable TextBox)
  - Channel (read-only TextBox, fixed value "1")

#### Scenario: Hikvision fields visibility when device type is LprAllInOne
- **GIVEN** user is in Settings window
- **AND** `LprDeviceType = LprAllInOne`
- **THEN** the system SHALL NOT show:
  - UserName field
  - Password field
  - Port field
  - Channel field
- **AND** only display generic fields: Name, Ip, Direction

#### Scenario: Hikvision fields visibility when device type is Huaxiazhixin
- **GIVEN** user is in Settings window
- **AND** `LprDeviceType = Huaxiazhixin`
- **THEN** the system SHALL NOT show Hikvision-specific fields
- **AND** only display generic fields: Name, Ip, Direction
- **BECAUSE** Huaxiazhixin devices have different configuration requirements (to be implemented in future change)

---

### Requirement: JSON Configuration Persistence for Hikvision Fields

The system SHALL persist Hikvision-specific configuration fields to JSON storage and restore them correctly when loading settings, with backward compatibility for old data.

#### Scenario: Saving Hikvision LPR configuration to JSON
- **GIVEN** user has configured a Hikvision LPR device with:
  - Name: "hikvision_lpr_1"
  - Ip: "192.168.1.100"
  - Direction: "In"
  - UserName: "admin"
  - Password: "password123"
  - Port: "8000"
  - Channel: "1"
- **WHEN** user clicks Save button in Settings window
- **THEN** the system SHALL:
  - Serialize all fields to JSON in `SettingsEntity.LicensePlateRecognitionConfigsJson`
  - Include UserName as "admin"
  - Include Password as "password123"
  - Include Port as "8000"
  - Include Channel as "1"
  - Include generic fields (Name, Ip, Direction) as usual
  - Save JSON to SQLite database

#### Scenario: Loading Hikvision LPR configuration from JSON
- **GIVEN** database contains JSON with a Hikvision LPR configuration with all fields populated
- **WHEN** user opens Settings window
- **THEN** the system SHALL:
  - Deserialize JSON from `SettingsEntity.LicensePlateRecognitionConfigsJson`
  - Load and display all Hikvision-specific fields with correct values
  - Populate UserName field with saved value
  - Populate Password field with saved value (masked display)
  - Populate Port field with saved value
  - Populate Channel field with saved value ("1")
  - Populate generic fields (Name, Ip, Direction) as usual

#### Scenario: Loading old configuration JSON (backward compatibility)
- **GIVEN** database contains old JSON without Hikvision fields:
  ```json
  {
    "Name": "old_lpr_device",
    "Ip": "192.168.1.50",
    "Direction": 0
  }
  ```
- **WHEN** user opens Settings window
- **THEN** the system SHALL:
  - Successfully deserialize old JSON without throwing exceptions
  - Load existing fields (Name, Ip, Direction) correctly
  - Set new Hikvision fields to null (UserName, Password, Port, Channel)
  - Display Channel field with default value "1" when user switches to Hikview device type
  - Allow user to fill in Hikvision fields when needed
  - NOT require any manual data migration

#### Scenario: Mixed device types configuration persistence
- **GIVEN** user has configured multiple LPR devices:
  - Device A: Hikvision (with all Hikvision fields populated)
  - Device B: LprAllInOne (without Hikvision fields)
- **WHEN** user saves and reloads settings
- **THEN** the system SHALL:
  - Correctly serialize Device A with all Hikvision fields
  - Correctly serialize Device B without Hikvision fields (JSON omits null fields)
  - Deserialize both devices with their respective configurations intact
  - Display Device A with Hikvision fields when `LprDeviceType = Hikvision`
  - Display Device B without Hikvision fields when `LprDeviceType = LprAllInOne`

---

### Requirement: Hikvision LPR Service Interface Definition

The system SHALL define a service interface for Hikvision LPR device integration, establishing the contract for future implementation.

#### Scenario: Service interface is defined
- **GIVEN** the system needs to support Hikvision LPR devices
- **WHEN** the development team implements the configuration and UI
- **THEN** the system SHALL:
  - Define `IHikvisionLprService` interface in `MaterialClient.Common.Services.Hikvision` namespace
  - Declare method signatures for:
    - `Task<bool> ConnectAsync(LicensePlateRecognitionConfig config)`
    - `Task DisconnectAsync()`
    - `Task StartListeningAsync()`
    - `Task StopListeningAsync()`
  - Declare property `IObservable<LicensePlateRecognizedEvent> PlateRecognized { get; }`
  - Declare property `bool IsConnected { get; }`
  - Provide XML documentation comments for all members
  - NOT include any implementation code (implementation in separate proposal)

#### Scenario: Interface follows ReactiveUI patterns
- **GIVEN** the project uses ReactiveUI for reactive programming
- **WHEN** the interface is defined
- **THEN** the system SHALL:
  - Use `IObservable<T>` for event streams (PlateRecognized)
  - Use `Task` for asynchronous operations
  - Follow existing ReactiveUI patterns in the codebase
  - Ensure interface is compatible with dependency injection

**Note**: This requirement only establishes the interface definition. Actual implementation, HCNetSDK integration, and event stream logic are out of scope and will be covered in a separate proposal.

---

## REMOVED Requirements

None. No requirements are removed by this change.

---

## Architecture Notes

### Device Type Enumeration

The system uses the `LprDeviceType` enum to differentiate device types:
```csharp
public enum LprDeviceType
{
    Hikvision = 0,      // Supported in this change
    LprAllInOne = 1,    // Existing functionality
    Huaxiazhixin = 2    // Future support (not in this change)
}
```

### Conditional Field Display Logic

UI components use computed properties to determine field visibility:
```csharp
public bool ShowHikvisionLprFields => LprDeviceType == LprDeviceType.Hikvision;
```

This property is bound to `IsVisible` in XAML:
```xml
<StackPanel IsVisible="{Binding ShowHikvisionLprFields}">
    <!-- Hikvision-specific fields -->
</StackPanel>
```

### JSON Storage Architecture

**Storage Mechanism**:
- Configuration data is stored in `SettingsEntity.LicensePlateRecognitionConfigsJson` (NVARCHAR column)
- JSON format: `"[{"Name":"device1","Ip":"192.168.1.100","Direction":0,"UserName":"admin","Password":"123","Port":"8000","Channel":"1"}]`
- Uses `System.Text.Json` for serialization/deserialization

**Backward Compatibility**:
- Old JSON (v1.0): `{"Name":"device1","Ip":"192.168.1.100","Direction":0}`
- New JSON (v2.0): Includes additional Hikvision fields
- `System.Text.Json` automatically handles missing fields during deserialization (sets them to null)
- No database migration required (table structure unchanged)

**Default Value Handling**:
- `Channel` field: UI provides default value "1" when loaded data is null
- Other fields (UserName, Password, Port): Display as empty when null

### Service Interface Design

**Interface Definition**: `IHikvisionLprService`
- Namespace: `MaterialClient.Common.Services.Hikvision`
- Purpose: Define contract for Hikvision LPR device operations
- Pattern: Async/Task with ReactiveUI IObservable for events
- Status: Interface definition only, no implementation in this change

**Future Implementation** (separate proposal):
- HCNetSDK integration
- ReactiveUI event stream implementation
- Connection management and error handling
- Rx subscription lifecycle and memory leak prevention

### Backward Compatibility

- Existing `LprAllInOne` configurations remain fully functional
- New fields are nullable and do not affect validation logic
- `IsValid()` method only checks generic fields (Name, Ip)
- No database migration required (JSON storage)
- Old JSON files load successfully without errors

---

## Testing Requirements

### Unit Tests

- [ ] Test `ShowHikvisionLprFields` returns correct value for each device type
- [ ] Test `ShowHikvisionLprFields` updates when `LprDeviceType` changes
- [ ] Test `LicensePlateRecognitionConfig` Hikvision fields can be set and retrieved
- [ ] Test `IsValid()` does not require Hikvision fields
- [ ] Test `IHikvisionLprService` interface is correctly defined

### Integration Tests

- [ ] Test saving Hikvision configuration to JSON
- [ ] Test loading Hikvision configuration from JSON
- [ ] Test deserializing old JSON without Hikvision fields (backward compatibility)
- [ ] Test serializing and deserializing null fields
- [ ] Test mixed device types (Hikvision + LprAllInOne) save/load cycle

### UI Tests

- [ ] Manual test: Hikvision fields visibility when `LprDeviceType = Hikvision`
- [ ] Manual test: Hikvision fields hidden when `LprDeviceType = LprAllInOne`
- [ ] Manual test: Device type switch triggers UI update without restart
- [ ] Manual test: Add/Edit/Remove Hikvision LPR configuration
- [ ] Manual test: Loading old configuration data does not crash application

### JSON Serialization Tests

- [ ] Verify serialization includes all new Hikvision fields
- [ ] Verify deserialization of old JSON (missing fields) succeeds
- [ ] Verify deserialization sets missing fields to null
- [ ] Verify round-trip serialization (serialize → deserialize) preserves data
- [ ] Verify default value application for Channel field

---

## Dependencies

### Depends On

- `SystemSettings.LprDeviceType` property (already exists)
- `LprDeviceType` enum (already exists)
- `LicensePlateRecognitionConfig` entity (already exists)
- `SettingsEntity.LicensePlateRecognitionConfigsJson` property (already exists)
- `System.Text.Json` for serialization (already in use)
- ReactiveUI infrastructure (already in use)

### Updates

- `LicensePlateRecognitionConfig` class (adds 4 properties)
- `LicensePlateRecognitionConfigViewModel` class (adds 4 properties)
- `SettingsWindowViewModel` class (adds `ShowHikvisionLprFields` property)
- `AddLprDialogViewModel` class (adds constructor parameter and 4 properties)
- `SettingsWindow.axaml` (adds Hikvision field group)
- `AddLprDialog.axaml` (adds Hikvision field group)
- `IHikvisionLprService` interface (new file, definition only)

### No Changes Required

- Database schema (JSON storage, no table changes)
- Existing `LprAllInOne` configurations
- `CameraConfig` or other configuration classes

---

## Non-Goals

The following are explicitly OUT OF SCOPE for this change:

1. **Hikvision LPR Service Implementation**: The actual service that connects to Hikvision devices, listens for LPR events, and integrates with the attended weighing flow is NOT implemented in this change. This requires a separate proposal to define:
   - HCNetSDK LPR component integration
   - Event stream implementation using ReactiveUI
   - Connection management and error handling
   - Rx subscription lifecycle and memory leak prevention

2. **Huaxiazhixin Device Support**: The `Huaxiazhixin` device type exists in the enum but is not implemented in this change. Future proposal will define its specific configuration fields.

3. **Password Encryption**: Passwords are stored in plain text within JSON, consistent with existing `CameraConfig.Password` behavior. Future enhancement may add DPAPI encryption.

4. **Configuration Validation**: No additional validation is added for Hikvision fields (e.g., port number format, connection testing). This is deferred to future work.

5. **Configuration Import/Export**: No bulk configuration management features are added.

6. **Database Migration**: Not required because configuration is stored as JSON in existing column. Table structure remains unchanged.

---

## References

- `openspec/changes/hikvision-lpr-integration/proposal.md` - Main proposal document
- `openspec/changes/hikvision-lpr-integration/design.md` - Design documentation
- `openspec/changes/hikvision-lpr-integration/tasks.md` - Implementation tasks
- `openspec/specs/attended-weighing/spec.md` - Related weighing service specification
- `MaterialClient.Common/Configuration/LicensePlateRecognitionConfig.cs` - Configuration entity
- `MaterialClient.Common/Configuration/SystemSettings.cs` - System settings with LprDeviceType
- `MaterialClient.Common/Entities/SettingsEntity.cs` - JSON storage mechanism
