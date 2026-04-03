# MaterialClient：OpenSpec 开发手册与项目解读

**文档类型**：会话分析报告  
**日期**：2026-04-03  
**适用范围**：本仓库开发者与评审者  

---

## 摘要

本文档汇总两方面内容：**如何使用 OpenSpec 在本仓库中规范开发**，以及 **MaterialClient 的项目结构与可读性要点**。阅读顺序建议：先通读第二节（OpenSpec）与第三节（项目结构）。

---

## 1. 文档与仓库中的权威来源

| 主题 | 推荐阅读路径 |
|------|----------------|
| 变更提案与流程 | `openspec/changes/README.md` |
| 项目级背景与约束（AI/人共读） | `openspec/project.md` |
| 能力规格（长期沉淀的需求） | `openspec/specs/<capability>/spec.md` |
| 软件设计总览 | `docs/SDD.md` |
| 历史技术报告索引 | `docs/existing-docs-inventory.md` |
| Cursor/Claude 技能（工作流） | `.claude/skills/openspec-*.md` |
| Agent 探索产出（可选） | `docs/`（见 §2.3-A） |

---

## 2. 如何使用 OpenSpec 开发 MaterialClient（开发手册）

### 2.1 OpenSpec 在本项目中的角色

OpenSpec 将「需求 → 设计 → 任务 → 实现 → 归档」串成可追溯工作流。仓库内：

- **`openspec/changes/<change-id>/`**：进行中的变更，通常包含 `proposal.md`（做什么、为什么）、可选 `design.md`（怎么做）、`tasks.md`（可勾选任务）。
- **`openspec/changes/archive/`**：已完成并归档的变更（目录名常带日期前缀，如 `2026-04-02-lockedat-plate-locking`）。
- **`openspec/specs/`**：与业务能力对应的**主规格**，变更中的 delta 规格在归档时可合并回此处（视团队流程而定）。

项目 README 中描述的流程概要：**创建 → 设计（可选）→ 任务拆解 → 评审 → 实施 → 归档**（见 `openspec/changes/README.md`）。

### 2.2 前置条件

- 安装 **openspec CLI**（技能文档标注为 *Requires openspec CLI*）。在终端中应能执行 `openspec list`、`openspec status`、`openspec new change` 等命令。
- 熟悉本仓库约定：分层架构、ABP + EF Core + SQLite、ReactiveUI/Rx 等（见 `openspec/project.md` 与 `docs/SDD.md`）。

### 2.3 典型工作流（与仓库技能对应）

以下与 `.claude/skills/` 中的 OpenSpec 技能一致，人工开发时也可按相同逻辑执行。

**A. 探索阶段（想清楚再动手）**

- 使用 **openspec-explore** 心态：以讨论、读代码、画草图为主，**不写业务实现代码**；需要时可整理提案/规格。
- 除上述技能流外，也可在 **Agent 模式**下将探索结论整理成独立文档，保存到仓库的 **`docs/`** 目录（例如一次会话的需求梳理、方案对比、风险列表）。该类文档与 OpenSpec 变更目录相互独立，便于评审前传阅与后续检索。
- 探索产出放在 `docs/` 时，建议在文件名中体现主题或日期（如 `explore-gate-io-session-2026-04-03.md`），与同目录下的 `SDD.md`、评估类报告等区分，便于检索。
- 可先执行 `openspec list --json` 查看是否已有进行中的变更，避免重复立项。

**B. 立项与一次性产出提案包（openspec-propose）**

1. 确定变更的 **kebab-case** 名称（例如 `add-user-auth`）。
2. `openspec new change "<name>"` 生成 `openspec/changes/<name>/` 及 `.openspec.yaml`。
3. `openspec status --change "<name>" --json` 查看 schema、artifact 依赖与 `applyRequires`。
4. 按 CLI 给出的顺序补齐 `proposal.md`、`design.md`、`tasks.md` 等（具体以当前 schema 为准）；依赖项先读后写。
5. **若探索阶段在 `docs/` 下已有文档**，在编写 **`proposal.md`（必要时在 `design.md`）中显式引用**：使用相对路径链接，并简要说明该文档与本次变更的关系（例如「背景与约束见 `docs/xxx.md`」）。这样评审与实现阶段都能追溯到探索依据，避免信息只留在对话记录里。
6. 全部就绪后，进入实现阶段。

