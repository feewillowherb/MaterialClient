## MODIFIED Requirements

### Requirement: View DataType compatibility
All views that bind to the detail ViewModel SHALL use `x:DataType="vm:AttendedWeighingDetailViewModelBase"` for compiled bindings in shared popup/container scope. Views that are strictly mode-specific MAY use their concrete subclass DataType only inside the mode-specific view boundary.

#### Scenario: Shared popup bindings compile against base type
- **WHEN** `AttendedWeighingDetailPopup` (and any shared child controls) is compiled with Avalonia compiled bindings
- **THEN** shared bindings resolve against `AttendedWeighingDetailViewModelBase` and do not require casting to a concrete mode subclass

#### Scenario: Standard mode concrete bindings stay in standard view boundary
- **WHEN** standard-only properties are bound in `StandardModeFormView`
- **THEN** those bindings are compiled and resolved within standard view scope without affecting shared popup binding contract

#### Scenario: SolidWaste mode concrete bindings stay in solid-waste view boundary
- **WHEN** solid-waste-only properties are bound in `SolidWasteModeFormView`
- **THEN** those bindings are compiled and resolved within solid-waste view scope without affecting shared popup binding contract

#### Scenario: SolidWaste popup open does not throw cast exception
- **WHEN** detail popup is opened with `SolidWasteWeighingDetailViewModel` as DataContext
- **THEN** no `InvalidCastException` is thrown from compiled binding accessors and popup renders normally
