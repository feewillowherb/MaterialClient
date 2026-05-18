# Addendum — MaterialClient.Urban (Technical)

## 1. Enum Extensions (`MaterialClient.Common`)

```csharp
// ProductCode.cs
UrbanManagement = 5030  // Description: 城管称重客户端

// WeighingMode.cs  
UrbanManagement = 201   // Description: 城管称重管理平台模式
```

**Note:** `WeighingMode` currently uses `0`, `1`; Urban uses `201` per stakeholder request for remote filtering—not sequential `2`.

## 2. Project Layout (Proposed)

```
MaterialClient.sln
├── MaterialClient              (existing UI host)
├── MaterialClient.Urban        (new: WinExe, no Avalonia UI v1)
├── MaterialClient.Common       (shared entities, EF, services)
└── MaterialClient.Common.Tests
```

**MaterialClient.Urban** references:
- `MaterialClient.Common`
- ABP Autofac, EF Core SQLite, Serilog, Refit (same versions as main app)
- Optional: subset of hardware modules from `MaterialClient` (scale, LPR) via shared service registration

**MaterialClient.UrbanModule** (ABP):
- Register Urban-only background workers
- Do **not** register `PollingBackgroundService` from main app
- Register `IUrbanManagementApi` (Refit) when contract known

## 3. Configuration Keys (Draft)

```json
{
  "UrbanManagement": {
    "BaseUrl": "https://api.example.com/",
    "AuthorizationFilePath": "C:\\ProgramData\\MaterialClientUrban\\auth.dat",
    "SiteId": "",
    "TelemetryIntervalSeconds": 300,
    "UploadBatchSize": 50
  },
  "ProductDefaults": {
    "ProductCode": 5030,
    "WeighingMode": 201
  }
}
```

Mirror existing patterns: `appsettings.json`, `appsettings.Development.json`, UserSecrets for dev.

## 4. Services to Reuse vs. Omit

| Component | Urban v1 |
|-----------|----------|
| `WeighingRecord` / repository | Reuse |
| `AttendedWeighingService` (or slim variant) | Reuse / fork thin wrapper |
| `WeighingMatchingService` | **Do not register** |
| `SyncMaterialService` | **Do not register** |
| `PollingBackgroundService` | **Replace** with `UrbanUploadBackgroundService` |
| `AuthenticationService.LoginAsync` | **Do not use** |
| `LicenseService.VerifyAuthorizationCodeAsync` | **Do not use** v1 |
| `MinimalWebHostService` | Reuse if LPR callbacks needed (HuaXia/Hikvision) |
| Static auth | New `IUrbanAuthorizationFileService` — read file, log |

## 5. Upload Pipeline (Draft)

1. Query `WeighingRecord` where `WeighingMode == UrbanManagement` AND not uploaded (flag TBD: `ExtraProperties` or new column `UrbanSyncStatus`).
2. Map to `UrbanWeighingRecordDto` (new record type in Common or Urban project).
3. `POST /api/weighing-records` (placeholder).
4. On success: mark uploaded timestamp; on failure: increment retry, log.

`[ASSUMPTION:]` Add `UrbanSyncStatus` via ExtraProperties initially to avoid migration in spike; formal column in epic 2 if needed.

## 6. Telemetry Payload (Draft)

```json
{
  "siteId": "string",
  "productCode": 5030,
  "appVersion": "1.0.0",
  "uptimeSeconds": 3600,
  "devices": [
    { "type": "Scale", "name": "COM3", "status": "Connected", "lastError": null }
  ],
  "errors": [
    { "at": "2026-05-18T10:00:00Z", "message": "Upload failed", "category": "Upload" }
  ]
}
```

Implement via dedicated `IUrbanTelemetryService` + Serilog sink subscribing to Error+.

## 7. Static Authorization File (v1)

- Read bytes/text from `AuthorizationFilePath`.
- Log: `Information` if file exists and length > 0; `Error` if missing.
- No remote call.
- Future: parse signed payload, expiry, site binding.

## 8. Coexistence with OpenSpec

Implementation changes should eventually be captured as an OpenSpec change under `openspec/changes/` when coding starts; this BMAD artifact set is the planning source.
