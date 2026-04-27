# datagrid-selection-indicator

## Purpose

定义 DataGrid 选中状态的视觉规范，确保用户能够清晰识别当前选中的行，提升数据网格控件的可用性和用户体验。

## Requirements

### Requirement: 选中行背景色

系统 SHALL 使用主题色 PrimaryBlue (#4169E1) 作为 DataGrid 选中行的背景颜色，替代当前的浅蓝色 (#C8DCFF)。

选中行背景色 SHALL 在所有主题变体（Light/Dark）下保持一致的视觉识别度。

#### Scenario: 选中行显示主题色背景

- **WHEN** 用户点击 DataGrid 中的任意一行
- **THEN** 该行的背景颜色 SHALL 变更为 #4169E1（PrimaryBlue）

#### Scenario: 选中行背景色与悬停区分

- **WHEN** 用户将鼠标悬停在某一行上，但该行未被选中
- **THEN** 该行的背景颜色 SHALL 为 #F0F7FF（浅蓝），与选中行的 #4169E1 形成明显对比

### Requirement: 选中行左侧边框指示器

系统 SHALL 在选中行的左侧显示 3px 宽度的实线边框，颜色为 PrimaryBlue (#4169E1)。

边框 SHALL 作为额外的视觉锚点，帮助用户快速定位选中行。

#### Scenario: 选中行显示左侧边框

- **WHEN** DataGrid 的某一行被选中
- **THEN** 该行左侧 SHALL 显示 3px solid #4169E1 的边框

#### Scenario: 未选中行无边框

- **WHEN** DataGrid 的某一行未被选中
- **THEN** 该行左侧 SHALL 不显示任何边框

### Requirement: 选中行文字颜色

系统 SHALL 将选中行内的所有文字颜色设置为白色 (#FFFFFF)，确保在深色背景下的可读性。

文字颜色调整 SHALL 适用于所有单元格，包括表头列和自定义模板列。

#### Scenario: 选中行文字显示为白色

- **WHEN** DataGrid 的某一行被选中
- **THEN** 该行内所有文字 SHALL 显示为白色 (#FFFFFF)

#### Scenario: 未选中行文字保持原色

- **WHEN** DataGrid 的某一行未被选中
- **THEN** 该行内文字 SHALL 保持默认颜色（黑色 #333333 或灰色 #666666）

### Requirement: 悬停状态保持不变

系统 SHALL 保持未选中行的悬停样式为 #F0F7FF，与选中状态形成清晰的视觉层级。

当已选中的行被悬停时，系统 SHALL 保持选中样式，不应用悬停效果。

#### Scenario: 未选中行悬停显示浅蓝色

- **WHEN** 用户将鼠标悬停在未被选中的行上
- **THEN** 该行的背景颜色 SHALL 变更为 #F0F7FF

#### Scenario: 选中行悬停保持选中样式

- **WHEN** 用户将鼠标悬停在已选中的行上
- **THEN** 该行 SHALL 保持选中样式（#4169E1 背景 + 白色文字 + 左侧边框），不应用悬停效果

### Requirement: 全局样式应用

系统 SHALL 通过 `App.axaml` 中的全局样式应用 DataGrid 选中状态样式，确保所有使用 DataGrid 的窗口受益于统一的视觉规范。

全局样式 SHALL 使用以下选择器：
- `DataGridRow:selected /template/ Rectangle#BackgroundRectangle` — 背景色和文字颜色
- `DataGridRow:selected /template/ Border#PART_SelectedCellIndicator` — 左侧边框

#### Scenario: 所有 DataGrid 应用统一样式

- **WHEN** 应用中任意 DataGrid 显示数据
- **THEN** 所有选中行 SHALL 应用统一的选中样式（主题色背景 + 左侧边框 + 白色文字）

#### Scenario: ManualMatchWindow DataGrid 应用样式

- **WHEN** ManualMatchWindow 中的可匹配订单列表 DataGrid 显示数据
- **THEN** 用户选中的订单行 SHALL 显示明显的主题色背景和左侧蓝色边框

## Visual Specification

```
DataGrid 选中状态视觉规范：

未选中行：
┌─────────────────────────────────────────────────┐
│ 内容 | 内容 | 内容 | 内容 | 内容                │  背景: #FFFFFF
└─────────────────────────────────────────────────┘  文字: #333333/#666666

悬停行（未选中）：
┌─────────────────────────────────────────────────┐
│ 内容 | 内容 | 内容 | 内容 | 内容                │  背景: #F0F7FF
└─────────────────────────────────────────────────┘  文字: #333333/#666666

选中行：
├─────────────────────────────────────────────────┤  左边框: 3px solid #4169E1
│ 内容 | 内容 | 内容 | 内容 | 内容                │  背景: #4169E1
└─────────────────────────────────────────────────┘  文字: #FFFFFF
```

颜色对比度要求：
- 选中行背景 (#4169E1) 与 悬停行背景 (#F0F7FF)：对比度 ≥ 3:1
- 选中行文字 (#FFFFFF) 与 选中行背景 (#4169E1)：对比度 ≥ 4.5:1 (WCAG AA)
