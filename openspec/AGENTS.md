# OpenSpec 指令

使用 OpenSpec 进行规范驱动开发的 AI 编程助手指令。

## TL;DR 快速检查清单

- 搜索现有工作：`openspec spec list --long`，`openspec list`（仅在全文搜索时使用 `rg`）
- 确定范围：新功能 vs 修改现有功能
- 选择唯一的 `change-id`：kebab-case，动词引导（`add-`、`update-`、`remove-`、`refactor-`）
- 搭建：`proposal.md`、`tasks.md`、`design.md`（仅在需要时）和每个受影响功能的 delta spec
- 编写 delta：使用 `## ADDED|MODIFIED|REMOVED|RENAMED Requirements`；每个需求至少包含一个 `#### Scenario:`
- **文档语言**：markdown 文档优先使用中文。仅在技术术语、代码引用、API 名称和特别要求时使用英文。
- 验证：`openspec validate [change-id] --strict` 并修复问题
- 请求批准：在提案获得批准之前不要开始实施

## 三阶段工作流程

### 阶段 1：创建变更
在以下情况下创建提案：
- 添加功能或特性
- 进行破坏性更改（API、schema）
- 更改架构或模式
- 优化性能（更改行为）
- 更新安全模式

触发器（示例）：
- "Help me create a change proposal"
- "Help me plan a change"
- "Help me create a proposal"
- "I want to create a spec proposal"
- "I want to create a spec"

松散匹配指导：
- 包含以下之一：`proposal`、`change`、`spec`
- 伴随以下之一：`create`、`plan`、`make`、`start`、`help`

跳过提案的情况：
- Bug 修复（恢复预期行为）
- 拼写错误、格式、注释
- 依赖更新（非破坏性）
- 配置更改
- 现有行为的测试

**工作流程**
1. 查看 `openspec/project.md`、`openspec list` 和 `openspec list --specs` 以了解当前上下文。
2. 选择一个唯一的动词引导的 `change-id` 并在 `openspec/changes/<id>/` 下搭建 `proposal.md`、`tasks.md`、可选的 `design.md` 和 spec delta。
3. 使用 `## ADDED|MODIFIED|REMOVED Requirements` 起草 spec delta，每个需求至少包含一个 `#### Scenario:`。
4. 在分享提案之前运行 `openspec validate <id> --strict` 并解决所有问题。

### 阶段 2：实施变更
将这些步骤作为 TODO 逐一跟踪并完成。
1. **Read proposal.md** - 了解要构建的内容
2. **Read design.md**（如果存在）- 审查技术决策
3. **Read tasks.md** - 获取实施检查清单
4. **Implement tasks sequentially** - 按顺序完成
5. **Confirm completion** - 确保在更新状态之前完成 `tasks.md` 中的每一项
6. **Update checklist** - 所有工作完成后，将每个任务设置为 `- [x]`，使列表反映实际情况
7. **Approval gate** - 在提案被审查和批准之前不要开始实施

### 阶段 3：归档变更
部署后，创建单独的 PR 以：
- 将 `changes/[name]/` 移动到 `changes/archive/YYYY-MM-DD-[name]/`
- 如果功能发生变化，更新 `specs/`
- 对于仅工具类的更改，使用 `openspec archive <change-id> --skip-specs --yes`（始终显式传递 change ID）
- 运行 `openspec validate --strict` 以确认归档的变更通过检查

## 任何任务之前

**上下文检查清单：**
- [ ] 在 `specs/[capability]/spec.md` 中阅读相关 spec
- [ ] 检查 `changes/` 中待处理的更改是否存在冲突
- [ ] 阅读 `openspec/project.md` 了解约定
- [ ] 运行 `openspec list` 查看活跃的更改
- [ ] 运行 `openspec list --specs` 查看现有功能

**在创建 Spec 之前：**

**文档语言：**
- 所有 markdown 文档（`docs/`、`specs/`、`changes/`、根级文件）优先使用中文
- 保留英文用于：
  - 技术术语和技术词汇（API、HTTP、REST、JSON、XML、SQL 等）
  - 代码引用和方法名
  - 文件路径和类名
  - 编程语言关键字
  - 代码中的异常消息
- 使用一致的翻译：当技术术语有既定翻译时，保持一致使用
- 与现有中文文档保持一致

- 始终检查功能是否已存在
- 优先修改现有 spec 而不是创建重复项
- 使用 `openspec show [spec]` 审查当前状态
- 如果请求不明确，在搭建之前提出 1-2 个澄清问题

