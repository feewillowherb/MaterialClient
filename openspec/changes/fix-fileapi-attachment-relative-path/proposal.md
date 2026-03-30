## Why

当前应用在开机自启（例如工作目录可能是 `C:\Windows\System32`）时，会出现“数据库/配置中保存的相对路径”被直接传入 `System.IO.File.*`（如 `File.Exists`、`File.Delete`、`FileStream`、以及 OSS 上传里的本地文件路径）导致路径解析到错误目录的问题。
这会造成附件同步到 OSS 失败、图片/预览相关文件被误判不存在，最终表现为附件链路不稳定。

## What Changes

- 引入统一的路径归一化能力：在任何进行本地文件 I/O（`File.*`、`FileStream`、以及需要本地路径的第三方 API，如 OSS `PutObject`）之前，把“相对路径”转换为“基于应用目录的绝对路径”。
- 更新附件链路关键代码，避免对 `AttachmentFile.LocalPath` 直接调用 `File.Exists`/`FileInfo`/上传 API：
  - `MaterialClient.Common/Services/AttachmentService.cs`：在同步到 OSS 时，把 `attachment.LocalPath` 归一化为绝对路径后再做 `File.Exists` 与上传前的文件信息读取。
  - `MaterialClient.Common/Services/OssUploadService.cs`：在上传前把本地路径归一化为绝对路径；上传失败日志同时记录归一化后的绝对路径。
- 更新打印/预览等场景中涉及本地图片文件的路径使用方式：
  - `MaterialClient.Common/Services/Hardware/TicketPrintingService.cs`：对 `imagePath` 进行相对路径归一化。
  - `MaterialClient\ViewModels/PrintPreviewViewModel.cs`：对 `PreviewImagePath` 在 `File.Exists`/`File.Delete` 前进行归一化。
- 为以上能力添加单元/集成层面的回归保障（重点覆盖“相对路径在 System32 下仍能正确命中”的场景），确保未来不会再引入新的 `File.*` 相对路径用法。

## Capabilities

### New Capabilities

- `file-api-relative-path-normalization`: 任何 `File API`/本地 I/O 在执行前都必须进行“相对路径 -> 应用目录绝对路径”的归一化，确保开机自启或任务计划程序环境下行为一致。

### Modified Capabilities

<!-- existing requirements modified at spec level (if any) -->

## Impact

- 受影响代码：`AttachmentService`、`OssUploadService`、`TicketPrintingService`、`PrintPreviewViewModel` 以及其他使用 `File.*` 的路径入口（仅限“输入可能是相对路径”的场景）。
- 行为变化：相对路径将不再依赖当前工作目录；所有本地文件 I/O 将基于应用目录解析。
- 依赖变化：无新增外部依赖；实现层可能复用现有的路径工具（例如 `AttachmentPathUtils`/`PathManager`）提供一致的归一化规则。

