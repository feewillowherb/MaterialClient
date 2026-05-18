---
title: MaterialClient.Urban
status: final
created: 2026-05-18
updated: 2026-05-18
---

# PRD: MaterialClient.Urban

## 0. Document Purpose

This PRD defines **MaterialClient.Urban**, a headless-oriented variant of MaterialClient that uploads weighing data to **UrbanManagement** (城管称重管理平台). It is written for PM, architects, and implementation agents. Technical transport details and reuse boundaries live in `addendum.md`. Downstream: `architecture.md`, `epics-and-stories.md`.

**Inputs:** User requirements draft (2026-05-18), existing `MaterialClient` / `openspec/project.md`, `WeighingRecord` entity.

## 1. Vision

Municipal weighing sites need a lightweight client that captures scale and LPR data, persists **WeighingRecord** locally, and reports to UrbanManagement—without the full material-acceptance workflow (login, waybills, material sync, OSS attachments). MaterialClient.Urban reuses proven hardware and persistence patterns from MaterialClient while narrowing scope to Urban-specific upload, telemetry, and static-file authorization.

UrbanManagement operators must see **device health**, **application health**, and **error logs** remotely; the client is responsible for pushing that telemetry on a schedule or on change.

## 2. Target User

### 2.1 Primary Persona

**Site operator / installer** — deploys the Urban client on a Windows PC at a weighbridge, drops an authorization file, and expects unattended upload. No daily login.

### 2.2 Jobs To Be Done

- Record inbound/outbound weights with plate recognition and persist them reliably.
- Upload weighing records to UrbanManagement without waybill pairing.
- Prove the site is licensed via a static authorization file at startup.
- Let the platform monitor scales, cameras, and client errors without visiting the site.

### 2.3 Non-Users (v1)

- Material acceptance clerks using full MaterialClient UI.
- Users who need waybill creation, material catalog sync, or platform login flows.

### 2.4 Key User Journeys

- **UJ-1. Cold start with authorization file**
  - **Persona + context:** Installer starts the Urban client after deployment.
  - **Entry state:** No session; `appsettings` and auth file path configured.
  - **Path:** App starts → reads static auth file → validates presence/format (v1: log outcome) → starts hardware listeners and background upload.
  - **Climax:** Log shows successful auth file recognition; upload worker begins.
  - **Resolution:** Client runs headless; records accumulate and upload.
  - **Edge case:** Missing auth file → log error, do not call remote auth APIs; policy for exit vs. degraded mode in OQ-2.

- **UJ-2. Weighing event upload**
  - **Persona + context:** Truck on scale; LPR captures plate.
  - **Entry state:** Client authorized; devices connected.
  - **Path:** Weight stable → create `WeighingRecord` with `WeighingMode.UrbanManagement` → queue for UrbanManagement API → retry on failure.
  - **Climax:** Record marked uploaded (or pending with retry).
  - **Resolution:** UrbanManagement has the record; local DB retains audit copy.

- **UJ-3. Platform visibility**
  - **Persona + context:** UrbanManagement ops team.
  - **Entry state:** Client running at site.
  - **Path:** Client periodically publishes device status, app heartbeat, and recent error log excerpts.
  - **Climax:** Platform dashboard reflects online/offline and last error.
  - **Resolution:** Ops can triage without RDP to site.

## 3. Glossary

- **MaterialClient** — Existing Avalonia desktop app (Standard / SolidWaste modes).
- **MaterialClient.Urban** — New host project; UrbanManagement-focused; no v1 UI.
- **UrbanManagement** — Remote municipal weighing management platform (API TBD).
- **WeighingRecord** — Domain entity for a single weighing event (`MaterialClient.Common`).
- **ProductCode** — Product identifier sent to platform/licensing; Urban uses `5030`.
- **WeighingMode** — Data isolation discriminator on records; Urban uses `201` (`UrbanManagement`).
- **Static authorization file** — Local file used at startup instead of interactive login / real-time license API (v1: validate + log only).
- **Telemetry** — Device status, software status, and error log payloads sent to UrbanManagement.