### 搜索指导
- 列出 specs：`openspec spec list --long`（或脚本使用 `--json`）
- 列出更改：`openspec list`（或 `openspec change list --json` - 已弃用但可用）
- 显示详细信息：
  - Spec：`openspec show <spec-id> --type spec`（过滤使用 `--json`）
  - Change：`openspec show <change-id> --json --deltas-only`
- 全文搜索（使用 ripgrep）：`rg -n "Requirement:|Scenario:" openspec/specs`

## 快速开始

### CLI 命令

```bash
# Essential commands
openspec list                  # List active changes
openspec list --specs          # List specifications
openspec show [item]           # Display change or spec
openspec validate [item]       # Validate changes or specs
openspec archive <change-id> [--yes|-y]   # Archive after deployment (add --yes for non-interactive runs)

# Project management
openspec init [path]           # Initialize OpenSpec
openspec update [path]         # Update instruction files

# Interactive mode
openspec show                  # Prompts for selection
openspec validate              # Bulk validation mode

# Debugging
openspec show [change] --json --deltas-only
openspec validate [change] --strict
```

### 命令标志

- `--json` - 机器可读输出
- `--type change|spec` - 消除项目歧义
- `--strict` - 全面验证
- `--no-interactive` - 禁用提示
- `--skip-specs` - 归档时不更新 specs
- `--yes`/`-y` - 跳过确认提示（非交互式归档）

## 目录结构

```
openspec/
├── project.md              # Project conventions
├── specs/                  # Current truth - what IS built
│   └── [capability]/       # Single focused capability
│       ├── spec.md         # Requirements and scenarios
│       └── design.md       # Technical patterns
├── changes/                # Proposals - what SHOULD change
│   ├── [change-name]/
│   │   ├── proposal.md     # Why, what, impact
│   │   ├── tasks.md        # Implementation checklist
│   │   ├── design.md       # Technical decisions (optional; see criteria)
│   │   └── specs/          # Delta changes
│   │       └── [capability]/
│   │           └── spec.md # ADDED/MODIFIED/REMOVED
│   └── archive/            # Completed changes
```

## 创建变更提案

### 决策树

```
New request?
├─ Bug fix restoring spec behavior? → Fix directly
├─ Typo/format/comment? → Fix directly
├─ New feature/capability? → Create proposal
├─ Breaking change? → Create proposal
├─ Architecture change? → Create proposal
└─ Unclear? → Create proposal (safer)
```

### 提案结构

1. **创建目录：** `changes/[change-id]/`（kebab-case，动词引导，唯一）

2. **编写 proposal.md：**
```markdown
# Change: [变更的简要描述]

## Why
[1-2 句话说明问题/机会]

## What Changes
- [变更列表]
- [用 **BREAKING** 标记破坏性变更]

## UI Design Changes（如适用）
- Include ASCII mockups for new/modified interfaces
- Add Mermaid sequence diagrams for user interaction flows
- Reference: PROPOSAL_DESIGN_GUIDELINES.md for format requirements

## Code Flow Changes（如适用）
- Include Mermaid flowcharts for data flow
- Add sequence diagrams for API interactions
- Include architecture diagrams for system changes
- Reference: PROPOSAL_DESIGN_GUIDELINES.md for format requirements

## Impact
- Affected specs: [list capabilities]
- Affected code: [key files/systems]
```

3. **创建 spec delta：** `specs/[capability]/spec.md`
```markdown
## ADDED Requirements
### Requirement: New Feature
The system SHALL provide...

#### Scenario: Success case
- **WHEN** user performs action
- **THEN** expected result

## MODIFIED Requirements
### Requirement: Existing Feature
[Complete modified requirement]

## REMOVED Requirements
### Requirement: Old Feature
**Reason**: [Why removing]
**Migration**: [How to handle]
```
如果影响多个功能，在 `changes/[change-id]/specs/<capability>/spec.md` 下创建多个 delta 文件——每个功能一个。

4. **创建 tasks.md：**
```markdown
## 1. Implementation
- [ ] 1.1 Create database schema
- [ ] 1.2 Implement API endpoint
- [ ] 1.3 Add frontend component
- [ ] 1.4 Write tests
```

5. **在需要时创建 design.md：**
如果满足以下任何条件，则创建 `design.md`；否则省略它：
- 跨领域变更（多个服务/模块）或新的架构模式
- 新的外部依赖或重要的数据模型更改
- 安全、性能或迁移复杂性
- 在编码之前从技术决策中受益的歧义
- 需要视觉 mockups 和交互流的 UI/UX 变更
- 需要图表的代码流变更（sequence、flowchart、architecture）

