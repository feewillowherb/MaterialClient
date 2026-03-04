# 翻译清单

本文提供中文本地化变更所需的全部待翻译文件清单。

## 待翻译文件

### Markdown 文档（12 个文件）

#### `docs/` 目录下的文档（10 个）
- `docs/SDD.md` - 软件设计文档
- `docs/existing-docs-inventory.md` - 文档清单
- `docs/design-creatable-pageable-searchable-selection.md` - 设计文档
- `docs/evaluation-generic-selection-popup-merge-search-and-trigger.md` - 评估文档
- `docs/evaluation-photo-grid-image-loading-performance.md` - 评估文档
- `docs/evaluation-remove-defaultweighingmode-bootstrap.md` - 评估文档
- `docs/popup-selection-analysis.md` - 分析文档
- `docs/proposal-creatable-pageable-searchable-selection.md` - 提案文档
- `docs/sdd-creation-summary.md` - 创建总结
- `docs/sdd-gap-analysis.md` - 差距分析
- `docs/sdd-maintenance-guide.md` - 维护指南
- `docs/sdd-quality-assessment.md` - 质量评估

#### 根目录 Markdown 文件（1 个）
- `HikLpr_OpenSpec_Proposal.md` - OpenSpec 提案文档
- 说明：`CLAUDE.md` 应保持英文，因其包含系统指令

### C# 源文件（256 个）

#### MaterialClient 项目（160 个）
- 位于 `MaterialClient/` 目录
- 需将注释翻译为中文

#### MaterialClient.Common 项目（72 个）
- 位于 `MaterialClient.Common/` 目录
- 需将注释翻译为中文

#### MaterialClient.Toolkit 项目（24 个）
- 位于 `MaterialClient.Toolkit/` 目录
- 需将注释翻译为中文

### 项目文件（3 个）

#### 需更新元数据的 .csproj
- `MaterialClient/MaterialClient.csproj` - 将描述更新为中文
- `MaterialClient.Common/MaterialClient.Common.csproj` - 将描述更新为中文
- `MaterialClient.Toolkit/MaterialClient.Toolkit.csproj` - 将描述更新为中文

## 排除文件

以下文件**不**参与翻译：

### OpenSpec 系统文件（必须保持英文）
- `openspec/specs/**/*.md` - 所有规范文档
- `openspec/changes/**/*.md` - 所有变更文档（proposal.md、tasks.md、design.md）
- `CLAUDE.md` - 系统指令文件

### 第三方与构建
- `.cursor/` 目录 - IDE 配置
- `.specify/` 目录 - AI 助手模板
- `archive/` 目录 - 归档旧文档
- 所有 `.json`、`.xml`、`.config`、`.editorconfig`、`.gitignore` 及 `.axaml`、`.axaml.cs` 等（本变更范围外）

## 翻译统计

- **待翻译文件总数**：271 个（Markdown 12、C# 256、项目 3）
- **待翻译注释行数**：约 15,000+ 行
- **预估工作量**：20–30 小时人工翻译

## 优先级

### 高优先级（关键）
- `docs/SDD.md`、`docs/existing-docs-inventory.md`、各 .csproj 项目元数据

### 中优先级（重要）
- `docs/` 下所有文档、根目录 Markdown、MaterialClient 与 MaterialClient.Common 的代码注释

### 低优先级（可选）
- MaterialClient.Toolkit 代码注释、评估与分析类文档

## 说明

1. 代码本身（变量名、方法名、类名）必须保持英文
2. 技术与专业术语应保留英文备注（如 API、HTTP、REST、JSON、XML）
3. 所有 OpenSpec 规范文档按系统要求保持英文
4. 翻译须保持原意与技术准确性
5. 代码注释应为中文开发者提供清晰指导
