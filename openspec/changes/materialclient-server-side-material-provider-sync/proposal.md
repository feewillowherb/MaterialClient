## Why

Material and provider create/update flows in MaterialClient still include local-write paths, which causes data divergence from server state and makes governance difficult. This change moves write responsibility to server APIs so new changes are validated, persisted, and audited in one place.

## What Changes

- Replace local material create/edit write path with server-side NameDriven API calls.
- Replace local provider create/update write path with server-side API calls.
- Keep existing client method signatures stable (`CreateMaterialAsync`, `CreateProviderAsync`, `UpdateProviderAsync`) and switch implementation only.
- Add consistent client-side handling for remote business errors, network failures, and write-after-read refresh.
- Ensure provider/material changes are no longer persisted locally as the source of truth.

## Capabilities

### New Capabilities
- `material-provider-server-side-write-sync`: Route material/provider create-update operations from MaterialClient to server-side APIs with unified error handling and write-after-read refresh.

### Modified Capabilities
- `attended-weighing`: Material/provider edit interactions in attended weighing must use remote write path and reflect server-persisted results.

## Impact

- Affected code: Material/provider application services in `MaterialClient.Application`, related ViewModel command execution paths, and remote API client integration code.
- Affected behavior: Material/provider writes become server-authoritative; local write persistence path is removed from runtime flow.
- Affected API usage: Calls to server-side material/provider NameDriven endpoints with existing auth/token pipeline.
