## Why

当前 `CreateMaterialByNameAsync` 与 `CreateProviderAsync` 在远端创建成功后仅返回实体对象，但不立即写入本地数据库，导致本地查询结果与当前创建动作存在短暂不一致。该不一致依赖后台轮询同步消除，会影响用户在创建后立即搜索/选择的稳定性与可预期性。

## What Changes

- 在材料与供应商创建成功后，立即将返回结果写入本地数据库，而非仅依赖后台同步任务。
- 对已存在主键的记录执行幂等处理（更新或跳过重复插入），避免主键冲突导致异常。
- 保持现有 API 契约与 UI 交互方式不变，仅增强创建后的本地持久化一致性。

## Capabilities

### New Capabilities
- `immediate-local-persistence-after-create`: 创建 Material/Provider 成功后，本地数据库应立即可查询到对应记录。

### Modified Capabilities
- `attended-weighing`: 人工称重流程中的新增材料/供应商后续选择行为更新为“创建即本地可见”，不再依赖下一次后台同步。

## Impact

- 受影响代码：`MaterialClient.Common/Services/MaterialService.cs`、`MaterialClient.Common/Services/ProviderService.cs` 及相关仓储写入路径。
- 受影响行为：新增材料/供应商后，在同一会话中立即刷新列表即可读取本地数据。
- 风险点：需处理与后台同步并行时的数据幂等与更新时间覆盖策略。
