# 变更：新增 PathManager 工具以统一路径管理

## 原因

**当前状态**：既有变更 `fix-path-resolution-from-system32` 通过为文件操作使用绝对路径解决了关键路径解析问题，但实现仍存在三处缺口：

1. **无双向路径转换**：当前工具（`DatabaseConnectionStringFactory`、`AttachmentPathUtils.GetLocalStorageAbsolutePath`）仅做相对→绝对转换，缺少用于数据库存储的绝对→相对标准方式。  
2. **UI 图片加载仍异常**：图片转换器（`CarNullOrEmptyImageConverter`、`NullOrEmptyImageConverter`）直接调用 `File.Exists(path)` 且未做路径规范化，从 System32 启动并从数据库读取相对路径时图片无法渲染。  
3. **文件操作逻辑分散**：文件存在检查、目录创建与路径解析在多个服务中重复且无统一抽象。

**问题影响**：数据库可移植性风险（可能误存绝对路径）；从 System32 启动时含相对路径的数据库下图片不显示；各服务须自行规范化路径，易不一致。

**企业路径管理最佳实践**（参考 VS Code、Electron）：**存储层**（数据库/配置）始终使用相对路径；**运行时层**（文件 I/O）始终使用基于 `AppContext.BaseDirectory` 的绝对路径；**转换层**由集中工具在两者间转换。

## 变更内容

在 `MaterialClient.Common/Utils/` 下新增 `PathManager` 工具类，提供：

1. **核心路径转换**：`ToAbsolutePath(string path)`（相对→绝对，用于文件操作）、`ToRelativePath(string path)`（绝对→相对，用于数据库存储），二者处理 null、空、已转换等边界。  
2. **文件操作辅助**：`FileExists(string path)`（带自动规范化的存在检查）、`EnsureDirectoryExists(string path)`（使用规范化路径创建目录）。  
3. **UI 转换器修复**：在 `CarNullOrEmptyImageConverter` 与 `NullOrEmptyImageConverter` 中使用 `PathManager.ToAbsolutePath()`。  
4. **服务路径存储校验**：审阅 `AttendedWeighingService` 与 `AttendedWeighingViewModel`，确保 `AttachmentFile.LocalPath` 存相对路径，并增加说明存储约定的行内注释。

**设计原则**：简单二元策略（存储用相对、I/O 用绝对）；不依赖工作目录；基于 `AppContext.BaseDirectory`；借鉴 VS Code 的成熟模式。

## 影响

**受影响规范**：`attended-weighing`（照片采集、存储与展示）。  
**新增文件**：`MaterialClient.Common/Utils/PathManager.cs`。  
**修改文件**：CarNullOrEmptyImageConverter、NullOrEmptyImageConverter（在 File.Exists/Bitmap 前做路径规范化）；AttendedWeighingService、AttendedWeighingViewModel（校验 LocalPath/BillPhotoPath 为相对路径并加注释）。  
**可选增强**：OssUploadService、AttachmentService 改用 `PathManager.FileExists()`。

**与现有变更关系**：基于 `fix-path-resolution-from-system32`，复用既有绝对路径与数据库路径解析，本变更补充双向转换与 UI 修复。可独立实现，逻辑上接续该变更。

**破坏性变更**：无。  
**迁移**：若数据库已是相对路径则图片会立即正常显示；若含绝对路径（手工测试遗留）仍可用，但下次拍照前不可移植。  
**验证策略**：从 System32 启动并检查图片显示与新照片保存位置；检查数据库中 LocalPath 为相对路径；复制 db 与 Photos 到新目录验证可移植性。

## 成功标准

1. 从 System32 启动时 UI 图片能正确加载  
2. 数据库中新 `AttachmentFile.LocalPath` 均为相对路径  
3. 服务使用 PathManager 方法而非直接 `File.Exists()`  
4. 所有文件操作以 `AppContext.BaseDirectory` 为锚点，不依赖工作目录
