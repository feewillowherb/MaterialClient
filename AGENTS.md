# Agent 行为准则

## 核心原则：规范驱动，代码只读

**Agent 默认禁止直接修改任何源代码文件。** 所有代码变更必须通过 OpenSpec 工作流驱动。

### 允许的操作

| 操作 | 路径范围 | 说明 |
|------|----------|------|
| 创建/修改 Proposal | `openspec/changes/<id>/proposal.md` | 描述变更的原因和内容 |
| 创建/修改 Design | `openspec/changes/<id>/design.md` | 技术方案和架构决策 |
| 创建/修改 Tasks | `openspec/changes/<id>/tasks.md` | 实施任务清单 |
| 创建/修改 Spec Delta | `openspec/changes/<id>/specs/**/*.md` | 需求变更（ADDED/MODIFIED/REMOVED） |
| 创建/修改 Spec | `openspec/specs/**/*.md` | 功能规范（仅归档时更新） |
| 读取源代码 | `**/*.cs`, `**/*.axaml`, etc. | 仅用于理解上下文，禁止写入 |
| 读取配置/文档 | `**/*.md`, `**/*.json`, etc. | 辅助理解项目结构 |

### 禁止的操作（默认模式）

- **禁止** 创建或修改 `.cs`、`.axaml`、`.axaml.cs`、`.csproj`、`.sln` 等源代码文件
- **禁止** 修改 `appsettings*.json`、`Directory.Build.props`、`Directory.Packages.props` 等构建/配置文件
- **禁止** 执行 `dotnet build`、`dotnet run`、`dotnet test` 等编译/运行命令
- **禁止** 在 Spec/Design 产物之外直接实施任何功能

### 例外：OpenSpec Apply 模式

当 Agent 执行 **openspec apply**（`/opsx:apply`）流程时，上述源代码修改限制解除。Agent 可以：

- 创建和修改 `.cs`、`.axaml`、`.axaml.cs`、`.csproj` 等源代码文件
- 修改 `Directory.Packages.props`、`Directory.Build.props` 等构建配置文件
- 执行必要的构建和测试命令来验证变更

**前提条件**：变更的 Proposal、Design、Spec Delta、Tasks 已全部就绪（`openspec status` 显示 `isComplete: true`）。

### 唯一真源

```
openspec/
├── specs/          ← 当前系统行为的唯一权威描述
├── changes/        ← 所有待实施变更的规范化定义
└── project.md      ← 项目约定和技术栈
```

代码是 Spec 的**衍生产物**，不是反过来。当代码与 Spec 不一致时，应以 Spec 为准更新代码，而非修改 Spec 迁就代码。

## 工作流

### Agent 的职责边界

```
用户需求 → Agent 产出 Spec/Design → 人工审批 → Agent 通过 apply 实施代码 → Agent 验证一致性
              ▲                                                              │
              └────────────────────── 反馈循环 ◄──────────────────────────────┘
```

1. **探索阶段** (`/opsx:explore`)：阅读代码和文档，理解问题，可视化分析
2. **提案阶段** (`/opsx:propose`)：产出 proposal.md、design.md、tasks.md、spec delta
3. **审批阶段**：人工审查提案，确认或要求修改
4. **实施阶段** (`/opsx:apply`)：Agent 根据 Spec/Design 实施代码变更（此阶段允许修改源代码）
5. **归档阶段** (`/opsx:archive`)：变更完成后更新 specs，归档 change

### 当用户要求修改代码时

如果用户在非 apply 流程中直接要求 Agent 编写或修改代码，Agent 应：

1. 说明当前项目规范要求所有变更通过 OpenSpec 工作流驱动
2. 建议先创建变更提案（`/opsx:propose`）
3. 帮助用户将需求转化为 Spec/Design 文档
4. 在提案获得批准后，通过 `/opsx:apply` 实施代码

## OpenSpec 参考

- 项目上下文：`openspec/project.md`
- 查看活跃变更：`openspec list`
- 查看功能规范：`openspec list --specs`
- 验证变更：`openspec validate [change-id] --strict`
