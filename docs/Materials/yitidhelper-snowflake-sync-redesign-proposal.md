# 材料/供应商双向同步新提案：基于 YitIdHelper 的雪花 ID 方案

> 目标口径：在不破坏当前 MVP 进度的前提下，先用 `YitIdHelper` 建立稳定的请求关联与幂等回写，再逐步评估端到端统一 ID 体系。

## 1. 提案背景

当前链路已识别出两个关键问题：

- 本地新增在上行阶段容易出现 Create/Update 语义混乱；
- 批量回写若依赖服务端生成的 `entityId` 反查本地状态，容易在 Create 场景失配。

这些问题本质是“同步关联键不稳定”。为降低复杂度，本提案引入 `YitIdHelper` 生成雪花 ID，优先作为客户端稳定关联键使用。

## 2. 设计目标

1. 本地新增记录必须拥有全局唯一、可追踪的客户端生成 ID。
2. 上行批量结果回写必须基于稳定关联键，不依赖服务端自增主键。
3. 幂等重试必须可收敛，避免重复创建和无限 Pending。
4. 方案可分阶段落地，兼容现有服务端主键模型。

## 3. 核心设计

## 3.1 客户端 ID 策略

- 客户端在新建 `Material` 和 `Provider` 时，使用 `YitIdHelper` 生成 `clientGeneratedId: long`；
- `clientGeneratedId` 写入本地实体与 `SyncState`，作为同步主关联键；
- 每条上行请求都携带 `clientGeneratedId` 与 `clientRequestId`。

## 3.2 上行请求与回写规则

1. Create 请求必须传 `Action = "Create"`，并传入 `clientGeneratedId`。  
2. 服务端返回结果需回显 `clientGeneratedId`。  
3. 客户端回写 `SyncState` 时只按 `clientGeneratedId` 关联，不再用 `entityId` 反查。  
4. 对 `invalid` 结果，必须从 Pending 队列迁出，进入 Failed 或 DeadLetter。

## 3.3 幂等约束

- `clientRequestId` 用于防重放；
- `clientGeneratedId` 用于实体级关联与回写定位；
- 两者职责分离，避免“一个键承担两种语义”导致的异常。

## 4. 分阶段落地

## 4.1 阶段 A：MVP 增强（立即可做）

- 客户端引入 `YitIdHelper`，新增 `clientGeneratedId` 字段；
- 上传 Create/Update 动作按实体状态明确区分；
- 批量回写改为按 `clientGeneratedId` 关联；
- `invalid` 结果迁出 Pending，终止无限重试。

交付目标：先把“可上传、可回写、可收敛”打稳。

## 4.2 阶段 B：服务端契约增强

- 上行 DTO 与响应 DTO 增加 `clientGeneratedId`；
- 服务端幂等缓存回包保留 `clientGeneratedId`；
- 下行增量返回中支持客户端按该键进行映射校验。

交付目标：弱网重试、批量混合结果场景可稳定恢复。

## 4.3 阶段 C：主键统一评估

- 评估服务端实体主键及关联外键从 `int` 升级到 `bigint`；
- 评估 `Sync_ChangeLog.EntityId`、相关 DTO 和迁移脚本的一致升级；
- 评估历史数据迁移成本与回滚窗口。

交付目标：决定是否进入端到端统一雪花 ID 的结构性改造。

## 5. 数据与接口调整建议

## 5.1 客户端模型

- `Material`、`Provider` 本地模型增加 `ClientGeneratedId: long`；
- `SyncState` 增加 `ClientGeneratedId: long` 并建立索引；
- 上传任务按 `ClientGeneratedId` 建立请求-状态映射。

## 5.2 上行 DTO

新增或补充以下字段：

- `clientGeneratedId: long`（Create/Update/Delete 全场景可用）；
- `clientRequestId: string`（请求级幂等）；
- `action: "Create" | "Update" | "Delete"`（显式动作，禁止默认 Update）。

## 5.3 返回 DTO

- 返回体必须包含 `status`、`clientGeneratedId`；
- `applied` 场景返回 `entityId`、`version`；
- `conflict` 场景返回 `serverVersion`、`serverData`；
- `invalid` 场景返回 `validationErrors`。

## 6. 迁移与兼容策略

## 6.1 向后兼容

- 阶段 A/B 不强制替换服务端主键；
- 老数据无 `clientGeneratedId` 时，允许回退到一次性兼容映射策略；
- 新增数据强制走 `clientGeneratedId` 通路。

## 6.2 迁移顺序

1. 先发客户端字段与上传链路改造。  
2. 再发服务端 DTO 与回包增强。  
3. 最后评估主键统一迁移窗口。  

## 6.3 风险与防护

- 雪花 ID 依赖时钟，需固定 `workerId` 分配方案；
- 如发生时钟回拨，需启用告警与降级策略；
- 雪花 ID 不解决业务重复，业务唯一约束仍需独立治理。

## 7. MVP 验收口径（本提案）

1. 本地新增 10 条供应商后，一个轮询周期内全部进入 `Applied`。  
2. 本地新增 10 条物料后，一个轮询周期内全部进入 `Applied`。  
3. 批量回包中 `applied/conflict/invalid` 混合场景可逐条正确回写。  
4. `invalid` 项不再重复上传。  
5. 网络重试场景下，`clientRequestId` 不引发重复创建。  

## 8. 与历史归档提案关系

- 本提案定位为“新设计方案”，用于替代历史提案中与 ID 关联相关的脆弱设计；
- 乐观并发（`Version`）、变更日志（`Sync_ChangeLog`）、游标下行（`sinceChangeId`）等能力可继续复用；
- 是否执行“主键全量雪花化”，由阶段 C 评估结论决定。

## 9. 决策建议

- 短期按阶段 A/B 执行，优先解决链路可用性与状态收敛；
- 中期依据运行数据（冲突率、重试率、重复率）决定是否进入阶段 C；
- 长期若统一主键收益显著，再推进服务端 `bigint` 全链路迁移。
