## 1. Binding Contract Audit

- [x] 1.1 Review attended weighing detail popup and shared child controls to identify compiled bindings currently typed to `StandardWeighingDetailViewModel`.
- [x] 1.2 Confirm mode-specific binding boundaries (`StandardModeFormView` and `SolidWasteModeFormView`) and list concrete-only members that must remain inside those boundaries.

## 2. XAML/DataType Fix Implementation

- [x] 2.1 Update shared popup/container `x:DataType` declarations to `AttendedWeighingDetailViewModelBase` and adjust bindings to base-contract members only.
- [x] 2.2 Keep or move concrete-only bindings into mode-specific views so shared scope no longer requires casting to a concrete subclass.
- [x] 2.3 Build/compile Avalonia XAML to verify no compiled-binding type errors are introduced.

## 3. Regression Verification

- [ ] 3.1 Verify standard-mode detail popup open/edit flow still works without binding exceptions.
- [ ] 3.2 Verify solid-waste detail popup open/edit flow no longer throws `InvalidCastException` and renders correctly.
- [x] 3.3 Add or update automated/manual regression checks documenting both mode paths and expected no-crash behavior.
