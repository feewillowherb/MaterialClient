## Why

当前 `Provider`、`Material`、`MaterialUnit` 的查询依赖 `WeighingMode` 作为启用标记进行过滤，导致数据可见性受历史模式字段影响。该字段已不再承担启用语义，且后续计划移除，需要先清理业务过滤逻辑避免迁移风险。

## What Changes

- 移除 `Provider`、`Material`、`MaterialUnit` 在读取和筛选时基于 `WeighingMode` 的过滤条件。
- 统一以上实体的可见性规则，不再将 `WeighingMode` 视为“启用状态”。
- 保持现有 API 与调用入口不变，仅调整筛选行为和相关数据查询实现。

## Capabilities

### New Capabilities
- `entity-filter-decoupled-from-weighing-mode`: Provider/Material/MaterialUnit 的列表与选择能力不再依赖 `WeighingMode` 过滤。

### Modified Capabilities
- `attended-weighing`: 相关实体选择与检索要求更新为不使用 `WeighingMode` 作为启用条件。

## Impact

- 受影响代码：`MaterialClient.Common` 中与 Provider/Material/MaterialUnit 查询相关的 API 接口、服务实现及调用方筛选逻辑。
- 受影响行为：下拉选择、搜索和加载实体数据时的结果集可能增加（此前被 `WeighingMode` 隐式过滤的数据将可见）。
- 兼容性：不引入外部接口破坏；为后续删除实体 `WeighingMode` 字段做前置准备。
