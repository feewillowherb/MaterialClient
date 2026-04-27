# Implementation Tasks: Manual Match Window Selection Indicator

## 0. 方案演示项目创建

- [x] 0.1 创建 MaterialClient.Demo 项目
  - 在解决方案根目录创建新的 Avalonia 项目
  - 项目名称：MaterialClient.Demo
  - 目标框架：net10.0（与 MaterialClient 主项目保持一致，见 Directory.Build.props）
  - 添加 Avalonia、Avalonia.Desktop、Avalonia.Themes.Fluent、Avalonia.Fonts.Inter NuGet 包

- [x] 0.2 配置演示项目基础结构
  - 创建 Views/ 目录用于存放演示窗口
  - 创建 ViewModels/ 目录用于存放视图模型
  - 创建 Models/ 目录用于存放演示数据模型
  - 删除默认的 MainWindow.axaml 和相关文件

- [x] 0.3 创建演示数据模型
  - 创建 Models/CandidateRecord.cs
  - 添加属性：LicensePlate（车牌号）、Supplier（供料单位）、Weight（车辆重量）、EntryTime（进场时间）、ElapsedTime（相隔时间）
  - 实现 INotifyPropertyChanged 接口

- [x] 0.4 创建模拟数据生成器
  - 创建 ViewModels/DemoDataGenerator.cs
  - 实现 GetDemoRecords() 方法，生成 5-10 条模拟候选记录
  - 确保数据包含不同的车牌号、供料单位和时间

- [x] 0.5 创建方案选择器主窗口
  - 创建 Views/DemoMainWindow.axaml
  - 添加标题："DataGrid 选中方案演示"
  - 添加 4 个按钮，分别对应方案 A、B、C、D
  - 为每个按钮添加点击事件，打开对应的演示窗口

- [x] 0.6 实现方案 A 演示窗口
  - 创建 Views/SchemeAWindow.axaml
  - 添加 DataGrid 控件，绑定到模拟数据
  - 内联定义方案 A 的样式（PrimaryBlue 背景 + 左侧边框 + 白色文字）
  - 支持鼠标悬停和选中交互

- [x] 0.7 实现方案 B 演示窗口
  - 创建 Views/SchemeBWindow.axaml
  - 添加 DataGrid 控件，绑定到模拟数据
  - 内联定义方案 B 的样式（浅蓝背景 + 左侧边框 + 保持文字颜色）
  - 支持鼠标悬停和选中交互

- [x] 0.8 实现方案 C 演示窗口
  - 创建 Views/SchemeCWindow.axaml
  - 添加 DataGrid 控件，绑定到模拟数据
  - 内联定义方案 C 的样式（仅边框指示器 + 阴影）
  - 支持鼠标悬停和选中交互

- [x] 0.9 实现方案 D 演示窗口
  - 创建 Views/SchemeDWindow.axaml
  - 添加 DataGrid 控件，绑定到模拟数据
  - 添加选中图标列（使用模板列）
  - 支持鼠标悬停和选中交互

- [x] 0.10 添加主题切换功能
  - 在主窗口添加主题切换按钮（Light/Dark）
  - 实现主题切换逻辑
  - 确保所有方案窗口支持主题切换

- [x] 0.11 验证演示项目可独立运行
  - 编译 MaterialClient.Demo 项目
  - 运行项目，验证所有方案窗口可正常打开
  - 验证交互效果（悬停、选中、主题切换）
  - 验证无编译错误和运行时错误

## 1. 样式实现

- [x] 1.1 修改 App.axaml 中的 DataGrid 选中行背景色
  - 定位到第 347-350 行的 `DataGridRow:selected` 样式
  - 将 `Fill` 属性值从 `#C8DCFF` 修改为 `#4169E1` (PrimaryBlue)

- [x] 1.2 添加选中行左侧边框指示器
  - 在 App.axaml 的 DataGrid 样式区域添加新的样式选择器
  - 创建 `DataGridRow:selected /template/ Border#PART_SelectedCellIndicator` 样式
  - 设置 `BorderBrush` 为 `#4169E1`
  - 设置 `BorderThickness` 为 `3,0,0,0`（左侧 3px 边框）

- [x] 1.3 添加选中行文字颜色样式
  - 在 App.axaml 中添加 `DataGridRow:selected` 样式选择器
  - 设置 `Foreground` 属性为 `#FFFFFF` (白色)
  - 确保该样式在背景色样式之后定义，保证优先级

- [x] 1.4 确保选中行不受悬停影响
  - 添加 `DataGridRow:selected:pointerover /template/ Rectangle#BackgroundRectangle` 样式
  - 设置 `Fill` 属性为 `#4169E1`（与选中状态一致）
  - 确保选中状态优先级高于悬停状态

