## ADDED Requirements

### Requirement: Entity selection SHALL ignore WeighingMode filtering
系统在加载 `Provider`、`Material`、`MaterialUnit` 的可选数据时，MUST NOT 使用 `WeighingMode` 字段作为启用/可见过滤条件。

#### Scenario: Provider list loading without WeighingMode constraint
- **WHEN** 系统请求 Provider 列表用于选择或搜索
- **THEN** 返回结果 MUST NOT 因 `WeighingMode` 字段值被过滤

#### Scenario: Material list loading without WeighingMode constraint
- **WHEN** 系统请求 Material 列表用于选择或搜索
- **THEN** 返回结果 MUST NOT 因 `WeighingMode` 字段值被过滤

#### Scenario: MaterialUnit list loading without WeighingMode constraint
- **WHEN** 系统请求 MaterialUnit 列表用于选择或搜索
- **THEN** 返回结果 MUST NOT 因 `WeighingMode` 字段值被过滤
