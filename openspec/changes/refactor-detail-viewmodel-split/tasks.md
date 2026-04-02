## 1. Extract MaterialItemRow

- [ ] 1.1 Create `ViewModels/MaterialItemRow.cs` with the `MaterialItemRow` class extracted from `AttendedWeighingDetailViewModel.cs`
- [ ] 1.2 Remove the inner `MaterialItemRow` class from `AttendedWeighingDetailViewModel.cs` and verify the project compiles

## 2. Create Base Class

- [ ] 2.1 Create `ViewModels/AttendedWeighingDetailViewModelBase.cs` as an abstract class inheriting `ViewModelBase`
- [ ] 2.2 Move shared `[Reactive]` properties to the base class: `AllWeight`, `TruckWeight`, `GoodsWeight`, `PlateNumber`, `Remark`, `JoinTime`, `OutTime`, `Operator`, `WeighingRecordId`, `SelectedDeliveryType`, `DeliveryTypeOptions`, `IsWeighingRecord`, `IsMatchButtonVisible`, `IsCompleteButtonVisible`, `PlateNumberError`, `MaterialItems`, `ProviderLabelText`, `DeliveryTypeTitleText`, `CompleteButtonText`, `DeliveryTypeDisplayText`
- [ ] 2.3 Move shared fields to base class: `_listItem`, `_capturedBillPhotoPath`, DI service fields (`IServiceProvider`, `IMaterialService`, `IProviderService`, `IRepository<WeighingRecord, long>`)
- [ ] 2.4 Move shared events to base class: `SaveCompleted`, `AbolishCompleted`, `CloseRequested`, `MatchCompleted`, `CompleteCompleted`, `ManualMatchSaveCompleted`
- [ ] 2.5 Move shared commands to base class: `AbolishAsync`, `Close`, `MatchAsync`
- [ ] 2.6 Move shared helper methods to base class: `ShowMessageBoxAsync`, `ShowMessageBoxAsyncWithoutBlocking`, `GetParentWindow`, `OnSaveCompletedAsync`
- [ ] 2.7 Add `abstract bool IsSolidWasteMode { get; }` property to base class
- [ ] 2.8 Add abstract methods: `protected abstract Task SaveModeSpecificAsync()`, `protected abstract Task CompleteModeSpecificAsync()`
- [ ] 2.9 Add virtual method: `protected virtual Task LoadModeSpecificDataAsync() => Task.CompletedTask`
- [ ] 2.10 Implement `InitializeData` as `InitializeCommonData` + `Dispatcher.UIThread.Post(LoadDropdownDataAsync)` where `LoadDropdownDataAsync` calls `LoadSharedDropdownDataAsync` then `LoadModeSpecificDataAsync`
- [ ] 2.11 Move `LoadProvidersAsync`, `LoadMaterialsAsync`, `LoadMaterialUnitsForRowAsync` to base class as shared methods
- [ ] 2.12 Implement `SaveAsync` as template method: validate plate → `SaveModeSpecificAsync()` → `OnSaveCompletedAsync()`
- [ ] 2.13 Implement `CompleteAsync` as template method: validate plate → `CompleteModeSpecificAsync()` → shared tail

## 3. Create StandardWeighingDetailViewModel

- [ ] 3.1 Create `ViewModels/StandardWeighingDetailViewModel.cs` inheriting `AttendedWeighingDetailViewModelBase`, implementing `ITransientDependency`
- [ ] 3.2 Set `IsSolidWasteMode => false`
- [ ] 3.3 Move Standard-specific `[Reactive]` properties: `Providers`, `SelectedProvider`, `Materials`, `SelectedProviderId`, `MaterialsSelectionPopupViewModel`
- [ ] 3.4 Move Standard-specific constructor subscriptions (provider/material change handlers)
- [ ] 3.5 Override `LoadModeSpecificDataAsync` to implement recommendation system logic and MaterialItemRow initialization
- [ ] 3.6 Implement `SaveModeSpecificAsync` with `UpdateListItemAsync` logic
- [ ] 3.7 Implement `CompleteModeSpecificAsync` with Standard mode validation (supplier/material/unit/quantity) and complete logic
- [ ] 3.8 Move Standard-specific commands: `AddMaterialAsync`, `SelectMaterialAsync`, `OpenMaterialSelectionPopupAsync`

## 4. Create SolidWasteWeighingDetailViewModel

- [ ] 4.1 Create `ViewModels/SolidWasteWeighingDetailViewModel.cs` inheriting `AttendedWeighingDetailViewModelBase`, implementing `ITransientDependency`
- [ ] 4.2 Set `IsSolidWasteMode => true`
- [ ] 4.3 Move SolidWaste-specific `[Reactive]` properties: `SolidWasteOrderNumber`, `Streets`, `SelectedStreet`, `SolidWasteTypes`, `SelectedSolidWasteType`, `SolidWasteMaterials`, `SelectedSolidWasteMaterial`, `SelectedProviderItem`, `SelectedMaterialItem`, `SelectedStreetItem`
- [ ] 4.4 Move SolidWaste-specific delegate properties: `ProviderLoadPageAsync`, `MaterialLoadPageAsync`, `StreetLoadPageAsync`, `ProviderCreateNewAsync`, `MaterialCreateNewAsync`
- [ ] 4.5 Move SolidWaste-specific constructor subscriptions (auto-unit on material select, auto order number on weight change)
- [ ] 4.6 Override `LoadModeSpecificDataAsync` with `LoadSolidWasteDataAsync` logic (ExtraProperties reading)
- [ ] 4.7 Move `LoadStreetsPageAsync`, configuration loading helpers
- [ ] 4.8 Implement `SaveModeSpecificAsync` with `UpdateSolidWasteModeAsync` logic
- [ ] 4.9 Implement `CompleteModeSpecificAsync` with SolidWaste validation (supplier/material/street/type/orderNumber) and complete logic
- [ ] 4.10 Move SolidWaste-specific commands: `CreateNewProviderAsync`, `CreateNewMaterialAsync`

## 5. Update Parent ViewModel

- [ ] 5.1 Change `DetailViewModel` property type from `AttendedWeighingDetailViewModel` to `AttendedWeighingDetailViewModelBase` in `AttendedWeighingViewModel.cs`
- [ ] 5.2 Update `OpenDetail`/`OpenDetailAsync` to create `StandardWeighingDetailViewModel` or `SolidWasteWeighingDetailViewModel` based on `item.WeighingMode`

## 6. Update View Layer

- [ ] 6.1 Update `AttendedWeighingDetailView.axaml` `x:DataType` to `vm:AttendedWeighingDetailViewModelBase`
- [ ] 6.2 Update `AttendedWeighingDetailView.axaml.cs` to use base class type for `GetService` and `WireInteractions`
- [ ] 6.3 Update `StandardModeFormView.axaml` `x:DataType` to `vm:AttendedWeighingDetailViewModelBase`
- [ ] 6.4 Update `SolidWasteModeFormView.axaml` `x:DataType` to `vm:AttendedWeighingDetailViewModelBase`

## 7. Cleanup and Verification

- [ ] 7.1 Delete original `ViewModels/AttendedWeighingDetailViewModel.cs`
- [ ] 7.2 Verify zero occurrences of `if.*IsSolidWasteMode` in all ViewModel files
- [ ] 7.3 Build the project and fix any compilation errors
