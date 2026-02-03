# Evaluation: Remove DefaultWeighingMode Bootstrap and Replace with UserSession / AuthCodeWindow

## 1. Current State

- **Temporary code** in `MaterialClientModule.OnApplicationInitializationAsync()` (lines 216–260) reads `SystemSettings:DefaultWeighingMode` from `appsettings.json` and overwrites the persisted setting in the DB on every startup.
- **Purpose (as commented)**: One-time/bootstrap initialization so the default is persisted per installation.
- **Cons**: Runs on every startup; couples deployment default to appsettings; bypasses user/session as source of truth.

## 2. Goal

- Remove the temporary bootstrap block from `MaterialClientModule.cs`.
- Replace the source of default weighing mode with either **UserSession (ProductId)** or **AuthCodeWindow** so the default is set by session/product or by user choice at auth, not by appsettings.

## 3. Replacement Options

### Option A: UserSession / ProductId

**Idea**: Derive default weighing mode from the current session’s product. `ProductCode` already aligns with weighing mode (Standard = 5000, SolidWaste = 5010). After successful login, set `SystemSettings.DefaultWeighingMode` from `UserSession.ProductId` (once per session or only when not yet set).

**Pros**

- No appsettings key; single source of truth is the logged-in product.
- Aligns with existing `ProductCode` ↔ business mode (Standard vs SolidWaste).
- No extra UI; behavior is automatic after login.

**Cons**

- Requires a clear rule: update on every login vs only when “never set by user” (e.g. first run or first login).
- If backend adds new product IDs, need a safe mapping (e.g. unknown ProductId → keep current or fallback to `WeighingMode.Standard`).

**Implementation outline**

1. Remove the bootstrap block (and optionally the `SystemSettings:DefaultWeighingMode` key from `appsettings.json`).
2. Add a mapping: ProductId → WeighingMode (e.g. 5000 → Standard, 5010 → SolidWaste; others → Standard or keep existing).
3. After successful login (in `AuthenticationService` when creating/saving `UserSession`), call a small helper that:
   - Gets current settings and current `DefaultWeighingMode`.
   - Option A1: Always set `DefaultWeighingMode` from `session.ProductId` (product wins every time).
   - Option A2: Set only if “never explicitly set” (e.g. first run or a flag); otherwise leave user’s choice.
4. Use `ISettingsService` in the auth flow (inject where session is saved) to load/save settings with the new default.

**Suggested rule**: Prefer **A2** (set from ProductId only when the app has never had a user/settings-driven default) to avoid overwriting a user’s later change. If product must always win, use A1.

---

### Option B: AuthCodeWindow (or post-auth onboarding)

**Idea**: Stop bootstrapping from appsettings; let the user choose the default weighing mode in the auth flow (e.g. in `AuthCodeWindow` or a one-time dialog right after first successful auth).

**Pros**

- Explicit user choice; no dependency on ProductId or backend.
- Fits “first-time setup” or “per-installation” default without touching appsettings.

**Cons**

- Requires UI (e.g. dropdown or radio for Standard / SolidWaste) and wiring in `AuthCodeWindow` (or a follow-up window).
- Need a clear moment to show it (e.g. only when no default has been set yet).

**Implementation outline**

1. Remove the bootstrap block (and optionally the appsettings key).
2. In `AuthCodeWindow.axaml`, add a row (e.g. below the “授权码” row) with a label “默认称重模式” and a ComboBox bound to a ViewModel property (e.g. `DefaultWeighingMode`), options: Standard, SolidWaste.
3. In `AuthCodeWindowViewModel`, on successful verification (or when closing after success), if a default was selected, call `ISettingsService.GetSettingsAsync()` → set `SystemSettings.DefaultWeighingMode` → `SaveSettingsAsync()`.
4. Alternatively: show the same choice in a small “first-run” or “onboarding” dialog after first successful auth instead of inside the auth window.

**Suggested rule**: Only write the default when the user has actually chosen it in this UI (e.g. on “确认” or window close after success), and optionally only when current `DefaultWeighingMode` has never been set (to avoid overwriting later settings changes).

---

## 4. Recommendation

| Criterion              | Option A (UserSession/ProductId)     | Option B (AuthCodeWindow)        |
|------------------------|--------------------------------------|----------------------------------|
| Effort                 | Medium (mapping + auth flow + rule)  | Medium (UI + VM + settings save) |
| Source of truth        | Product/session                      | User choice                      |
| Fits current design    | Yes (ProductCode ≈ WeighingMode)    | Yes (auth is entry point)        |
| No appsettings         | Yes                                  | Yes                              |

- Prefer **Option A** if the default weighing mode should follow the **product** (e.g. “this installation is for SolidWaste product, so default is SolidWaste”). Then remove bootstrap and set default from `UserSession.ProductId` after login (with a clear “when to set” rule).
- Prefer **Option B** if the default should be a **user choice** at auth/first run. Then remove bootstrap and add the default-weighing-mode control to AuthCodeWindow (or a one-time dialog) and save on confirm.

## 5. Steps to Remove the Temporary Code (common to both options)

1. **Delete** in `MaterialClientModule.cs` the entire block from the comment `// TEMP(2026-01-19): Bootstrap ...` through the end of the `catch` (lines 216–260).
2. **Optional**: Remove `SystemSettings:DefaultWeighingMode` from `appsettings.json` (line 47–49) so no deployment default is read at all.
3. **Implement** either Option A or B as above.
4. **Verify**: Fresh install (or cleared settings) gets a correct default via session or auth UI; existing installs keep their current `DefaultWeighingMode` unless the new logic intentionally updates it.

## 6. Summary

- **Remove**: The temporary bootstrap in `MaterialClientModule.cs` (222–254) and optionally the appsettings key.
- **Replace with**:
  - **UserSession (ProductId)**: Map ProductId → WeighingMode and set `SystemSettings.DefaultWeighingMode` after login (with a defined “when” rule).
  - **AuthCodeWindow**: Add default weighing mode selection in the auth UI and save to settings on success/first run.

Choosing between A and B depends on whether the default should be **product-driven** (A) or **user-driven** (B).
