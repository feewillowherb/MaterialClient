## ADDED Requirements

### Requirement: 台账管理对话框支持 Ursa 分页浏览
台账管理对话框（固废模式台账管理窗口）SHALL 使用 Ursa `Pagination` 组件对固废导出行进行分页浏览，允许用户在保持当前过滤条件不变的前提下，通过分页控件切换任意页的数据。

#### Scenario: 点击下一页加载后续数据
- **WHEN** 用户在台账管理对话框中设置过滤条件并点击「查询」，随后点击 Ursa 分页控件的「下一页」按钮
- **THEN** 系统 SHALL 以当前过滤条件和 `CurrentPage`（已递增）调用固废分页查询服务，刷新表格中的记录为下一页数据，并更新页脚中的当前页、总条数和总页数

#### Scenario: 点击上一页返回前一页数据
- **WHEN** 用户当前位于第 N 页（N > 1），并点击 Ursa 分页控件的「上一页」按钮
- **THEN** 系统 SHALL 以当前过滤条件和 `CurrentPage`（已递减）重新查询数据，表格显示第 N-1 页的记录，且当前页指示同步更新为 N-1

#### Scenario: 切换到指定页码
- **WHEN** 用户通过 Ursa 分页控件选择任意有效页码 K（1 ≤ K ≤ TotalPages）
- **THEN** 系统 SHALL 使用页码 K 和当前过滤条件重新加载数据，表格显示第 K 页记录，`CurrentPage` 属性值为 K，页脚文案与控件显示保持一致

### Requirement: 分页命令与 CurrentPage 绑定一致
台账管理对话框的 ViewModel SHALL 暴露以下与 Ursa 分页绑定的一致接口：`int CurrentPage`（TwoWay 绑定到 `Pagination.CurrentPage`）、`int PageSize`、`int TotalCount` 以及一个无参数的 `ICommand PageChangeCommand`，该命令在分页控件触发时基于当前 `CurrentPage` 重新加载数据。

#### Scenario: 分页命令无参数执行
- **WHEN** 用户点击 Ursa 分页控件的任意分页按钮（上一页、下一页或页码按钮）
- **THEN** 系统 SHALL 先通过 TwoWay 绑定更新 `CurrentPage`，随后执行无参数的 `PageChangeCommand`，该命令使用当前的 `CurrentPage` 和过滤条件调用固废分页查询服务以刷新数据

#### Scenario: CurrentPage 超出范围时被规范化
- **WHEN** 由于后端 `TotalCount` 变化导致 `TotalPages` 减少，从而使当前 `CurrentPage` 大于新的 `TotalPages`
- **THEN** 系统 SHALL 将 `CurrentPage` 规范化到 `[1, TotalPages]` 范围内（当 `TotalPages` 为 0 时视为 1），并基于规范化后的页码重新加载数据