**Note**：有关包括 UI mockups 和代码流图的设计指南，请参阅 [PROPOSAL_DESIGN_GUIDELINES.md](PROPOSAL_DESIGN_GUIDELINES.md)

最小 `design.md` 骨架：
```markdown
## Context
[Background, constraints, stakeholders]

## Goals / Non-Goals
- Goals: [...]
- Non-Goals: [...]

## Decisions
- Decision: [What and why]
- Alternatives considered: [Options + rationale]

## UI/UX Design
[Include if UI changes are involved]
- ASCII mockups of new/modified interfaces
- User interaction flows (Mermaid sequence diagrams)
- State transitions and error handling UI
- Mobile/responsive considerations
- See PROPOSAL_DESIGN_GUIDELINES.md for detailed requirements

## Technical Design
[Include if code flow changes are involved]
- Architecture diagrams (Mermaid)
- Data flow diagrams (Mermaid flowcharts)
- API interaction sequences (Mermaid sequence diagrams)
- Component relationships (Mermaid graphs)
- See PROPOSAL_DESIGN_GUIDELINES.md for detailed requirements

## Risks / Trade-offs
- [Risk] → Mitigation

## Migration Plan
[Steps, rollback]

## Open Questions
- [...]
```

## Spec 文件格式

### 关键：Scenario 格式

**CORRECT**（使用 #### 标题）：
```markdown
#### Scenario: User login success
- **WHEN** valid credentials provided
- **THEN** return JWT token
```

**WRONG**（不要使用项目符号或粗体）：
```markdown
- **Scenario: User login**  ❌
**Scenario**: User login     ❌
### Scenario: User login      ❌
```

每个需求必须至少有一个 scenario。

### 需求措辞
- 使用 SHALL/MUST 用于规范性需求（除非有意非规范性，否则避免 should/may）

### Delta 操作

- `## ADDED Requirements` - 新功能
- `## MODIFIED Requirements` - 更改的行为
- `## REMOVED Requirements` - 已弃用的功能
- `## RENAMED Requirements` - 名称更改

使用 `trim(header)` 匹配标题 - 忽略空白。

#### 何时使用 ADDED vs MODIFIED
- ADDED：引入可以作为需求独立存在的新功能或子功能。当更改是正交的（例如，添加"Slash Command Configuration"）而不是更改现有需求的语义时，首选 ADDED。
- MODIFIED：更改现有需求的行为、范围或验收标准。始终粘贴完整的、更新的需求内容（标题 + 所有 scenario）。归档器将完全用您在此处提供的内容替换需求；部分 delta 将丢失以前的详细信息。
- RENAMED：仅在名称更改时使用。如果您也更改行为，请使用 RENAMED（名称）加上 MODIFIED（内容），引用新名称。

常见错误：使用 MODIFIED 添加新关注点而不包含以前的文本。这会在归档时导致详细信息丢失。如果您没有明确更改现有需求，请在 ADDED 下添加一个新需求。

正确编写 MODIFIED 需求：
1) 在 `openspec/specs/<capability>/spec.md` 中找到现有需求。
2) 复制整个需求块（从 `### Requirement: ...` 到其 scenario）。
3) 将其粘贴到 `## MODIFIED Requirements` 下并编辑以反映新行为。
4) 确保标题文本完全匹配（不区分空白）并保留至少一个 `#### Scenario:`。

RENAMED 示例：
```markdown
## RENAMED Requirements
- FROM: `### Requirement: Login`
- TO: `### Requirement: User Authentication`
```

## 故障排除

### 常见错误

**"Change must have at least one delta"**
- 检查 `changes/[name]/specs/` 是否存在 .md 文件
- 验证文件具有操作前缀（## ADDED Requirements）

**"Requirement must have at least one scenario"**
- 检查 scenario 使用 `#### Scenario:` 格式（4 个井号）
- 不要在 scenario 标题中使用项目符号或粗体

**静默 scenario 解析失败**
- 需要精确格式：`#### Scenario: Name`
- 使用以下方法调试：`openspec show [change] --json --deltas-only`

### 验证提示

```bash
# Always use strict mode for comprehensive checks
openspec validate [change] --strict

# Debug delta parsing
openspec show [change] --json | jq '.deltas'

# Check specific requirement
openspec show [spec] --json -r 1
```

