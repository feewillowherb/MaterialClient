## Context

当前材料与供应商新增流程由 `MaterialService.CreateMaterialAsync`、`ProviderService.CreateProviderAsync` 调用远端 API 完成创建。成功后仅将响应 DTO 转换为实体对象返回给调用方，但不立即执行本地仓储写入。本地库更新依赖后台轮询 `ISyncMaterialService`（按时间戳增量同步）在后续周期补齐。

该模型在“创建后立即搜索/选择”场景中会出现短时间不一致：UI 层可拿到新对象，但其它依赖本地查询的路径在同步前可能无法读取新记录。

## Goals / Non-Goals

**Goals:**
- 远端创建成功后立即写入本地数据库，保证本地查询可即时命中新建 Material/Provider。
- 保持现有 API 与 ViewModel 调用方式不变，变更限定在服务层持久化行为。
- 处理幂等：当本地已存在相同主键时不抛冲突异常。

**Non-Goals:**
- 不改动远端 API 协议与接口定义。
- 不调整后台同步机制（仍保留周期同步用于增量更新）。
- 不扩展到其它实体（如 Waybill、MaterialType）的创建链路。

## Decisions

1. **在 Create*Service 内直接执行本地 upsert**
   - 决策：`CreateMaterialAsync` / `CreateProviderAsync` 在 `response.Data` 转实体后，立即执行“存在则更新，不存在则插入”。
   - 理由：改动范围最小，且保证创建结果可立即被本地查询读取。
   - 备选：仅插入不更新。未采用原因是并发/重试场景下可能主键冲突。

2. **以主键为幂等键，优先仓储查询后分支处理**
   - 决策：按返回实体 `Id` 查询本地；命中则 `UpdateAsync`，未命中则 `InsertAsync`。
   - 理由：兼容现有仓储能力，逻辑清晰可测试。
   - 备选：直接 `InsertAsync` 捕获异常回退更新。未采用原因是异常驱动流程可读性与可维护性较差。

3. **不改变后台同步时戳推进策略**
   - 决策：同步服务仍按既有 `MaterialUpdatedTime/ProviderUpdatedTime` 推进，不做额外耦合。
   - 理由：创建后本地已具备记录，后台同步继续承担全局一致性和补偿更新职责。

## Risks / Trade-offs

- **[风险] 本地即时写入与后台同步并发更新导致最后写入覆盖** → **缓解**：遵循远端为权威源，后台同步继续可覆盖最新状态。
- **[风险] Create 返回字段不全导致本地记录临时不完整** → **缓解**：保留后台同步周期补齐；必要时在服务中补默认值映射。
- **[权衡] 服务层职责增加（远端调用 + 本地持久化）** → **缓解**：逻辑集中在两个 Create 方法，避免扩散到 UI 层。

## Migration Plan

1. 更新 `MaterialService.CreateMaterialAsync` 与 `ProviderService.CreateProviderAsync` 的成功路径，新增本地 upsert。
2. 增加针对“创建后本地可查询”的单元测试或服务级测试。
3. 回归验证新增后立即搜索/选择路径。
4. 如出现异常，回滚到“仅返回对象”的行为不影响远端创建正确性。

## Open Questions

- Create API 返回字段是否覆盖本地实体必填字段（尤其审计字段）？若不全，是否统一采用本地默认值填充策略。
