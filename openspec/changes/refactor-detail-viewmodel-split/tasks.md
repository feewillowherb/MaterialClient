## 1. Extract MaterialItemRow

- [x] 1.1 Create `ViewModels/MaterialItemRow.cs` with the `MaterialItemRow` class extracted from `AttendedWeighingDetailViewModel.cs`
- [x] 1.2 Remove the inner `MaterialItemRow` class from `AttendedWeighingDetailViewModel.cs` and verify the project compiles

## 2. Create Base Class

- [x] 2.1 Create `ViewModels/AttendedWeighingDetailViewModelBase.cs` as an abstract class inheriting `ViewModelBase`
- [x] 2.2 Move shared `[Reactive]` properties to the base class: `AllWeight`, `TruckWeight`, `GoodsWeight`, `PlateNumber`, `Remark`, `JoinTime`, `OutTime`, `Operator`, `WeighingRecordId`, `SelectedDeliveryType`, `DeliveryTypeOptions`, `IsWeighingRecord`, `IsMatchButtonVisible`, `IsCompleteButtonVisible`, `PlateNumberError`, `MaterialItems`, `ProviderLabelText`, `DeliveryTypeTitleText`, `CompleteButtonText`, `DeliveryTypeDisplayText`
- [x] 2.3 Move shared fields to base class: `_listItem`, `_capturedBillPhotoPath`, DI service fields (`IServiceProvider`, `IMaterialService`, `IProviderService`, `IRepository<WeighingRecord, long>`)
- [x] 2.4 Move shared events to base class: `SaveCompleted`, `AbolishCompleted`, `CloseRequested`, `MatchCompleted`, `CompleteCompleted`, `ManualMatchSaveCompleted`
- [x] 2.5 Move shared commands to base class: `AbolishAsync`, `Close`, `MatchAsync`
- [x] 2.6 Move shared helper methods to base class: `ShowMessageBoxAsync`, `ShowMessageBoxAsyncWithoutBlocking`, `GetParentWindow`, `OnSaveCompletedAsync`
- [x] 2.7 Add `abstract bool IsSolidWasteMode { get; }` property to base class
- [x] 2.8 Add abstract methods: `protected abstract Task SaveModeSpecificAsync()`, `protected abstract Task CompleteModeSpecificAsync()`
- [x] 2.9 Add virtual method: `protected virtual Task LoadModeSpecificDataAsync() => Task.CompletedTask`
- [x] 2.10 Implement `InitializeData` as shared init + `Dispatcher.UIThread.Post(LoadDropdownDataAsync)` where `LoadDropdownDataAsync` calls shared loading then `LoadModeSpecificDataAsync`
- [x] 2.11 Move `LoadProvidersAsync`, `LoadMaterialsAsync`, `LoadMaterialUnitsForRowAsync` to base class as shared methods
- [x] 2.12 Implement `SaveAsync` as template method: validate plate, `SaveModeSpecificAsync()`, `OnSaveCompletedAsync()`
- [x] 2.13 Implement `CompleteAsync` as template method: validate plate, `CompleteModeSpecificAsync()`, shared tail

## 3. Create StandardWeighingDetailViewModel

- [x] 3.1 Create `ViewModels/StandardWeighingDetailViewModel.cs` inheriting `AttendedWeighingDetailViewModelBase`, implementing `ITransientDependency`
- [x] 3.2 Set `IsSolidWasteMode => false`
- [x] 3.3 Move Standard-specific `[Reactive]` properties: `Providers`, `SelectedProvider`, `Materials`, `SelectedProviderId`, `MaterialsSelectionPopupViewModel`
- [x] 3.4 Move Standard-specific constructor subscriptions (provider/material change handlers)
- [x] 3.5 Override `LoadModeSpecificDataAsync` to implement recommendation system logic and MaterialItemRow initialization
- [x] 3.6 Implement `SaveModeSpecificAsync` with `UpdateListItemAsync` logic
- [x] 3.7 Implement `CompleteModeSpecificAsync` with Standard mode validation (supplier/material/unit/quantity) and complete logic
- [x] 3.8 Move Standard-specific commands: `AddMaterialAsync`, `SelectMaterialAsync`, `OpenMaterialSelectionPopupAsync`

## 4. Create SolidWasteWeighingDetailViewModel

- [x] 4.1 Create `ViewModels/SolidWasteWeighingDetailViewModel.cs` inheriting `AttendedWeighingDetailViewModelBase`, implementing `ITransientDependency`
- [x] 4.2 Set `IsSolidWasteMode => true`
- [x] 4.3 Move SolidWaste-specific `[Reactive]` properties: `SolidWasteOrderNumber`, `Streets`, `SelectedStreet`, `SolidWasteTypes`, `SelectedSolidWasteType`, `SolidWasteMaterials`, `SelectedSolidWasteMaterial`, `SelectedProviderItem`, `SelectedMaterialItem`, `SelectedStreetItem`
- [x] 4.4 Move SolidWaste-specific delegate properties: `ProviderLoadPageAsync`, `MaterialLoadPageAsync`, `StreetLoadPageAsync`, `ProviderCreateNewAsync`, `MaterialCreateNewAsync`
- [x] 4.5 Move SolidWaste-specific constructor subscriptions (auto-unit on material select, auto order number on weight change)
- [x] 4.6 Override `LoadModeSpecificDataAsync` with `LoadSolidWasteDataAsync` logic (ExtraProperties reading)
- [x] 4.7 Move `LoadStreetsPageAsync`, configuration loading helpers
- [x] 4.8 Implement `SaveModeSpecificAsync` with `UpdateSolidWasteModeAsync` logic
- [x] 4.9 Implement `CompleteModeSpecificAsync` with SolidWaste validation (supplier/material/street/type/orderNumber) and complete logic
- [x] 4.10 Move SolidWaste-specific commands: `CreateNewProviderAsync`, `CreateNewMaterialAsync`

## 5. Update Parent ViewModel

- [x] 5.1 Change `DetailViewModel` property type from `AttendedWeighingDetailViewModel` to `AttendedWeighingDetailViewModelBase` in `AttendedWeighingViewModel.cs`
- [x] 5.2 Update `OpenDetail`/`OpenDetailAsync` to create `StandardWeighingDetailViewModel` or `SolidWasteWeighingDetailViewModel` based on `item.WeighingMode`

## 6. Update View Layer

- [x] 6.1 Update `AttendedWeighingDetailView.axaml` `x:DataType` to `vm:AttendedWeighingDetailViewModelBase`
- [x] 6.2 Update `AttendedWeighingDetailView.axaml.cs` to use base class type for `GetService` and `WireInteractions`
- [x] 6.3 Update `StandardModeFormView.axaml` `x:DataType` to `vm:StandardWeighingDetailViewModel`
- [x] 6.4 Update `SolidWasteModeFormView.axaml` `x:DataType` to `vm:SolidWasteWeighingDetailViewModel`

## 7. Cleanup and Verification

- [x] 7.1 Delete original `ViewModels/AttendedWeighingDetailViewModel.cs`
- [x] 7.2 Verify zero occurrences of `if.*IsSolidWasteMode` in all ViewModel files
- [x] 7.3 Build the project and fix any compilation errors
