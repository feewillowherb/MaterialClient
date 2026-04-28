## ADDED Requirements

### Requirement: Newly created selection entities SHALL be locally queryable immediately
在人工称重流程中，用户新增 `Material` 或 `Provider` 成功后，系统 MUST 保证该记录已写入本地数据库，以便同一会话中的列表加载、搜索和再次打开选择框时可立即命中。

#### Scenario: Immediate re-query after creating provider
- **WHEN** 用户在人工称重界面新增 Provider 成功后立即触发 Provider 列表查询
- **THEN** 查询结果 MUST 包含该新建 Provider（无需等待后台轮询同步）

#### Scenario: Immediate re-query after creating material
- **WHEN** 用户在人工称重界面新增 Material 成功后立即触发 Material 列表查询
- **THEN** 查询结果 MUST 包含该新建 Material（无需等待后台轮询同步）
