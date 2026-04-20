# OpenSpec 双向同步提案：前后端缺陷与不足分析

> **分析范围**  
> - 服务端：`FdSoft.Material/openspec/changes/archive/2026-04-15-material-provider-data-sync`  
> - 客户端：`MaterialClient/openspec/changes/archive/2026-04-17-materialclient-new-sync-material-supplier`  
>  
> **目的**：对照两份已归档提案，归纳设计缺口、实现风险与跨端一致性问题；并单独展开「桌面端先建 ProviderA、未同步前服务端也建 ProviderA」的典型分叉场景。

---

## 1. 总览结论

| 维度 | 服务端提案 | 客户端提案 | 交叉问题 |
|------|------------|------------|----------|
| 乐观并发与版本 | `Version` + `baseVersion`，覆盖 Update/Delete | `SyncState.LocalVersion` → 上行 `baseVersion` | Create 路径无版本语义，无法检测「重复创建」 |
| 幂等 | `clientRequestId` 24h 去重 | 每条 `SyncState` 一个 GUID，重试复用 | 幂等仅保证**同一请求重放**，不解决**跨端重复业务对象** |
| 冲突与合并 | 409 + `serverData`，明确不做自动合并 | 「服务端优先」+ 本地覆盖 | 分叉创建不会被建模为「冲突」，易静默产生两条记录 |
| 下行同步 | `sinceChangeId` + 保留时间戳 | 提案强调上行与轮询集成 | 客户端是否全面切游标、与本地 `WorkSettings` 时间戳如何统一，文档粒度不足 |
| 人机交互 | 设计图含冲突 UI；proposal 要求客户端冲突 UI | 已移除数据管理/状态 UI，全自动 | 与早期服务端对外预期不一致；静默覆盖可能违背现场预期 |

以下分端说明，并在第 4 节集中讨论「双端各建 ProviderA」场景。

---

## 2. 服务端提案（2026-04-15）的主要缺陷与不足

### 2.1 Create（插入）路径缺乏业务唯一性与去重语义

- 上行规范约定：`Action = "Create"` 且无 `GoodsId`/`ProviderId` 时执行 **INSERT**，返回新主键与 `Version = 1`。
- **未规定** 在 `(CoId, ProId?, 业务键如名称/编码)` 等维度上的唯一约束或「已存在则转为 Update/合并」策略。
- **后果**：无法区分「 genuinely 新记录」与「与别处已创建记录实为同一供应商」——这正是分叉场景的核心。

### 2.2 乐观并发与「可选 baseVersion」的灰区

- Spec 明确：未提供 `baseVersion` 时 Update **可不校验版本**（向后兼容）。
- Design 已意识到 Web 管理端可能长期不携带 `baseVersion`，与同步路径的强一致目标并存。
- **后果**：同一实体可能被非同步接口覆盖，而同步客户端仍以为版本链完整；审计与排错困难。

### 2.3 幂等键语义边界清晰但能力有限

- `clientRequestId` + `Sync_IdempotencyKey` 解决的是 **HTTP 重试 / 同一客户端同一上传意图**。
- **不解决**：设备 A 与设备 B、或桌面与 Web 各自生成不同 `clientRequestId` 插入「同名同项目供应商」。

### 2.4 ChangeLog 与下行拉取假设

- 游标模式依赖 `Sync_ChangeLog` 驱动「变更实体 Id 列表再查全量」。
- 若历史数据或旁路写入未一致写日志（迁移、脚本、旧接口），客户端可能与服务端视图漂移；提案提到风险但未规定校验任务。

### 2.5 Open Questions 未关闭即归档的影响

- 如 `MaterialGoodsType` 是否同步、`AddMtByCompany` 分发时 `Version` 重置等，仍影响多端长期一致性；客户端提案将「仅物料与供应商」列为 Non-Goal，与服务端扩展节奏需对齐。

---

## 3. 客户端提案（2026-04-17）的主要缺陷与不足

### 3.1 「冲突」策略与无 UI 的张力

- Design 采用 **服务端优先**：`status = "conflict"` 时拉取 `serverData` 覆盖本地，并依赖 MessageBus 刷新缓存。
- 同时移除了数据管理类 UI，用户**看不到**「本地曾提交过什么、被谁覆盖」。
- **风险**：现场以为「已在本地建好供应商」，后台静默换成服务端副本，且无留痕界面；与「平台为权威」一致，但与「可解释性/可审计」不足。

### 3.2 SyncState 与本地实体生命周期的若干未写清点

- 规范要求：本地创建/更新需建 `SyncState(Pending)`，并维护 `LocalVersion`。
- **未在 spec 中写清**：
  - 本地新建行是否先写**临时主键**，上行 Create 成功后如何把 `ProviderId`/`GoodsId` **回写**并修复关联（如物料单位、引用）；
  - 若采用服务端优先覆盖，是否删除/合并旧 `SyncState` 与孤立关联行。

### 3.3 与下载同步的竞态仅部分缓解

