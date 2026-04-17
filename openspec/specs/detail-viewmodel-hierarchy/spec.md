# Detail ViewModel Hierarchy

## Purpose

Defines the inheritance hierarchy for attended weighing detail ViewModels, separating shared behavior into an abstract base class with mode-specific implementations for Standard and SolidWaste weighing modes.

## Requirements

### Requirement: ViewModel inheritance hierarchy
The system SHALL define an abstract base class `AttendedWeighingDetailViewModelBase` inheriting from `ViewModelBase`, with two concrete subclasses: `StandardWeighingDetailViewModel` and `SolidWasteWeighingDetailViewModel`.

#### Scenario: Base class instantiation blocked
- **WHEN** code attempts to instantiate `AttendedWeighingDetailViewModelBase` directly
- **THEN** compilation fails because the class is abstract

#### Scenario: Standard mode creates correct subclass
- **WHEN** `AttendedWeighingViewModel.OpenDetail` receives an item with `WeighingMode.Standard`
- **THEN** it creates an instance of `StandardWeighingDetailViewModel` via DI

#### Scenario: SolidWaste mode creates correct subclass
- **WHEN** `AttendedWeighingViewModel.OpenDetail` receives an item with `WeighingMode.SolidWaste`
- **THEN** it creates an instance of `SolidWasteWeighingDetailViewModel` via DI

### Requirement: Shared properties in base class
The base class SHALL expose all properties shared between modes: `AllWeight`, `TruckWeight`, `GoodsWeight`, `PlateNumber`, `Remark`, `JoinTime`, `OutTime`, `Operator`, `WeighingRecordId`, `SelectedDeliveryType`, `DeliveryTypeOptions`, `IsWeighingRecord`, `IsMatchButtonVisible`, `IsCompleteButtonVisible`, `PlateNumberError`, `MaterialItems`, `ProviderLabelText`, `DeliveryTypeTitleText`, `CompleteButtonText`, `DeliveryTypeDisplayText`.

#### Scenario: All shared properties accessible via base class
- **WHEN** a View binds to any shared property through `x:DataType="vm:AttendedWeighingDetailViewModelBase"`
- **THEN** the binding resolves correctly for both Standard and SolidWaste subclass instances

### Requirement: Mode-specific abstract property
The base class SHALL declare `abstract bool IsSolidWasteMode { get; }` to support View-layer mode switching.

#### Scenario: Standard subclass returns false
- **WHEN** `IsSolidWasteMode` is read on a `StandardWeighingDetailViewModel` instance
- **THEN** it returns `false`

#### Scenario: SolidWaste subclass returns true
- **WHEN** `IsSolidWasteMode` is read on a `SolidWasteWeighingDetailViewModel` instance
- **THEN** it returns `true`

### Requirement: Shared commands in base class
The base class SHALL provide shared commands: `AbolishAsync`, `Close`, `MatchAsync`. These commands SHALL work identically to the current implementation.

#### Scenario: Abolish command works for both modes
- **WHEN** user clicks "废单" button in either Standard or SolidWaste mode
- **THEN** the record is deleted and `AbolishCompleted` event fires with correct item info

#### Scenario: Close command works for both modes
- **WHEN** user clicks "下一个" button in either mode
- **THEN** `CloseRequested` event fires

### Requirement: Shared events in base class
The base class SHALL define events: `SaveCompleted`, `AbolishCompleted`, `CloseRequested`, `MatchCompleted`, `CompleteCompleted`, `ManualMatchSaveCompleted`. Event signatures SHALL remain unchanged.

#### Scenario: Parent ViewModel subscribes to base class events
- **WHEN** `AttendedWeighingViewModel` subscribes to events on a subclass instance
- **THEN** all event handlers receive `ItemOperationCompletedEventArgs` with correct data

### Requirement: Template method pattern for Save
The base class SHALL implement `SaveAsync` as a template method: validate plate number → call `SaveModeSpecificAsync()` (abstract) → call `OnSaveCompletedAsync()` (shared tail).

#### Scenario: Standard save flow
- **WHEN** user clicks save in Standard mode
- **THEN** base validates plate number, `StandardWeighingDetailViewModel.SaveModeSpecificAsync()` executes `UpdateListItemAsync`, then shared tail handles BillPhoto/events/notification

#### Scenario: SolidWaste save flow
- **WHEN** user clicks save in SolidWaste mode
- **THEN** base validates plate number, `SolidWasteWeighingDetailViewModel.SaveModeSpecificAsync()` executes `UpdateSolidWasteModeAsync`, then shared tail handles BillPhoto/events/notification

