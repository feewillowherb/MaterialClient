# OpenSpec Proposal: Hikvision Capture Device Migration

## Proposal Summary

Migrate and refactor Hikvision (海康威视) capture device integration to a target codebase. **In scope:** manual capture (主动抓拍) and passive capture (被动抓拍) only. **Out of scope:** live preview (实时预览). Both capture types receive result data (images, plate info) via a single listen channel: `NET_DVR_StartListen_V30` callback.

---

## 1. Problem / Context

- Existing Hikvision capture logic lives in `CaptureDevice.cs` (BLL), using CHCNetSDK (native SDK) and possibly AlarmCSharpDemo.
- Current code has correctness and lifecycle issues: listen handle is never stopped with `NET_DVR_StopListen_V30`; callback delegate can be garbage-collected (unmanaged SDK holds only a function pointer).
- Config mixes “listen local IP/port” with “device IP/port”; error handling is inconsistent.
- Migration target may run alongside a .NET web application; listen port must not conflict with the web app port.

---

## 2. Goals

1. **Preserve behavior:** Manual capture (app-triggered shot) and passive capture (device-pushed capture) with the same receive path.
2. **Fix correctness:** Proper stop sequence (`NET_DVR_StopListen_V30` for listen handle); pin callback delegate (GCHandle) for listen lifetime.
3. **Improve structure:** Centralize P/Invoke and SDK types; clear config (listen vs device); consistent error handling and logging.
4. **Document constraints:** Dedicated listen port (no sharing with web app); use NET_DVR_StartListen_V30 (no V40 Listen in current SDK).

---

## 3. Functional Requirements

### 3.1 Manual Capture (主动抓拍)

- Application can trigger a single capture on the device.
- Flow: initialize SDK if not already; start listen if not already (see 3.3); login to device; call `NET_DVR_ContinuousShoot` with valid `NET_DVR_SNAPCFG`; result (images, plate info) is delivered via the same `NET_DVR_StartListen_V30` callback as passive capture.
- No live preview; no `NET_DVR_RealPlay_V40` or `NET_DVR_StopRealPlay`.

### 3.2 Passive Capture (被动抓拍)

- Device pushes capture data to the client without app trigger.
- Flow: client has started listen via `NET_DVR_StartListen_V30`; device sends alarm/capture; callback `MSGCallBack` receives COMM_UPLOAD_PLATE_RESULT (0x2800) or COMM_ITS_PLATE_RESULT (0x3050); application parses `NET_DVR_PLATE_RESULT` or `NET_ITS_PLATE_RESULT` and persists images / plate info as required.

### 3.3 Unified Receive Path

- **One listen, one callback:** Both manual and passive capture results are received through `NET_DVR_StartListen_V30(sLocalIP, wLocalPort, DataCallback, pUserData)`. The callback must handle:
  - `COMM_UPLOAD_PLATE_RESULT` → parse `NET_DVR_PLATE_RESULT` (e.g. `dwFarCarPicLen`, `pBuffer5`, `byAbsTime`, `struPlateInfo`, `struVehicleInfo`).
  - `COMM_ITS_PLATE_RESULT` → parse `NET_ITS_PLATE_RESULT` (e.g. `struPicInfo[]`, `struSnapFirstPicTime`, `struPlateInfo`, `struVehicleInfo`, `dwPicNum`).
- Callback runs on SDK threads; implementation must not block (marshal to app thread or queue for processing).

### 3.4 Lifecycle

- **Start:** `NET_DVR_Init`; then `NET_DVR_StartListen_V30` with a pinned callback delegate (GCHandle). Store listen handle.
- **Stop:** Call `NET_DVR_StopListen_V30(listenHandle)` when listen was started; then `NET_DVR_Logout(userId)` if logged in for manual capture; then `NET_DVR_Cleanup`. Do **not** call `NET_DVR_StopRealPlay` (preview out of scope).
- **Manual capture session:** Login (`NET_DVR_Login_V40`) when user triggers manual capture; after `NET_DVR_ContinuousShoot`, optionally logout to release device session. Listen remains active for receiving result.

---

## 4. Non-Functional Requirements

### 4.1 Correctness (CRITICAL)

- **Callback lifetime:** Pin the `MSGCallBack` delegate with `GCHandle` for the entire period between `NET_DVR_StartListen_V30` and `NET_DVR_StopListen_V30`. Document in code: *"CRITICAL: Use GCHandle to prevent delegate from being garbage collected. The unmanaged SDK only stores a function pointer; the GC does not know it is still in use."*
- **Stop sequence:** Always call `NET_DVR_StopListen_V30(listenHandle)` when listen was started; never pass the listen handle to `NET_DVR_StopRealPlay`.

