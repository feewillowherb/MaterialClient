## ADDED Requirements

### Requirement: Normalize local relative paths before File API calls
The system SHALL normalize any local file path that is not absolute (relative path) before using it in `System.IO.File.*`/`System.IO.Directory.*` or before passing it to third-party local-file I/O (e.g., OSS `PutObject`).

The normalization SHALL use `MaterialClient.Common/Utils/AttachmentPathUtils` (directly or via an equivalent method) and MUST base the absolute result on the application directory (`AppContext.BaseDirectory`).

If the input path is already absolute, the normalization MUST keep it unchanged.

#### Scenario: Attachment sync works from System32
- **WHEN** the application auto-starts with a working directory such as `C:\Windows\System32` and `AttachmentFile.LocalPath` contains a relative local path
- **THEN** `AttachmentService` selects the correct existing local file via `File.Exists` and OSS upload uses the normalized absolute path (file upload no longer skips due to wrong working directory)

#### Scenario: Print preview file deletion is robust
- **WHEN** `PrintPreviewViewModel.Dispose()` receives a `PreviewImagePath` that is a relative local path
- **THEN** deletion occurs against the normalized absolute path and the operation does not fail due to a wrong working directory

#### Scenario: Absolute path stays unchanged
- **WHEN** the caller passes an absolute path to the normalization step
- **THEN** the system uses the same path value for `File.Exists`/uploads without any modification

