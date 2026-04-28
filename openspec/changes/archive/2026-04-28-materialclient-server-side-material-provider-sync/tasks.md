## 1. Contract and write-path discovery

- [x] 1.1 Locate all material/provider create-update entry points currently using local persistence in `MaterialClient.Application`.
- [ ] 1.2 Confirm remote endpoint contracts and DTO mappings for material create/rename and provider create/update.
- [x] 1.3 Document and remove local-write fallback assumptions for these flows.

## 2. Remote write implementation

- [x] 2.1 Refactor `CreateMaterialAsync(string materialName)` implementation to call server API and return server-authored state.
- [x] 2.2 Refactor `CreateProviderAsync(string providerName, DeliveryType deliveryType)` implementation to call server API.
- [x] 2.3 Refactor `UpdateProviderAsync(int id, string providerName, string? contactName, string? contactPhone)` implementation to call server API.
- [x] 2.4 Ensure no local persistence path is executed as final write result for material/provider edits.

## 3. Error handling and post-write consistency

- [x] 3.1 Map remote business and transport errors into stable client-facing failure messages.
- [x] 3.2 Add bounded retry handling for transient network failures without local-write fallback.
- [x] 3.3 Ensure write-after-read refresh behavior uses server state for UI/navigation decisions.

## 4. Verification

- [ ] 4.1 Add or update tests for material/provider remote write success and failure paths.
- [ ] 4.2 Add coverage for attended weighing flow to ensure provider/material edits use remote APIs.
- [ ] 4.3 Run lint/build/test checks and verify no regressions in related workflows.
