## Context

Current attended weighing detail UI is split into shared popup/container views plus mode-specific form views for standard and solid-waste workflows. The runtime exception indicates at least one compiled binding in shared UI was compiled against `StandardWeighingDetailViewModel`, while runtime DataContext in solid-waste flow is `SolidWasteWeighingDetailViewModel`.

Because Avalonia compiled binding generates typed accessors, a concrete-type mismatch causes immediate cast failure before normal fallback behavior can occur. This breaks solid-waste detail operations and makes the polymorphic ViewModel hierarchy unsafe at view-binding boundaries.

## Goals / Non-Goals

**Goals:**
- Ensure shared popup-level bindings compile against `AttendedWeighingDetailViewModelBase` (or another common contract) so either subclass can be used safely.
- Keep mode-specific bindings constrained to corresponding mode view boundaries where concrete type assumptions are valid.
- Preserve existing standard-mode behavior while removing solid-waste crash path.
- Add explicit validation steps that cover both standard and solid-waste popup open/save flows.

**Non-Goals:**
- Refactor business logic in detail ViewModels beyond what is needed for binding contract correctness.
- Redesign attended weighing UI/UX structure.
- Introduce new weighing modes or change backend API contracts.

## Decisions

- Use base-type DataType in shared views: `x:DataType` for popup/container controls that can host either mode will use `AttendedWeighingDetailViewModelBase`.
  - Rationale: keeps compiled binding strongly typed while remaining polymorphism-safe.
  - Alternative considered: disable compiled binding for these paths. Rejected due to losing compile-time binding checks.

- Keep concrete DataType only inside mode-specific forms (`Standard...` view and `SolidWaste...` view).
  - Rationale: allows mode-only fields/commands without unsafe casts in shared shell.
  - Alternative considered: flatten all fields to base ViewModel. Rejected due to unnecessary abstraction leakage and larger refactor.

- Add regression verification for cross-mode popup initialization and primary interactions.
  - Rationale: crash is mode-dependent; both modes must be exercised to catch future contract drift.
  - Alternative considered: rely only on manual spot-check. Rejected as insufficient for a previously shipped runtime crash class.

## Risks / Trade-offs

- [Risk] Additional shared bindings may still reference concrete-only members indirectly. -> Mitigation: audit shared XAML and require base-contract-only references in shared scope.
- [Risk] Moving DataType boundaries could expose missing base members at compile time. -> Mitigation: either bind those members inside mode-specific views or promote truly shared members into base ViewModel.
- [Risk] Limited automated UI coverage in current stack may leave a manual verification gap. -> Mitigation: include explicit two-mode manual checklist in tasks and add tests where feasible.

## Migration Plan

- No data migration required.
- Deploy as normal client release.
- Rollback strategy: revert changed view DataType/binding scope files to prior version if an unforeseen binding regression appears.

## Open Questions

- Should this include a dedicated UI smoke test around detail popup opening for each weighing mode in CI, or remain as a manual regression checklist for now?
