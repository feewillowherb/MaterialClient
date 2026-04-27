## ADDED Requirements

### Requirement: 管理窗口 SHALL 具有固定高度
台账管理、材料管理、供应商管理三个窗口 SHALL 设置固定 `Height="500"`，移除 `SizeToContent="Height"` 属性。

#### Scenario: 窗口打开时高度固定
- **WHEN** 用户打开任一管理窗口（台账管理、材料管理或供应商管理）
- **THEN** 窗口高度 SHALL 固定为 500px，不随数据行数变化

#### Scenario: 窗口不可通过拖拽调整高度
- **WHEN** 管理窗口已打开
- **THEN** 用户 SHALL NOT 能够通过拖拽窗口边框改变窗口高度（`CanResize="False"` 保持不变）

### Requirement: DataGrid SHALL 填充剩余空间
管理窗口内的 DataGrid SHALL 使用 Grid 的 `*` 行高自动填充窗口剩余垂直空间。

#### Scenario: DataGrid 自动填充空间
- **WHEN** 管理窗口已打开且数据行数不足 10 行
- **THEN** DataGrid SHALL 占据标题栏和底栏之间的全部剩余空间，底部可能出现空白区域

#### Scenario: 10 行数据完整显示
- **WHEN** 管理窗口已打开且数据行数为 10 行（每行 30px）
- **THEN** 10 行数据 SHALL 全部可见，无需滚动

### Requirement: DataGrid SHALL 支持垂直滚动
当数据行数超过 10 行时，DataGrid SHALL 显示垂直滚动条允许用户滚动查看。

#### Scenario: 超过 10 行时显示滚动条
- **WHEN** 管理窗口已打开且数据行数超过 10 行
- **THEN** DataGrid SHALL 显示垂直滚动条
- **AND** 用户 SHALL 能够通过滚动条或鼠标滚轮查看所有数据行

#### Scenario: 不超过 10 行时无滚动条
- **WHEN** 管理窗口已打开且数据行数不超过 10 行
- **THEN** DataGrid SHALL NOT 显示垂直滚动条（`VerticalScrollBarVisibility="Auto"`）
