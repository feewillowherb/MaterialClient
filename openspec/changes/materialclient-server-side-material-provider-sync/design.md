## Context

MaterialClient currently has mixed write paths for material/provider operations: some flows call remote APIs while create/update paths can still persist locally. This creates divergence risks and weakens server-side governance for validation, auditing, and conflict handling.

The target state is server-authoritative writes for material/provider changes, while preserving existing client-facing method signatures used by ViewModels and UI workflows. The implementation must align with existing token/auth pipeline and avoid broad UI contract churn.

## Goals / Non-Goals

**Goals:**
- Route material create/rename and provider create/update writes through remote APIs only.
- Keep `CreateMaterialAsync`, `CreateProviderAsync`, and `UpdateProviderAsync` signatures unchanged.
- Standardize client behavior for remote error handling, retry boundaries, and write-after-read refresh.
- Remove runtime local-write responsibility for provider/material modifications.

**Non-Goals:**
- This change does not migrate historical local data.
- This change does not redesign full-field material/provider editing contracts.
- This change does not refactor unrelated list/read experiences beyond required write-after-read consistency.

## Decisions

### Decision 1: Keep client contracts stable and swap implementation
- Choice: Keep existing service method signatures and update internals to call server APIs.
- Rationale: Reduces UI/ViewModel regression risk and shortens migration time.
- Alternative considered: Introduce new remote-only service contracts and refactor all callers. Rejected due to larger blast radius.

### Decision 2: Server-authoritative write policy
- Choice: Material/provider create-update operations MUST persist on server, not local client storage.
- Rationale: Enforces single source of truth and central business validation.
- Alternative considered: Dual-write (local + server) transition phase. Rejected due to reconciliation complexity and conflict risk.

### Decision 3: Write-after-read consistency on successful writes
- Choice: After successful create/update, client refreshes entity state from server response or follow-up query when needed.
- Rationale: Prevents stale UI state and aligns with server-normalized/defaulted values.
- Alternative considered: Trust pre-submit local model without refresh. Rejected due to defaulting/concurrency mismatch risk.

### Decision 4: Structured remote failure mapping in application service
- Choice: Map transport/business failures into existing user-facing failure channels with stable messaging behavior.
- Rationale: Keeps ViewModels simple and avoids per-screen error branching.
- Alternative considered: Bubble raw HTTP/DTO errors to UI. Rejected because it leaks transport details and increases UI coupling.

## Risks / Trade-offs

- [Risk] Remote dependency increases perceived latency for save flows -> Mitigation: bounded retries for transient faults and explicit busy-state UX.
- [Risk] API contract mismatch between client and server versions -> Mitigation: add integration coverage for request/response and error code mapping.
- [Risk] Existing local fallback assumptions may remain in edge paths -> Mitigation: remove local write entry points and add tests for server-only persistence.
- [Risk] Version/conflict failures may increase after cutover -> Mitigation: provide deterministic conflict handling and prompt user refresh/retry path.

## Migration Plan

1. Implement remote-only write path in material/provider application services.
2. Preserve public method signatures and update underlying adapters/mappers.
3. Add tests for success, validation failure, conflict, auth failure, and transient network scenarios.
4. Run staged validation in test/pre-release environments against target server endpoints.
5. Roll out with monitoring on write error rates and response latency.

Rollback:
- Re-enable previous implementation behind a temporary feature flag only if critical remote outage occurs.
- Keep rollback time-boxed and require issue triage before reattempting cutover.

## Open Questions

- Should client retries include idempotency key propagation for all create calls or only material create?
- Is provider update conflict behavior version-based or last-write-wins in current server implementation?
- For offline mode, should save be blocked immediately or queued for a later enhancement phase?
