## 1. Implementation

### 1.1 Create WindowsAutoStartService

- [x] Create `MaterialClient.Common/Services/WindowsAutoStartService.cs`
- [x] Define interface `IWindowsAutoStartService` with methods:
  - `Task EnableAutoStartAsync()`
  - `Task DisableAutoStartAsync()`
  - `Task<bool> IsAutoStartEnabledAsync()`
- [x] Implement registry operations using `Microsoft.Win32.Registry`
- [x] Registry path: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
- [x] Registry value name: Application executable name
- [x] Registry value data: Full path to executable
- [x] Add error handling for registry permission errors
- [x] Add logging for registry operations
- [x] Register service in DI container (via `ITransientDependency` - ABP auto-registration)

### 1.2 Integrate with SettingsService

- [x] Modify `SettingsService.SaveSettingsAsync()`:
  - After saving settings to database, check `SystemSettings.EnableAutoStart`
  - If `true`, call `IWindowsAutoStartService.EnableAutoStartAsync()`
  - If `false`, call `IWindowsAutoStartService.DisableAutoStartAsync()`
- [x] Handle exceptions gracefully (log warning, don't fail save operation)
- [ ] Add unit tests for integration

### 1.3 Add Startup Synchronization

- [x] Create method `SyncAutoStartOnStartupAsync()` in `StartupService` or `App.axaml.cs`
- [x] On application startup (after ABP initialization):
  - Load settings from database via `ISettingsService.GetSettingsAsync()`
  - Check registry state via `IWindowsAutoStartService.IsAutoStartEnabledAsync()`
  - Compare database setting with registry state
  - If inconsistent, repair by applying database setting to registry
- [x] Log inconsistencies for troubleshooting
- [x] Do not block application startup if sync fails

### 1.4 Update SettingsWindowViewModel (Optional Enhancement)

- [ ] In `LoadSettingsAsync()`, optionally verify registry state matches database
- [ ] If mismatch detected, log warning (but don't auto-repair here - let startup sync handle it)
- [ ] This ensures UI shows correct state
- **Note**: Skipped for now - startup sync provides sufficient consistency. Can be added later if needed.

## 2. Testing

### 2.1 Unit Tests

- [ ] Create `WindowsAutoStartServiceTests.cs`
- [ ] Mock `RegistryKey` for testing
- [ ] Test `EnableAutoStartAsync()` - verifies registry write
- [ ] Test `DisableAutoStartAsync()` - verifies registry deletion
- [ ] Test `IsAutoStartEnabledAsync()` - verifies registry read
- [ ] Test error handling (permission denied, registry unavailable)
- [ ] Test with different executable paths

### 2.2 Integration Tests

- [ ] Create integration test for full flow:
  - Save settings with `EnableAutoStart = true`
  - Verify registry entry created
  - Save settings with `EnableAutoStart = false`
  - Verify registry entry removed
- [ ] Test startup synchronization:
  - Set database to enabled, registry disabled
  - Start application
  - Verify registry entry created
- [ ] Test on actual Windows system (not just mocked)

### 2.3 Manual Testing

- [ ] Enable auto-start in settings, verify registry entry
- [ ] Disable auto-start in settings, verify registry entry removed
- [ ] Manually delete registry entry, restart app, verify repair
- [ ] Test with insufficient registry permissions (if possible)
- [ ] Verify application actually starts on Windows boot

## 3. Documentation

- [ ] Update `openspec/docs/system-configuration.md` with auto-start details
- [ ] Add code comments explaining registry operations
- [ ] Document error handling strategy
- [ ] Add troubleshooting guide for registry permission issues

## 4. Validation

- [ ] Run `openspec validate implement-windows-auto-start --strict`
- [ ] Ensure all tests pass
- [ ] Verify no breaking changes
- [ ] Check code coverage for new service