## 快乐路径脚本

```bash
# 1) Explore current state
openspec spec list --long
openspec list
# Optional full-text search:
# rg -n "Requirement:|Scenario:" openspec/specs
# rg -n "^#|Requirement:" openspec/changes

# 2) Choose change id and scaffold
CHANGE=add-two-factor-auth
mkdir -p openspec/changes/$CHANGE/{specs/auth}
printf "## Why\n...\n\n## What Changes\n- ...\n\n## Impact\n- ...\n" > openspec/changes/$CHANGE/proposal.md
printf "## 1. Implementation\n- [ ] 1.1 ...\n" > openspec/changes/$CHANGE/tasks.md

# 3) Add deltas (example)
cat > openspec/changes/$CHANGE/specs/auth/spec.md << 'EOF'
## ADDED Requirements
### Requirement: Two-Factor Authentication
Users MUST provide a second factor during login.

#### Scenario: OTP required
- **WHEN** valid credentials are provided
- **THEN** an OTP challenge is required
EOF

# 4) Validate
openspec validate $CHANGE --strict
```

## 多功能示例

```
openspec/changes/add-2fa-notify/
├── proposal.md
├── tasks.md
└── specs/
    ├── auth/
    │   └── spec.md   # ADDED: Two-Factor Authentication
    └── notifications/
        └── spec.md   # ADDED: OTP email notification
```

auth/spec.md
```markdown
## ADDED Requirements
### Requirement: Two-Factor Authentication
...
```

notifications/spec.md
```markdown
## ADDED Requirements
### Requirement: OTP Email Notification
...
```

## 最佳实践

### 设计可视化
- **UI Changes**：始终包含 ASCII mockups 和交互流（参见 [PROPOSAL_DESIGN_GUIDELINES.md](PROPOSAL_DESIGN_GUIDELINES.md)）
- **Code Changes**：包含 Mermaid 图表用于数据流、序列和架构
- **Error Paths**：在 UI 和代码图中记录错误处理流
- **State Transitions**：在图表中清楚地显示前/后状态

### 简单性优先
- 默认 <100 行新代码
- 单文件实现，直到证明不足
- 避免没有明确理由的框架
- 选择无聊、经过验证的模式

### 复杂性触发器
仅在以下情况下添加复杂性：
- 性能数据显示当前解决方案太慢
- 具体的规模要求（>1000 用户，>100MB 数据）
- 多个已证实的需要抽象的用例

### 清晰的引用
- 使用 `file.ts:42` 格式表示代码位置
- 将 spec 引用为 `specs/auth/spec.md`
- 链接相关的变更和 PR

### 功能命名
- 使用动词-名词：`user-auth`、`payment-capture`
- 每个功能单一目的
- 10 分钟可理解规则
- 如果描述需要"AND"则拆分

### 变更 ID 命名
- 使用 kebab-case，简短且描述性：`add-two-factor-auth`
- 优先使用动词引导的前缀：`add-`、`update-`、`remove-`、`refactor-`
- 确保唯一性；如果已被占用，附加 `-2`、`-3` 等。

## 工具选择指南

| Task | Tool | Why |
|------|------|-----|
| Find files by pattern | Glob | Fast pattern matching |
| Search code content | Grep | Optimized regex search |
| Read specific files | Read | Direct file access |
| Explore unknown scope | Task | Multi-step investigation |

## 错误恢复

### 变更冲突
1. 运行 `openspec list` 查看活跃的变更
2. 检查重叠的 specs
3. 与变更所有者协调
4. 考虑合并提案

### 验证失败
1. 使用 `--strict` 标志运行
2. 检查 JSON 输出获取详细信息
3. 验证 spec 文件格式
4. 确保 scenario 格式正确

### 缺少上下文
1. 首先阅读 project.md
2. 检查相关的 specs
3. 查看最近的归档
4. 请求澄清

## 快速参考

### 阶段指示器
- `changes/` - 已提议，尚未构建
- `specs/` - 已构建和部署
- `archive/` - 已完成的变更

### 文件用途
- `proposal.md` - 原因和内容
- `tasks.md` - 实施步骤
- `design.md` - 技术决策
- `spec.md` - 需求和行为

### CLI 要点
```bash
openspec list              # What's in progress?
openspec show [item]       # View details
openspec validate --strict # Is it correct?
openspec archive <change-id> [--yes|-y]  # Mark complete (add --yes for automation)
```

记住：Specs 是真理。Changes 是提案。保持它们同步。