## 4. Features

### 4.1 Solution & Host (`MaterialClient.Urban`)

**Description:** Add `MaterialClient.Urban` to the solution with configuration structure parallel to `MaterialClient` (appsettings, Serilog, ABP module, DI). v1 may use a minimal host (no Avalonia main window) or hidden tray—**no functional UI pages**. Realizes UJ-1.

**Functional Requirements:**

#### FR-1: Urban solution project

The team can build and run `MaterialClient.Urban` as a distinct executable referencing `MaterialClient.Common` (and shared infrastructure packages). Realizes UJ-1.

**Consequences (testable):**
- Solution contains `MaterialClient.Urban` project entry.
- Startup loads configuration from `appsettings.json` / environment overrides consistent with MaterialClient patterns.
- No Avalonia views are required for v1 acceptance.

**Out of Scope:**
- Feature parity with full MaterialClient UI.

---

### 4.2 Product & Data Identity

**Description:** Urban data is isolated via `ProductCode` and `WeighingMode` so shared database and services can filter Urban records. Realizes UJ-2.

**Functional Requirements:**

#### FR-2: ProductCode and WeighingMode

The system assigns `ProductCode = 5030` (`UrbanManagement`) and `WeighingMode = 201` (`UrbanManagement`) to all Urban weighing records and Urban-specific configuration defaults.

**Consequences (testable):**
- `ProductCode` enum includes `UrbanManagement = 5030`.
- `WeighingMode` enum includes `UrbanManagement = 201`.
- New `WeighingRecord` rows created by Urban host use `WeighingMode.UrbanManagement`.
- Settings/bootstrap for Urban host default to Urban mode (no SolidWaste/Standard switching UI in v1).

---

### 4.3 Authorization (Static File, v1)

**Description:** No user login. On startup, read configured authorization file; v1 does **not** call real-time license APIs—log success/failure and continue per policy. Realizes UJ-1.

**Functional Requirements:**

#### FR-3: Static authorization file check

On application start, the client loads the authorization file from a configured path, performs basic validation (exists, non-empty, optional format check), and writes structured logs. Realizes UJ-1.

**Consequences (testable):**
- Missing file produces error-level log with path (no secrets).
- Present file produces information-level log indicating acceptance for v1.
- No `AuthenticationService.LoginAsync` or periodic `VerifyAuth` against platform in Urban v1.

**Out of Scope:**
- Full cryptographic validation of auth file content (deferred).
- Storing license in DB from remote API response.

**Notes:** `[ASSUMPTION: v1 continues running after failed auth file with error log unless product owner mandates hard stop — see OQ-2.]`

---

### 4.4 Weighing Capture & Upload

**Description:** Capture weighing events like MaterialClient (scale + optional LPR) but **only** sync `WeighingRecord` to UrbanManagement—no waybill matching, no waybill push, no material/provider sync. Realizes UJ-2.

**Functional Requirements:**

#### FR-4: WeighingRecord-only upload

The Urban client uploads `WeighingRecord` payloads to UrbanManagement and does not invoke waybill pairing, `PushWaybillAsync`, or waybill-related APIs.

**Consequences (testable):**
- Upload worker selects pending records where `WeighingMode == UrbanManagement`.
- No `Waybill` entities are created or updated by Urban v1 flows.
- Failed uploads retry with backoff; state persisted on record or sync queue table.

#### FR-5: Record field scope

Upload DTO includes fields required by UrbanManagement contract; minimum: weight, plate, timestamps, mode, delivery type if applicable, audit ids. `[ASSUMPTION: photos/attachments excluded in v1 unless API requires — see addendum.]`

**Consequences (testable):**
- Integration test or contract test against mock UrbanManagement API validates DTO shape once OpenAPI available.

**Out of Scope:**
- `MatchedId` / `WaybillId` population.
- Solid-waste-specific extra properties unless Urban API mandates.

---

### 4.5 Telemetry to UrbanManagement

**Description:** UrbanManagement must know device state, software state, and errors. Realizes UJ-3.

