## 1. Path utility enhancements

- [x] 1.1 在 `MaterialClient.Common/Utils/AttachmentPathUtils.cs` 增加“相对路径 -> 基于 `AppContext.BaseDirectory` 的绝对路径”归一化方法（例如 `ToAbsolutePath(...)`）
- [x] 1.2 在 `AttachmentPathUtils` 内补充必要的便捷封装（例如 `FileExists(...)` 或归一化后再调用 `File.Exists` 的内部逻辑），确保空/空白路径的行为可控

## 2. Attachment sync & upload fixes

- [x] 2.1 更新 `MaterialClient.Common/Services/AttachmentService.cs`：在 `SyncWaybillAttachmentsToOssAsync` 中对 `attachment.LocalPath` 先归一化再执行 `File.Exists` 与文件信息读取
- [x] 2.2 更新 `MaterialClient.Common/Services/AttachmentService.cs`：在 `SyncPendingAttachmentsToOssAsync` 中对 `AttachmentFile.LocalPath` 先归一化再执行 `File.Exists`
- [x] 2.3 更新 `MaterialClient.Common/Services/AttachmentService.cs`：在 `UploadAttachmentInfoToServerAsync` 中对 `attachment.LocalPath` 先归一化再执行 `File.Exists` 与 `FileInfo.Length`
- [x] 2.4 更新 `MaterialClient.Common/Services/OssUploadService.cs`：在 `UploadFileAsync` 中归一化 `localPath` 后再判断存在性与调用 `_ossClient.PutObject(...)`
- [x] 2.5 更新 `MaterialClient.Common/Services/OssUploadService.cs`：在 `UploadFilesAsync` 中归一化 `item.Attachment.LocalPath` 后再判断存在性与调用 `_ossClient.PutObject(...)`

## 3. Print/preview file ops normalization

- [x] 3.1 更新 `MaterialClient.Common/Services/Hardware/TicketPrintingService.cs`：在 `PrintImageToPdf(...) / PrintImage(...)` 中对 `imagePath` 先归一化再做 `File.Exists` 与 `Image.FromFile`
- [x] 3.2 更新 `MaterialClient/ViewModels/PrintPreviewViewModel.cs`：在 `Dispose()` 中对 `PreviewImagePath` 先归一化再做 `File.Exists` 与 `File.Delete`

## 4. Regression coverage & verification

- [x] 4.1 编写单元测试覆盖 `AttachmentPathUtils` 归一化规则（相对路径转绝对、绝对不变、空/空白输入的返回策略）
- [x] 4.2 通过日志/断言方式验证：开机自启场景下（工作目录为 `System32`）相对 `LocalPath` 不再导致附件跳过上传（在可测环境中至少验证路径归一化发生）
- [x] 4.3 运行现有构建/测试流程，确认无回归

## 5. Audit remaining File API usage

- [x] 5.1 对仓库内剩余 `File.Exists/File.Delete/File.OpenRead/FileStream` 调用点做一次审计：仅保留已确认传入为绝对路径的点；对“可能收到相对路径”的点继续应用归一化封装

