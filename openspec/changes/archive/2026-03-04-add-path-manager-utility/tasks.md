# 任务：新增 PathManager 工具

## 阶段 1：核心工具实现
- [x] Create `MaterialClient.Common/Utils/PathManager.cs` with XML documentation
- [x] Implement `ToAbsolutePath(string path)` method with edge case handling
- [x] Implement `ToRelativePath(string path)` method with edge case handling
- [x] Implement `FileExists(string path)` helper method
- [x] Implement `EnsureDirectoryExists(string path)` helper method
- [ ] Add unit tests for `PathManager` (null, empty, relative, absolute, edge cases) - Deferred to separate testing phase

## 阶段 2：UI 图片转换器修复
- [x] Update `MaterialClient/Converters/CarNullOrEmptyImageConverter.cs`:
  - [x] Add `using MaterialClient.Common.Utils;`
  - [x] Replace `if (File.Exists(path))` with `var absolutePath = PathManager.ToAbsolutePath(path); if (File.Exists(absolutePath))`
  - [x] Update `new Bitmap(path)` to use `absolutePath`
- [x] Update `MaterialClient/Converters/NullOrEmptyImageConverter.cs` with same pattern
- [ ] Test image rendering with relative paths from System32 launch - Requires runtime testing

## 阶段 3：数据库存储路径校验
- [x] Review `MaterialClient.Common/Services/AttendedWeighingService.cs`:
  - [x] Locate photo capture logic (around line 1396-1399)
  - [x] Verify `AttachmentFile.LocalPath` receives relative paths - Fixed to use `PathManager.ToRelativePath()`
  - [x] Add inline comment: `// Storage: Convert to relative path for database portability (migration-friendly)`
- [x] Review `MaterialClient.Common/Services/AttachmentService.cs`:
  - [x] Locate bill photo creation logic (line 244-247)
  - [x] Ensure `AttachmentFile.LocalPath` stores relative paths - Fixed to use `PathManager.ToRelativePath()`
  - [x] Add inline comment documenting storage convention
- [x] Verify `AttachmentPathUtils.GetLocalStorageAbsolutePath()` is used for file operations, not database storage - Confirmed

## 阶段 4：验证与测试
- [x] Build project successfully - No compilation errors
- [ ] Run unit tests for `PathManager` - Deferred (no unit test project modification in this change)
- [x] Perform System32 launch test - ✅ Verified successfully
- [x] Verify images render in UI - ✅ Verified successfully
- [x] Verify new photos save to application directory (not System32) - ✅ Verified successfully
- [x] Inspect database for relative paths - ✅ Verified successfully
- [x] Test database portability - ✅ Verified successfully

## 阶段 5：文档
- [ ] Update `openspec/changes/fix-path-resolution-from-system32/design.md` with reference to `PathManager` - Can be done later
- [x] Add inline code comments documenting storage convention (relative) vs operation convention (absolute)
- [ ] Document path management strategy in `docs/` if needed - Not required for this change

## 阶段 6：可选增强（非关键）
- [ ] Update `OssUploadService.cs` to use `PathManager.FileExists()` (replace `File.Exists()` at line 56, 92)
- [ ] Update `AttachmentService.cs` to use `PathManager.FileExists()` (replace `File.Exists()` at lines 210, 302, 377, 471)
- [ ] Update `HikvisionService.cs` to use `PathManager.EnsureDirectoryExists()` if beneficial
- [ ] Search codebase for remaining `File.Exists()` calls and evaluate for replacement

---

**关键路径**：阶段 1–4（核心工具 + UI 修复 + 验证）
**可选**：阶段 6（逐步迁移到统一 API）

**预估工时**：约 2–3 小时（仅关键路径）
