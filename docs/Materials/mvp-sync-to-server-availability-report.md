# 材料/供应商双向同步：当前最小可用性报告（MVP）

> 目标口径：**先保证“本地数据可以上传到服务端”**，允许存在部分数据不一致（如重复行、冲突自动覆盖未完善）。

## 1. 结论（先给结果）

当前代码状态下，**还不满足最小可用（不可上线）**。  
根因不是“有些不一致”，而是存在**上传链路阻断点**，会导致本地新建数据无法稳定落到服务端，甚至进入重复重试或重复插入风险。

---

## 2. 现状核查（前后端）

### 2.1 服务端（FdSoft.Material）

已具备上行同步基础能力：

- `SyncController` 提供单条/批量 `UpsertMaterialGood`、`UpsertMaterialProvider`、`Changes`。
- `SyncService` 已实现：
  - `Create/Update/Delete` 三动作；
  - `clientRequestId` 幂等缓存；
  - `Version/baseVersion` 冲突返回；
  - `Sync_ChangeLog` 记录。

结论：**服务端具备接收能力**，可作为 MVP 的接收端。

### 2.2 客户端（MaterialClient）

已具备轮询触发与上传框架：

- `PollingBackgroundService` 中已串联 `UploadAllPendingAsync`。
- `MaterialService` / `ProviderService` 在本地创建/更新后会写入 `SyncState(Pending)`。
- `UploadSyncService` 会按批次上传 `SyncState` 待同步项。

结论：**框架具备，但实现细节有阻断缺陷**（见下一节）。

---

## 3. 阻断 MVP 的关键缺陷（必须先解决）

### 3.1 本地新建数据被当成 Update 上传（阻断）

- 客户端 DTO 工厂默认 `action = "Update"`。
- `UploadSyncService` 调用 `FromEntity(...)` 时没有基于状态切换 Create/Update。
- 本地新建实体（本地 ID）首次上传会携带 `ProviderId/GoodsId` 走 Update，服务端会判定“记录不存在”并返回 `invalid`。

影响：

- 本地新建无法在服务端创建；
- 记录长期停留 Pending，被轮询反复尝试；
- 这不是“可容忍不一致”，是**同步不可用**。

### 3.2 批量结果回写关联键错误（阻断）

- 上传前客户端用 `ClientRequestId -> SyncState` 建了映射。
- 处理结果时却通过 `items.FirstOrDefault(i => i.ProviderId == result.EntityId)`（物料同理）反查 `clientRequestId`。
- Create 成功时服务端返回的是**新服务端 ID**，与本地上传时的 ID 不一致，导致找不到映射，SyncState 无法正确置为 Applied。

影响：

- 即使服务端已创建成功，客户端也可能不认账，继续重试；
- 可能触发重复写入风险（取决于 action 与幂等键复用情况）。

### 3.3 `invalid` 结果未落状态（高风险）

- `invalid` 仅计数日志，不更新 `SyncState` 状态（仍 Pending）。
- 形成“永远重试”的坏循环，吞噬轮询窗口并制造噪音告警。

影响：

- 上传效率下降；
- 运维难以区分“暂时失败”和“永久无效数据”。

---

## 4. 可容忍不一致（MVP 阶段可接受）

以下问题在“先能上传”的目标下，可阶段性接受：

- **重复行风险**：运营人工对齐（已确认可接受）。
- **冲突自动合并缺失**：先保留 `conflict` 状态，不阻断主流程。
- **服务端优先覆盖未完成**：TODO 可延后，只要不影响 `applied` 主通路。
- **游标下行未全量切换**：短期仍可使用现有时间戳下行策略。

---

## 5. MVP 最小验收口径（建议）

只看“能否上传成功”：

1. 本地新建 10 条供应商 -> 一个轮询周期后，服务端新增数量 >= 10。  
2. 本地新建 10 条物料 -> 一个轮询周期后，服务端新增数量 >= 10。  
3. 成功上传后对应 `SyncState` 进入 `Applied`，不再重复上传。  
4. `invalid` 项可停止无限重试（可进入 Failed/DeadLetter 或人工队列）。  

只要这 4 条达成，即可定义为“当前最小可用”。

---

## 6. 面向当前阶段的最小修复范围（非大改）

为了尽快达到 MVP，只建议做小范围修复：

- **修复 A（必须）**：首次上传判定 Create/Update（可基于 `ServerVersion == null` 或本地来源标记）。
- **修复 B（必须）**：结果回写改为按请求顺序或显式回传 `clientRequestId` 关联，不能再用 `entityId` 反查。
- **修复 C（建议）**：`invalid` 状态从 Pending 移出，避免无限重试。
- **修复 D（建议）**：为失败类型增加简单统计（CreateFail/Invalid/Conflict），便于运营观察。

---

## 7. 运营兜底策略（当前阶段）

在“允许部分不一致”的前提下：

- 运营每日对齐一次“同名/同证号”重复供应商；
- 保留一份重复合并 SOP（主记录选择规则、引用迁移规则、删除规则）；
- 若重复率持续升高，触发下阶段“业务唯一约束”改造。

---

## 8. 最终判定

- **当前代码判定：不可用（未达 MVP）**  
- **原因：存在上传主链路阻断缺陷，不是单纯一致性容忍问题**  
- **建议：先完成第 6 节的最小修复，再以第 5 节口径做一次端到端验收**