**C. 按任务实现（openspec-apply-change）**

1. 选定变更：`openspec status --change "<name>" --json`。
2. `openspec instructions apply --change "<name>" --json` 获取上下文文件列表与任务进度。
3. 读取 `contextFiles` 中的提案、规格、设计、任务文件。
4. 逐项完成任务，**每完成一项在 `tasks.md` 中将 `- [ ]` 改为 `- [x]`**；若发现设计不符，先改文档再改代码。
5. 遇阻（需求不清、设计缺陷）应暂停并更新 artifacts，而非硬编码绕过。

**D. 归档（openspec-archive-change）**

1. 确认任务与 artifact 状态；若有 `openspec/changes/<name>/specs/` 下的 delta，评估是否与 `openspec/specs/` 主规格同步。
2. 将变更目录移至 `openspec/changes/archive/YYYY-MM-DD-<name>/`（具体步骤以团队规范与 CLI 为准）。

### 2.4 给开发者的实操建议

- **新功能**：优先走「新 change + proposal/design/tasks」，再动代码，便于评审与回溯。
- **小修小补**：若团队允许，可直接修并补简短变更说明；仍建议重大行为变化纳入 OpenSpec。
- **规格即契约**：`openspec/specs/` 中的能力规格是跨变更的长期参考，修改前确认影响面。

---

## 3. MaterialClient 项目结构与评估（帮助读者读懂程序）

### 3.1 解决方案与工程划分

根目录 `MaterialClient.sln` 主要包含：

| 工程 | 职责概要 |
|------|-----------|
| **MaterialClient** | 主应用：Avalonia UI、模块启动、业务编排 |
| **MaterialClient.Common** | 共享库：领域模型、通用服务、硬件/SDK 封装等 |
| **MaterialClient.Toolkit** | 工具类与 UI/开发辅助 |
| **MaterialClient.Common.Tests** | 单元测试 |

读懂业务路径的实用顺序：**从主程序入口与模块注册** → **对应功能 View/ViewModel** → **Application/Domain 服务** → **基础设施（EF、HTTP、设备）**。

### 3.2 架构与技术栈（评估摘要）

依据 `docs/SDD.md` 与 `openspec/project.md`：

- **定位**：Windows 桌面端，卡车称重与物料流程，集成地磅、摄像头、车牌识别等，可与远程平台同步。
- **UI**：Avalonia + ReactiveUI（MVVM），响应式流用 Rx.NET。
- **后端式基础设施**：Volo.Abp + Autofac、EF Core + SQLite、Refit、Serilog。
- **特点**：强依赖硬件与非托管 SDK（如海康等），部署与调试需真实或模拟环境配合。

**评估结论（给读者）**：代码库体量与领域概念较多，但分层与 ABP 习惯一致；难点集中在 **硬件边界、并发与 Rx 组合、称重/道闸等业务规则**。阅读时应对照 `openspec/specs/` 与 `docs/` 中的专题报告。

### 3.3 如何读懂「一块功能」

1. 在 `openspec/specs/` 中搜能力名（如 gate-io、attended-weighing、license-plate）。
2. 在 `openspec/changes/archive/` 搜近期相关归档，了解演进动机。
3. 在解决方案中搜 ViewModel/Service 类名，跟引用链向下读。
4. 若有评估类文档（如 `docs/evaluation-vzvision-lpr-gate-io-function-assessment-2026-03-25.md`），作为业务与 SDK 能力的补充阅读。

### 3.4 文档体系现状（简要）

- `docs/SDD.md`：软件设计文档（架构、模块、数据模型、开发指南等）。
- `docs/existing-docs-inventory.md`：历史报告索引，便于按需深入。
- 部分条目标注「未实施」或「提案」，读时注意区分**当前代码行为**与**历史建议**。

---

## 4. 结语

MaterialClient 将 **OpenSpec** 作为需求与设计载体，将 **`openspec/project.md` + `docs/SDD.md`** 作为人与工具的共享上下文。按 **环境 → 架构 → 规格 → 代码 → 归档变更** 的路径渐进阅读，可在可控成本下建立对仓库的持续理解。

---

**维护说明**：若 OpenSpec CLI 命令或 schema 升级，请同步更新本文第二节；若解决方案增删项目，请同步第三节表格。
