---
title: Epics and Stories — MaterialClient.Urban
created: 2026-05-18
prd: prd.md
architecture: architecture.md
---

# Epics and Stories: MaterialClient.Urban

## Epic E1: Solution Foundation

| Story | Title | FR | Acceptance |
|-------|-------|-----|------------|
| S1.1 | Add `MaterialClient.Urban` project to solution | FR-1 | Project builds; references Common |
| S1.2 | Add `MaterialClientUrbanModule` with ABP + Autofac + SQLite | FR-1 | App starts without UI |
| S1.3 | Add `appsettings` schema for UrbanManagement | FR-1 | Config binds; secrets via UserSecrets |
| S1.4 | Extend `ProductCode` and `WeighingMode` enums | FR-2 | 5030 and 201 present with descriptions |

## Epic E2: Authorization (v1)

| Story | Title | FR | Acceptance |
|-------|-------|-----|------------|
| S2.1 | Implement `IUrbanAuthorizationFileService` | FR-3 | Reads configured path |
| S2.2 | Startup auth logging | FR-3 | Info/Error logs; no remote API call |
| S2.3 | Unit tests for missing/present file | FR-3 | Tests pass |

## Epic E3: Weighing Capture (Urban Mode)

| Story | Title | FR | Acceptance |
|-------|-------|-----|------------|
| S3.1 | Register scale/LPR services in Urban module | FR-4 | Hardware events create records |
| S3.2 | Force `WeighingMode.UrbanManagement` on create | FR-2 | DB rows have mode 201 |
| S3.3 | Exclude waybill/matching service registration | FR-4 | No Waybill inserts in tests |

## Epic E4: Upload Pipeline

| Story | Title | FR | Acceptance |
|-------|-------|-----|------------|
| S4.1 | Define `UrbanWeighingRecordDto` + mapper | FR-5 | Maps from `WeighingRecord` |
| S4.2 | Add `IUrbanManagementApi` Refit interface (stub) | FR-4 | Mock server test passes |
| S4.3 | Implement `UrbanWeighingUploadService` | FR-4 | Pending → Uploaded flow |
| S4.4 | Add `UrbanOperationsBackgroundService` | FR-9 | Runs upload on timer |
| S4.5 | Retry/backoff on upload failure | FR-4 | Failed records retry |

## Epic E5: Telemetry

| Story | Title | FR | Acceptance |
|-------|-------|-----|------------|
| S5.1 | Device status collector | FR-6 | Reports scale/LPR state |
| S5.2 | Application heartbeat payload | FR-7 | Version + uptime in payload |
| S5.3 | Error log batch sink | FR-8 | Errors appear in telemetry batch |
| S5.4 | Telemetry upload in background worker | FR-6–8 | Mock API receives payloads |

## Epic E6: Shutdown & Operations

| Story | Title | FR | Acceptance |
|-------|-------|-----|------------|
| S6.1 | Graceful shutdown (WebHost, hardware, ABP) | FR-9 | No hang >5s per AGENTS.md |
| S6.2 | Deployment README for auth file path | FR-3 | Doc in repo or ops guide |

## Suggested Sprint 1 (MVP)

S1.1 → S1.4 → S2.1 → S2.2 → S4.2 (stub) → S4.4 (skeleton) → S3.2

## Dependencies

- **Blocks E4/E5:** OQ-1 UrbanManagement API contract.
- **Can parallelize:** E2 and E1.