### Requirement: Template method pattern for Complete
The base class SHALL implement `CompleteAsync` as a template method: validate plate number → call `CompleteModeSpecificAsync()` (abstract) → handle BillPhoto → fire `CompleteCompleted` event.

#### Scenario: Standard complete flow
- **WHEN** user clicks complete in Standard mode
- **THEN** base validates plate number, `StandardWeighingDetailViewModel.CompleteModeSpecificAsync()` validates supplier/material/unit/quantity then calls Update+CompleteOrder, then shared tail fires event

#### Scenario: SolidWaste complete flow
- **WHEN** user clicks complete in SolidWaste mode
- **THEN** base validates plate number, `SolidWasteWeighingDetailViewModel.CompleteModeSpecificAsync()` validates supplier/material/street/type/orderNumber then calls Save+CompleteOrder, then shared tail fires event

### Requirement: InitializeData split
`InitializeData` SHALL be split into `InitializeCommonData` (base class, sets shared properties) + `LoadModeSpecificDataAsync` (virtual, overridden by subclasses).

#### Scenario: Standard mode loads recommendation and MaterialItemRows
- **WHEN** `StandardWeighingDetailViewModel.LoadModeSpecificDataAsync()` executes
- **THEN** it loads recommendation data by plate number and initializes each MaterialItemRow with material/units

#### Scenario: SolidWaste mode loads ExtraProperties
- **WHEN** `SolidWasteWeighingDetailViewModel.LoadModeSpecificDataAsync()` executes
- **THEN** it reads SolidWaste data from `WeighingRecord` or `Waybill` ExtraProperties and populates SolidWaste-specific fields

### Requirement: No IsSolidWasteMode if/else branching
After refactoring, the codebase SHALL NOT contain any `if (IsSolidWasteMode)` conditional branches in the ViewModel layer. All mode-specific behavior SHALL be determined by polymorphism.

#### Scenario: Codebase search finds no mode branching
- **WHEN** searching all ViewModel files for the pattern `if.*IsSolidWasteMode`
- **THEN** zero matches are found

### Requirement: View DataType compatibility
All detail sub-views SHALL be selected through `ContentControl` + typed `DataTemplate` in `AttendedWeighingDetailView.axaml`. `AttendedWeighingDetailView` SHALL bind `Content="{Binding}"`, and each sub-view SHALL keep its own concrete `x:DataType` matching the ViewModel subclass (`StandardWeighingDetailViewModel` / `SolidWasteWeighingDetailViewModel`).

#### Scenario: StandardModeFormView resolves Standard subclass bindings
- **WHEN** `AttendedWeighingDetailView` is rendered with a `StandardWeighingDetailViewModel` DataContext
- **THEN** only `StandardModeFormView` is instantiated by `DataTemplate` type matching, and compiled bindings to `StandardWeighingDetailViewModel` properties resolve correctly

#### Scenario: SolidWasteModeFormView resolves SolidWaste subclass bindings
- **WHEN** `AttendedWeighingDetailView` is rendered with a `SolidWasteWeighingDetailViewModel` DataContext
- **THEN** only `SolidWasteModeFormView` is instantiated by `DataTemplate` type matching, and compiled bindings to `SolidWasteWeighingDetailViewModel` properties resolve correctly

#### Scenario: No cast exception in SolidWaste mode
- **WHEN** the DataContext is `SolidWasteWeighingDetailViewModel`
- **THEN** `StandardModeFormView` is NOT instantiated in the visual tree, and no `InvalidCastException` occurs

#### Scenario: No cast exception in Standard mode
- **WHEN** the DataContext is `StandardWeighingDetailViewModel`
- **THEN** `SolidWasteModeFormView` is NOT instantiated in the visual tree, and no binding errors occur

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

### Requirement: MaterialItemRow independence
`MaterialItemRow` SHALL be defined in its own file (`ViewModels/MaterialItemRow.cs`) and remain shared by both modes via the base class `MaterialItems` collection.

#### Scenario: MaterialItemRow accessible from both subclasses
- **WHEN** either `StandardWeighingDetailViewModel` or `SolidWasteWeighingDetailViewModel` accesses `MaterialItems`
- **THEN** `MaterialItemRow` instances function identically to the current implementation

### Requirement: DI registration via ITransientDependency
Both subclasses SHALL implement `ITransientDependency` for automatic ABP DI registration.

#### Scenario: DI resolves Standard subclass
- **WHEN** `_serviceProvider.GetRequiredService<StandardWeighingDetailViewModel>()` is called
- **THEN** a new transient instance is returned with all dependencies injected

#### Scenario: DI resolves SolidWaste subclass
- **WHEN** `_serviceProvider.GetRequiredService<SolidWasteWeighingDetailViewModel>()` is called
- **THEN** a new transient instance is returned with all dependencies injected