### 4.2 Configuration

- **Listen:** Explicit settings for the client listen endpoint used by `NET_DVR_StartListen_V30`, e.g. `ListenLocalIP`, `ListenLocalPort`. This port must be dedicated (not used by the .NET web application).
- **Device:** Separate settings for device connection used by login and manual capture, e.g. device IP, port, username, password (VideoIP, VideoPort, VideoUserName, VideoPwd or equivalent).

### 4.3 Port Usage

- The listen port (`ListenLocalPort`) must not be shared with the web application. One process cannot bind the same port twice; different processes on the same machine cannot bind the same port. Use a dedicated port for Hikvision listen.

### 4.4 Error Handling and Logging

- After every SDK call that can fail, call `NET_DVR_GetLastError` (and optionally map to string). Propagate or log errors; avoid empty catch blocks.

### 4.5 Encoding

- Use consistent encoding for plate text (e.g. GBK for Chinese). Centralize in one helper to avoid drift.

---

## 5. Technical Scope

### 5.1 APIs to Use (Hikvision SDK)

| API | Use |
|-----|-----|
| `NET_DVR_Init` | SDK initialization |
| `NET_DVR_StartListen_V30` | Start listen; receive capture data (manual + passive) via callback |
| `NET_DVR_StopListen_V30` | Stop listen; must be called on stop |
| `NET_DVR_Login_V40` | Device login for manual capture |
| `NET_DVR_Logout` | Logout after manual capture session |
| `NET_DVR_ContinuousShoot` | Trigger manual capture |
| `NET_DVR_Cleanup` | SDK cleanup |
| `NET_DVR_GetLastError` | Error code after failed calls |

**Do not use:** `NET_DVR_RealPlay_V40`, `NET_DVR_StopRealPlay`, `NET_DVR_PREVIEWINFO` (preview out of scope).

### 5.2 Callback and Types

- **MSGCallBack:** `(LONG lCommand, NET_DVR_ALARMER *pAlarmer, char *pAlarmInfo, DWORD dwBufLen, void* pUser)`.
- **Structures (P/Invoke):** `NET_DVR_USER_LOGIN_INFO`, `NET_DVR_DEVICEINFO_V40`, `NET_DVR_SNAPCFG`, `NET_DVR_JPEGPARA`, `NET_DVR_ALARMER`, `NET_DVR_PLATE_RESULT`, `NET_ITS_PLATE_RESULT`. Layout and marshalling must match HCNetSDK.h (CH-HCNetSDKV6.1.9.48). Centralize in one module (e.g. `HikvisionSdk.cs` or dedicated adapter assembly).

### 5.3 Listen API Version

- Use **NET_DVR_StartListen_V30** and **NET_DVR_StopListen_V30** only. There is no `NET_DVR_StartListen_V40` in the current SDK.

---

## 6. Out of Scope

- Live preview (实时预览); any RealPlay/Preview APIs.
- Other device brands (e.g. 臻识); this proposal is Hikvision-only.
- Changing the semantics of manual vs passive capture; only migration, correctness fixes, and refactor are in scope.

---

## 7. Acceptance Criteria

1. **Manual capture:** User can trigger a capture; result (images, plate info) is received via the same listen callback and processed correctly.
2. **Passive capture:** Device-pushed captures are received via the listen callback and processed correctly (COMM_UPLOAD_PLATE_RESULT and COMM_ITS_PLATE_RESULT).
3. **Stop:** On stop, `NET_DVR_StopListen_V30(listenHandle)` is called when listen was started; no use of `NET_DVR_StopRealPlay` for the listen handle.
4. **Callback lifetime:** The MSGCallBack delegate is pinned (GCHandle) for the listen session; no GC-related crashes when SDK invokes the callback.
5. **Config:** Listen local IP/port and device IP/port (and credentials) are clearly separated; listen port is documented as dedicated (not shared with web app).
6. **Errors:** Failed SDK calls are followed by error retrieval and logging or propagation.
7. **P/Invoke:** Hikvision SDK calls and structures are centralized; layout matches SDK headers.

---

## 8. References

- Source: `Fdsoft.Weight.GovClient/BLL/CaptureDevice.cs`
- SDK headers: HCNetSDK.h (CH-HCNetSDKV6.1.9.48)
- Evaluation doc: `agents/海康威视抓拍机迁移评估文档.md`
