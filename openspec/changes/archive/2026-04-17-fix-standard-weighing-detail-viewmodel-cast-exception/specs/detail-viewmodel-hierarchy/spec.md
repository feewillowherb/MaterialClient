## MODIFIED Requirements

### Requirement: View DataType compatibility
All views that bind to the detail ViewModel SHALL use `ContentControl` with `DataTemplate` type selection in `AttendedWeighingDetailView.axaml`. Each sub-view (`StandardModeFormView`, `SolidWasteModeFormView`) SHALL retain its own `x:DataType` declaration matching its corresponding ViewModel subclass. The `ContentControl` SHALL use `Content="{Binding}"` to pass the current DataContext, and `DataTemplate.DataType` SHALL match the concrete ViewModel type to ensure only the correct sub-view is instantiated.

#### Scenario: StandardModeFormView resolves Standard subclass bindings
- **WHEN** `AttendedWeighingDetailView` is rendered with a `StandardWeighingDetailViewModel` DataContext
- **THEN** only `StandardModeFormView` is instantiated via `DataTemplate` type matching, and all compiled bindings to `StandardWeighingDetailViewModel` properties (including `IsMaterialPopupOpen`, `MaterialsSelectionPopupViewModel`, `OpenMaterialSelectionCommand`) resolve correctly

#### Scenario: SolidWasteModeFormView resolves SolidWaste subclass bindings
- **WHEN** `AttendedWeighingDetailView` is rendered with a `SolidWasteWeighingDetailViewModel` DataContext
- **THEN** only `SolidWasteModeFormView` is instantiated via `DataTemplate` type matching, and all compiled bindings to `SolidWasteWeighingDetailViewModel` properties resolve correctly

#### Scenario: No cast exception in SolidWaste mode
- **WHEN** the DataContext is `SolidWasteWeighingDetailViewModel`
- **THEN** `StandardModeFormView` is NOT instantiated in the visual tree, and no `InvalidCastException` occurs

#### Scenario: No cast exception in Standard mode
- **WHEN** the DataContext is `StandardWeighingDetailViewModel`
- **THEN** `SolidWasteModeFormView` is NOT instantiated in the visual tree, and no binding errors occur

## ADDED Requirements

### Requirement: DataTemplate view selection in AttendedWeighingDetailView
`AttendedWeighingDetailView.axaml` SHALL use a `ContentControl` with typed `DataTemplate` entries instead of a `Panel` with visibility toggling. The `ContentControl` SHALL define exactly two `DataTemplate` entries: one for `vm:StandardWeighingDetailViewModel` and one for `vm:SolidWasteWeighingDetailViewModel`.

#### Scenario: ContentControl selects correct DataTemplate for Standard mode
- **WHEN** `DetailViewModel` is a `StandardWeighingDetailViewModel` instance
- **THEN** the `ContentControl` selects the `DataTemplate` with `DataType="vm:StandardWeighingDetailViewModel"` and renders `StandardModeFormView`

#### Scenario: ContentControl selects correct DataTemplate for SolidWaste mode
- **WHEN** `DetailViewModel` is a `SolidWasteWeighingDetailViewModel` instance
- **THEN** the `ContentControl` selects the `DataTemplate` with `DataType="vm:SolidWasteWeighingDetailViewModel"` and renders `SolidWasteModeFormView`

#### Scenario: Only one sub-view exists in visual tree at a time
- **WHEN** `AttendedWeighingDetailView` is displayed
- **THEN** exactly one sub-view (either `StandardModeFormView` or `SolidWasteModeFormView`) exists in the visual tree, not both