- 决策为：**先下载，后上传**，减轻「先推送再被旧拉取覆盖」的问题。
- **不能缓解**：两端在**同一窗口期内各自插入**尚未被对端知晓的记录（分叉创建）；下载再上传的顺序与此无关。

### 3.4 `GET /Sync/Changes` 的客户端角色

- Refit 与 DTO 包含变更日志查询，但核心流程描述以批量 Upsert 为主。
- 若下行仍以时间戳为主、游标为辅，**本地 Version 与 ServerVersion 对齐策略**需在实现层写清楚，否则易出现 `baseVersion` 与服务器真实版本脱节。

### 3.5 与早期服务端 proposal 表述不一致

- 服务端 `proposal.md` 仍写「MaterialClient：冲突处理 UI、游标拉取切换」；客户端归档已去掉同步管理 UI。
- 需以**客户端归档 design 为准**做对外沟通，并更新服务端文档中的「预期能力」描述，避免验收标准分裂。

---

## 4. 补充场景：桌面端创建 ProviderA，未同步时服务端也创建 ProviderA

下面「ProviderA」指**同一业务含义**的供应商（例如同一公司、同一项目上下文下相同名称或相同统一社会信用代码等——具体业务键以产品为准）。**时间线**：T1 桌面本地 `Create ProviderA`（仅 SQLite）；T2 服务端通过 Web 或其它客户端再 `Create ProviderA`；T3 桌面轮询执行上行，`Action=Create`，尚未拉取到 T2 的插入。

### 4.1 在当前提案下的最可能行为

1. **服务端**  
   - T2 已插入一行 `Material_Provider`，主键 `ProviderId = S`，`Version = 1`。  
   - T3 收到桌面上行 Create（无 `ProviderId`）：按 spec **再执行一次 INSERT**，得到 `ProviderId = S'`，`S ≠ S'`。  
   - 除非数据库层对 `(CoId, …)` 等业务键有 **UNIQUE** 且插入失败返回 `invalid`，否则**不会**返回「冲突」类结果；乐观锁不参与 Create。

2. **桌面端**  
   - 本地原持有 `ProviderId = L`（本地自增）。  
   - 若上传成功：服务端返回新 `providerId = S'`，客户端需把本地行更新为 `S'`，并修正所有引用 `L` 的外键；若实现不完整，会出现**混用 L 与 S'** 或重复展示两条供应商。  
   - 下一次下载（时间戳或游标）可能再拉下 **T2 产生的 S** 与 **T3 产生的 S'**，用户可见**两条「ProviderA」**，或需靠名称去重（提案未规定）。

### 4.2 该场景暴露了哪些「提案级」缺口

| 缺口 | 说明 |
|------|------|
| 无「分叉创建」检测 | Create 无 `baseVersion`，也无业务键哈希/ idempotency key 跨会话去重 |
| 无统一「实体身份」模型 | 仅依赖整型主键，不引入客户端 `clientGeneratedId` 或服务端「自然键」解析 |
| 服务端优先在 Create 上不成立 | 「权威」无法裁决两条 INSERT 谁保留；除非人工合并或事后去重 |
| ChangeLog 会记录两次 Insert | 下行游标会拉两条变更，客户端若只做 ID 映射不做业务去重，状态机复杂度上升 |

### 4.3 若产品不能接受重复行，需在 spec 层补充的方向（非实现承诺，仅分析选项）

- **数据库/领域约束**：在 `CoId + ProId + 关键业务字段` 上唯一索引，重复 Create 返回可识别的 `invalid` 或专用 `duplicate` 状态，客户端改为拉取再对齐 ID。  
- **显式去重键**：创建请求携带 `clientMutationId` 或 `(namespace, businessKey)`，服务端在幂等表或独立映射表中解析。  
- **两阶段创建**：先「申请占位/预留业务键」，再提交详情（复杂度高）。  
- **运维/管理端合并**：保留重复检测后台，不在同步协议内解决（提案外）。

---

## 5. 建议的后续文档或规范动作（可选）

1. **统一对外说明**：更新服务端 `proposal.md` 中与「冲突 UI」相关的表述，与 MaterialClient 归档结论一致。  
2. **补一篇「分叉创建」ADR**：明确是否允许重复、依赖库表唯一约束还是产品层合并。  
3. **客户端实现清单**：单列「Create 成功后 ID 映射与引用修复」「与服务端重复行的检测策略（若业务要求）」。

---

## 6. 参考路径

- 服务端归档：`FdSoft.Material/openspec/changes/archive/2026-04-15-material-provider-data-sync/`（`proposal.md`、`design.md`、`specs/**`）  
- 客户端归档：`MaterialClient/openspec/changes/archive/2026-04-17-materialclient-new-sync-material-supplier/`（`proposal.md`、`design.md`、`specs/**`）  
- 相关调研：`FdSoft.Material/docs/research-materialgoods-materialprovider-bidirectional-sync-materialclient-analysis.md`

---

*文档生成说明：基于上述归档 Markdown 的静态对照分析；若实现已与提案分叉，请以代码与数据库约束为准复核本节结论。*