## 2. ManualMatchWindow 验证

- [ ] 2.1 验证选中状态视觉效果
  - 启动应用并打开 ManualMatchWindow
  - 点击可匹配订单列表中的任意一行
  - 验证选中行背景色变为 #4169E1
  - 验证选中行左侧显示 3px 蓝色边框
  - 验证选中行内所有文字显示为白色

- [ ] 2.2 验证悬停与选中状态区分
  - 将鼠标悬停在未选中的行上
  - 验证悬停行背景色为 #F0F7FF
  - 将鼠标悬停在已选中的行上
  - 验证选中行保持选中样式，不应用悬停效果

- [ ] 2.3 验证交互功能正常
  - 验证选中行后"确定"按钮变为可用状态
  - 验证可以切换选中不同的行
  - 验证选中状态在翻页后保持（如有分页）

## 3. 全局样式兼容性测试

- [ ] 3.1 测试其他窗口中的 DataGrid
  - 识别应用中所有使用 DataGrid 的窗口（称重记录查询、供应商管理等）
  - 逐个打开窗口并验证选中行样式
  - 确认所有 DataGrid 都应用了新的选中样式
  - 检查是否有样式冲突或异常

- [ ] 3.2 验证 Light 主题兼容性
  - 切换应用主题为 Light 模式
  - 打开 ManualMatchWindow
  - 验证选中行样式在 Light 主题下显示正常
  - 检查背景色、边框和文字颜色的对比度

- [ ] 3.3 验证 Dark 主题兼容性
  - 切换应用主题为 Dark 模式
  - 打开 ManualMatchWindow
  - 验证选中行样式在 Dark 主题下显示正常
  - 如发现问题，记录并调整样式

## 4. 无障碍与可读性验证

- [ ] 4.1 验证颜色对比度符合 WCAG AA 标准
  - 使用颜色对比度检查工具验证选中行背景 (#4169E1) 与文字 (#FFFFFF) 的对比度
  - 确认对比度 ≥ 4.5:1（WCAG AA 标准）
  - 如不达标，调整颜色值

- [ ] 4.2 验证不同 DPI 设置下的显示效果
  - 在 100% DPI 缩放下测试显示效果
  - 在 125% DPI 缩放下测试显示效果
  - 在 150% DPI 缩放下测试显示效果
  - 确保边框和文字在所有缩放级别下清晰可见

## 5. 文档与变更记录

- [ ] 5.1 更新变更日志
  - 记录 DataGrid 选中样式的视觉变更
  - 说明变更原因（提升选中状态的可识别性）
  - 列出受影响的窗口和组件

- [ ] 5.2 更新 UI 规范文档（如有）
  - 更新 DataGrid 选中状态的视觉规范
  - 添加新的颜色值和样式定义
  - 更新 UI 组件库文档（如有）

## 6. 回归测试

- [ ] 6.1 执行 DataGrid 相关功能的回归测试
  - 测试称重记录查询窗口的数据加载和选择
  - 测试供应商管理窗口的数据加载和选择
  - 测试所有包含 DataGrid 的窗口的基本功能
  - 确认无功能回归

- [ ] 6.2 执行手动匹配流程的端到端测试
  - 完整执行手动匹配流程
  - 验证从选择订单到确认匹配的整个流程
  - 确认样式变更不影响业务逻辑

## 完成标准

所有任务完成后，应满足以下条件：

### 演示项目完成标准
1. ✅ MaterialClient.Demo 项目可独立编译和运行
2. ✅ 可通过主窗口访问所有 4 个方案的演示界面
3. ✅ 每个方案窗口展示正确的 DataGrid 样式效果
4. ✅ 支持鼠标悬停、选中、取消选中等交互操作
5. ✅ 支持 Light/Dark 主题切换，并在各主题下正确显示
6. ✅ 模拟数据正常显示，数据包含必要的字段

### 生产环境完成标准
7. ✅ ManualMatchWindow 中的 DataGrid 选中行显示明显的主题色背景 (#4169E1)
8. ✅ 选中行左侧显示 3px 蓝色边框指示器
9. ✅ 选中行文字颜色为白色，可读性良好
10. ✅ 悬停状态与选中状态区分明显
11. ✅ 所有使用 DataGrid 的窗口都应用了统一的新样式
12. ✅ Light/Dark 主题下视觉效果一致
13. ✅ 颜色对比度符合 WCAG AA 标准
14. ✅ 无功能回归，所有 DataGrid 相关功能正常工作
