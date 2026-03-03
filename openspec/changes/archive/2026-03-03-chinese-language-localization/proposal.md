## Why

项目当前语言设置不统一，项目描述使用英文，代码注释可能使用英文，用户界面和文档存在语言不一致的情况，需要双语维护导致维护成本较高。统一语言为中文可以提高项目可读性，降低中文用户的理解门槛。

## What Changes

- 统一项目主语言为中文
- 翻译所有 Markdown 文档为中文（除 OpenSpec 规范文档外）
- 翻译所有代码注释为中文（代码内容本身保持英文）
  - **技术性和专业术语保留英文备注**（如 API、HTTP、REST、JSON 等技术术语）
- 更新项目描述为中文
- 确保项目整体语言一致性

## Capabilities

### New Capabilities
None (localization work without new functional capabilities)

### Modified Capabilities
None (no specification-level behavior changes)

## Impact

**Affected Documentation**:
- `docs/` 目录下所有 Markdown 文件
- 项目根目录下的 Markdown 文件（`CLAUDE.md` 除外）
- 代码注释（MaterialClient 项目下的 .cs 文件）
- 项目描述（.csproj 文件中的描述信息）

**Affected Systems**:
- MaterialClient 应用程序（UI 文本）
- MaterialClient.Common 共享库（注释和文档）
- MaterialClient.Toolkit 工具库（注释和文档）

**Note**: OpenSpec 规范文档（`openspec/specs/**/spec.md`, `openspec/changes/**/proposal.md`, `tasks.md`, `design.md`）必须保持英文，这是 OpenSpec 系统的非协商性要求。

---

## Code Change Table

| File Path | Change Type | Change Reason | Impact Scope |
|-----------|-------------|---------------|--------------|
| `**/*.cs` | Update comments | Translate English comments to Chinese | Code readability |
| `docs/**/*.md` | Translate content | Translate Markdown documentation to Chinese | Documentation accessibility |
| `MaterialClient.csproj` | Update metadata | Change project description to Chinese | Project metadata |
| `MaterialClient.Common.csproj` | Update metadata | Change project description to Chinese | Project metadata |
| `MaterialClient.Toolkit.csproj` | Update metadata | Change project description to Chinese | Project metadata |
