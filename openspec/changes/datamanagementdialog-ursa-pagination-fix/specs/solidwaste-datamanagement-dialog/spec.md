## MODIFIED Requirements

### Requirement: 固废台账管理对话框支持分页浏览
固废台账管理对话框 SHALL 通过服务端分页的方式展示固废导出行数据，使用 Ursa `Pagination` 控件进行页间导航，并在页脚显示当前页、总条数和总页数。分页操作不得改变当前的过滤条件。

#### Scenario: 分页状态与服务端结果一致
- **WHEN** 用户在台账管理对话框中设置过滤条件并点击查询
- **THEN** 系统 SHALL 使用过滤条件调用分页查询接口，表格显示第一页数据，页脚中的总条数和总页数 SHALL 与服务端返回的 `TotalCount` 和 `PageSize` 计算结果一致

#### Scenario: 分页控件与页脚文案同步
- **WHEN** 用户通过 Ursa 分页控件切换到任意页码 N
- **THEN** 表格 SHALL 显示第 N 页数据，Ursa 分页控件中的当前页指示与页脚文案中的「现页数」均显示为 N，且总页数和总条数保持不变

