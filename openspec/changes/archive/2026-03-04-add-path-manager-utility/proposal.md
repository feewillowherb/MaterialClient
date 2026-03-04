# Change: Add PathManager Utility for Unified Path Management

## Why

**Current State**: The existing `fix-path-resolution-from-system32` change addressed critical path resolution issues by using absolute paths for file operations. However, the implementation has three gaps:

1. **No bidirectional path conversion**: Current utilities (`DatabaseConnectionStringFactory`, `AttachmentPathUtils.GetLocalStorageAbsolutePath`) only convert relative→absolute. There's no standardized way to convert absolute→relative for database storage.

2. **UI image loading still broken**: Image converters (`CarNullOrEmptyImageConverter`, `NullOrEmptyImageConverter`) call `File.Exists(path)` directly without path normalization. When the app starts from System32 and reads relative paths from the database, images fail to render.

3. **Scattered file operation logic**: File existence checks, directory creation, and path resolution are duplicated across multiple services without a unified abstraction.

**Problem Impact**:
- **Database portability at risk**: Services may inadvertently save absolute paths (e.g., `D:\MaterialClient\Photos\car.jpg`) instead of relative paths (e.g., `Photos/car.jpg`), breaking database migration scenarios
- **Images not rendering**: When database contains relative paths (correct for portability) and app starts from System32, UI converters fail to load images
- **Maintenance burden**: Each service must remember to normalize paths, leading to inconsistencies

**Enterprise Path Management Best Practice** (learned from VS Code, Electron):
- **Storage Layer** (database/config): Always use relative paths for portability
- **Runtime Layer** (file I/O): Always use absolute paths based on `AppContext.BaseDirectory`
- **Conversion Layer**: Centralized utility to transform between the two

## What Changes

Add `PathManager` utility class to `MaterialClient.Common/Utils/` providing:

1. **Core Path Conversion**:
   - `ToAbsolutePath(string path)`: Convert relative→absolute for file operations
   - `ToRelativePath(string path)`: Convert absolute→relative for database storage
   - Both methods handle edge cases (null, empty, already-converted paths)

2. **File Operation Helpers**:
   - `FileExists(string path)`: Safe existence check with automatic path normalization
   - `EnsureDirectoryExists(string path)`: Create directory with normalized path

3. **UI Converter Fixes**:
   - Update `CarNullOrEmptyImageConverter` to use `PathManager.ToAbsolutePath()`
   - Update `NullOrEmptyImageConverter` to use `PathManager.ToAbsolutePath()`

4. **Service Path Storage Validation**:
   - Review `AttendedWeighingService` and `AttendedWeighingViewModel` to ensure `AttachmentFile.LocalPath` stores relative paths
   - Add inline comments documenting the storage convention

**Design Principles**:
- ✅ Simple binary strategy: relative for storage, absolute for I/O
- ✅ Zero dependencies on working directory (`Directory.GetCurrentDirectory()`)
- ✅ Based on `AppContext.BaseDirectory` (immutable, process-level constant)
- ✅ VS Code-inspired pattern (proven in enterprise applications)

## Impact

### Affected Specs
- `attended-weighing` - Photo capture, storage, and display behavior

### Affected Code

**New Files**:
- `MaterialClient.Common/Utils/PathManager.cs` - New utility class

**Modified Files**:
- `MaterialClient/Converters/CarNullOrEmptyImageConverter.cs` - Add path normalization before `File.Exists()` and `Bitmap()` loading
- `MaterialClient/Converters/NullOrEmptyImageConverter.cs` - Add path normalization before `File.Exists()` and `Bitmap()` loading
- `MaterialClient.Common/Services/AttendedWeighingService.cs` - Add validation that `AttachmentFile.LocalPath` receives relative paths
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs` - Add validation that `BillPhotoPath` is converted to relative before database storage

**Optional Enhancements** (not in critical path):
- `MaterialClient.Common/Services/OssUploadService.cs` - Use `PathManager.FileExists()` instead of `File.Exists()`
- `MaterialClient.Common/Services/AttachmentService.cs` - Use `PathManager.FileExists()` instead of `File.Exists()`

### Relationship to Existing Changes

**Builds on**: `fix-path-resolution-from-system32`
- Reuses existing `AttachmentPathUtils.GetLocalStorageAbsolutePath()` (generates absolute paths for file operations)
- Reuses existing `DatabaseConnectionStringFactory.FixConnectionString()` (database path resolution)
- **Complements** with bidirectional conversion and UI fixes

**Dependency**: This change can be implemented independently but logically follows `fix-path-resolution-from-system32`

### Breaking Changes

None - This is a bug fix and architectural improvement.

### Migration

**Automatic**: 
- If database already contains relative paths (expected): Images will start rendering immediately
- If database contains absolute paths (edge case from manual testing): Paths will still work, but won't be portable until next photo capture

### Verification Strategy

1. **System32 Launch Test**:
   ```powershell
   cd C:\Windows\System32
   D:\MaterialClient\MaterialClient.exe
   ```
   - ✅ Images should render in UI
   - ✅ New photos should be saved to `D:\MaterialClient\Photos\...`, not `C:\Windows\System32\Photos\...`

2. **Database Path Inspection**:
   ```sql
   SELECT LocalPath FROM AttachmentFiles LIMIT 10;
   -- Expected: Photos/2026/01/23/car.jpg (relative)
   -- Not: D:\MaterialClient\Photos\2026\01\23\car.jpg (absolute)
   ```

3. **Portability Test**:
   - Copy `MaterialClient.db` and `Photos/` folder to different directory
   - Launch app from new location
   - ✅ Existing images should still load

## Success Criteria

1. **UI image rendering works from System32**: Converters successfully load images when app starts from any working directory
2. **Database stores relative paths**: All new `AttachmentFile.LocalPath` entries use relative paths
3. **Unified path API**: Services use `PathManager` methods instead of direct `File.Exists()` calls
4. **Zero working directory dependency**: All file operations use `AppContext.BaseDirectory` as anchor