**Functional Requirements:**

#### FR-6: Device status reporting

The client periodically reports status for configured devices (scale serial, LPR, cameras as applicable): connected/disconnected, last read time, last error.

**Consequences (testable):**
- At least one telemetry payload per reporting interval while running.
- Device offline transitions reflected within one reporting interval.

#### FR-7: Software status reporting

The client reports application version, uptime, last successful upload, and Urban `ProductCode` / host identity.

**Consequences (testable):**
- Payload includes assembly version and configurable site id.

#### FR-8: Error log reporting

The client forwards recent error-level log entries (or aggregated error events) to UrbanManagement on schedule and optionally on fatal errors.

**Consequences (testable):**
- Serilog sink or dedicated telemetry service batches errors without blocking weighing path.
- PII/secrets are not included in forwarded logs.

---

### 4.6 Background Operations

**Description:** Replace MaterialClient polling worker scope with Urban-specific jobs. Realizes UJ-2, UJ-3.

**Functional Requirements:**

#### FR-9: Urban background worker

A dedicated `AsyncPeriodicBackgroundWorkerBase` (or equivalent) runs upload + telemetry; it does not run material sync, waybill push, or OSS attachment sync.

**Consequences (testable):**
- Worker registration exists only in Urban module.
- Cancellation and shutdown respect existing app exit ordering (WebHost stop → hardware → ABP shutdown).

## 5. Non-Goals (Explicit)

- Avalonia UI pages for Urban v1.
- Interactive login / session refresh against material platform.
- Real-time authorization API integration in v1.
- Waybill matching, waybill push, material/provider sync.
- OSS attachment upload (unless explicitly added post-API review).
- Duplicating `WeighingRecord` entity in a separate model project.

## 6. MVP Scope

### 6.1 In Scope

- `MaterialClient.Urban` project + module bootstrap.
- `ProductCode.UrbanManagement = 5030`, `WeighingMode.UrbanManagement = 201`.
- Static auth file read + log.
- Weighing capture (reuse services where possible).
- WeighingRecord upload client (mock/stub until API defined).
- Telemetry: device, app, errors (stub acceptable for endpoints TBD).
- Configuration aligned with MaterialClient patterns.

### 6.2 Out of Scope for MVP

| Item | Reason |
|------|--------|
| UI | User explicit |
| Login / token refresh | User explicit |
| Full auth file crypto | Deferred; log-only v1 |
| Waybill domain | User explicit |
| UrbanManagement API final integration | Pending API spec (OQ-1) |

## 7. Success Metrics

**Primary**

- **SM-1**: ≥99% of created Urban `WeighingRecord` rows reach `Uploaded` or `PendingRetry` within 5 minutes under normal network. Validates FR-4.
- **SM-2**: Telemetry heartbeat received by platform (or mock) every ≤5 minutes while app running. Validates FR-6, FR-7.

**Secondary**

- **SM-3**: Zero waybill rows created by Urban host in acceptance test dataset. Validates FR-4.

**Counter-metrics**

- **SM-C1**: Do not optimize for feature parity with full MaterialClient UI.

## 8. Open Questions

1. **OQ-1:** UrbanManagement OpenAPI/base URL, auth headers, and WeighingRecord DTO schema?
2. **OQ-2:** On missing/invalid auth file—exit process or run in degraded (capture-only) mode?
3. **OQ-3:** Reporting interval and retention for error log forwarding?
4. **OQ-4:** Headless WinExe vs. system tray icon for operator stop/restart?
5. **OQ-5:** Share single SQLite DB with MaterialClient install or separate DB file per product?

## 9. Assumptions Index

| Tag | Assumption |
|-----|------------|
| A-1 | Urban host reuses `MaterialClient.Common` DbContext and hardware services |
| A-2 | Auth file path in `appsettings` (`UrbanManagement:AuthorizationFilePath`) |
| A-3 | Upload/telemetry APIs can be stubbed until OQ-1 resolved |
| A-4 | v1 continues on auth file failure with error log (see OQ-2) |
