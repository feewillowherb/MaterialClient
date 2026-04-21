## Why

Attended weighing detail popup uses compiled bindings and currently has at least one binding path compiled against `StandardWeighingDetailViewModel`. When the popup is opened in solid-waste mode, Avalonia tries to cast `SolidWasteWeighingDetailViewModel` to `StandardWeighingDetailViewModel`, causing `InvalidCastException` and breaking the workflow.

## What Changes

- Fix detail popup binding contracts so shared popup-level bindings target the base type (`AttendedWeighingDetailViewModelBase`) instead of a concrete subclass.
- Ensure standard-only and solid-waste-only bindings are isolated to their own mode views or guarded by type-correct DataType boundaries.
- Add verification coverage for both modes to prevent regression of compiled-binding cast issues.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `detail-viewmodel-hierarchy`: tighten view binding requirements to ensure popup and shared controls remain polymorphism-safe across `StandardWeighingDetailViewModel` and `SolidWasteWeighingDetailViewModel`.

## Impact

- Affected code: attended weighing popup/view XAML files and related DataType declarations; possible adjustments in detail ViewModel exposure for shared UI state.
- Runtime impact: removes crash path in solid-waste detail flow and keeps standard flow unchanged.
- Testing impact: add or update UI/viewmodel tests and manual verification steps for both weighing modes.
