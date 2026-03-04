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

### 新增能力
无（本地化工作不增加新功能）

### 修改的能力
无（无规范层面的行为变更）

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

## 代码变更表

| 文件路径 | 变更类型 | 变更原因 | 影响范围 |
|-----------|-------------|---------------|--------------|
| `**/*.cs` | 更新注释 | 将英文注释翻译为中文 | 代码可读性 |
| `docs/**/*.md` | 翻译内容 | 将 Markdown 文档翻译为中文 | 文档可访问性 |
| `MaterialClient.csproj` | 更新元数据 | 将项目描述改为中文 | 项目元数据 |
| `MaterialClient.Common.csproj` | 更新元数据 | 将项目描述改为中文 | 项目元数据 |
| `MaterialClient.Toolkit.csproj` | 更新元数据 | 将项目描述改为中文 | 项目元数据 |
