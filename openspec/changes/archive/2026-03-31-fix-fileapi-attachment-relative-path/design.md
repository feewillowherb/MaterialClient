## Context

在开机自启或任务计划程序触发时，应用进程的工作目录可能不是可执行文件所在目录（常见为 `C:\Windows\System32`）。
当前代码中存在把“数据库/配置存储的相对路径”（例如附件 `AttachmentFile.LocalPath`、预览图 `PreviewImagePath`、打印输入图片 `imagePath` 等）直接传给 `System.IO.File.*`、`FileStream` 或第三方文件 I/O（如 OSS `PutObject`）的情况。
当工作目录是 `System32` 时，相对路径会被解析到错误位置，从而导致：
- 附件同步到 OSS 时判断文件不存在（`File.Exists` 为 false）并跳过上传
- 文件信息读取失败（例如 file size）
- 预览/打印相关的图片文件无法被正确删除/渲染/打印

目前项目里已有 `PathManager` 以及部分 UI Converter 使用了相对路径归一化，但附件上传/同步链路仍存在直传路径给 File API 的场景。

## Goals / Non-Goals

**Goals:**

- 确保任何进入“本地文件 I/O”环节的路径，在执行 `File.Exists/File.Delete/File.OpenRead/FileStream/第三方上传 PutObject` 等操作前，统一转换为绝对路径（避免依赖工作目录）。
- 将归一化逻辑集中在一个可复用的附件/路径工具上，降低后续回归风险。
- 保持对已是绝对路径的兼容：绝对路径不应被二次拼接或改变含义。

**Non-Goals:**

- 不改变数据库中 `LocalPath` 的存储策略（仍保持相对路径）。
- 不引入新的外部依赖。
- 不做大规模架构重构；仅修复已识别的 File API 相对路径用法点。

## Decisions

### 归一化的实现位置

选择在 `MaterialClient.Common/Utils/AttachmentPathUtils.cs` 提供“相对路径归一化”能力，并在所有需要本地文件 I/O 的调用点使用它。

- 原因：
  - 用户关心的文件链路均与附件/图片相关，集中在 `AttachmentPathUtils` 更贴合业务语义。
  - 项目已有 `PathManager` 做了类似工作，但当前要求是把风险用法“改为用 AttachmentPathUtils”。
  - `AttachmentPathUtils` 内部可复用 `PathManager` 的策略（基于 `AppContext.BaseDirectory`），避免逻辑重复和语义漂移。

### 归一化规则

采用如下规则，确保行为在各种工作目录下稳定：

1. 当传入路径为空/空白：返回原值或按调用点约定跳过（由调用点负责）。
2. 当传入路径已为绝对路径（`Path.IsPathRooted`）：直接返回原值。
3. 当传入路径为相对路径：用 `AppContext.BaseDirectory` 进行拼接并返回绝对路径。

### 归一化的调用时机

在进入任何“本地文件操作”的边界点前归一化：

- `File.Exists` 前归一化
- `FileInfo` / `FileStream` / `Image.FromFile` 前归一化
- OSS 上传调用 `PutObject(bucket, key, localPath)` 前归一化

并尽量只在“路径可能来自 DB/config/用户输入且不保证绝对”的场景替换。

## Risks / Trade-offs

- [Risk] 归一化后可能改变某些“原本希望相对当前工作目录”的路径解析语义。
  - [Mitigation] 只替换已定位到的路径来源（附件 `LocalPath`、打印/预览输入图片路径等），避免全局重写未知场景。

- [Risk] 部分调用方传入的路径可能并不属于应用目录（例如用户自定义外部路径）。
  - [Mitigation] 使用“绝对路径不改变”的规则；相对路径才基于 `AppContext.BaseDirectory` 拼接。

- [Risk] 数据库里可能存在历史数据（绝对路径或不同相对前缀）。
  - [Mitigation] 归一化对绝对路径保持不变；对相对路径则统一基于 `AppContext.BaseDirectory`，同时在上传/同步失败日志中打印归一化前后路径以便排查。

## Migration Plan

无显式数据迁移。
- 已存在数据库里相对路径：归一化会在运行时修复。
- 已存在数据库里绝对路径：归一化保持原值不改变。

## Open Questions

1. `AttachmentFile.LocalPath` 的“相对路径前缀集合”是否完全覆盖 `AttachmentPathUtils`/`PathManager` 的拼接逻辑（例如存在 `PhotoPiaoJu/...`、`Photos/...` 的不同历史命名）。
2. 是否存在其他未被本次调研覆盖的 `File.*` 使用点，其入参同样来自 `LocalPath/PreviewImagePath` 等字段。

