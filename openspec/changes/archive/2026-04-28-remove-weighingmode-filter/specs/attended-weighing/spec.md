## ADDED Requirements

### Requirement: Attended weighing entity queries SHALL not use WeighingMode as enabled flag
在人工称重相关流程中，涉及 `Provider`、`Material`、`MaterialUnit` 的查询和选择 MUST NOT 将 `WeighingMode` 作为“启用”判定条件。`WeighingMode` 在该上下文仅视为遗留字段，不参与可用性过滤。

#### Scenario: Entity search in attended weighing returns all non-WeighingMode-filtered candidates
- **WHEN** 用户在人工称重流程中打开或搜索 Provider/Material/MaterialUnit 选项
- **THEN** 系统 MUST 基于既有业务条件返回结果，且 MUST NOT 添加 `WeighingMode` 过滤
