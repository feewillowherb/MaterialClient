## 原因

Views 文件夹中混合了控件与视图的 XAML 文件，且无清晰分类，导致维护与导航困难。缺乏组织会增加开发者的认知负担、拖慢文件查找，并妨碍新成员上手。

## 变更内容

- 建立 `Views/Controls` 目录结构，将控件文件与视图文件分离
- 将 `Views/` 中所有与控件相关的 XAML 文件（自定义控件、用户控件、控件模板）移至 `Views/Controls/`
- 更新所有受影响 XAML 文件中的命名空间引用，改用新的 `Controls` 命名空间
- 更新项目文件（.csproj）中的文件路径以反映新位置
- 保持现有功能不变——此为重构，非行为变更

## 能力

### 新增能力
- `file-organization-controls`：在独立的 `Views/Controls` 文件夹中组织与控件相关的 XAML 文件，并实现命名空间隔离

### 修改的能力
- 无（此为纯重构，无需求层面的修改）

## 影响

**受影响代码**：
- `Views/` 中引用已移动控件的所有 XAML 文件（命名空间更新）
- 已移动控件的所有 C# 代码隐藏文件（命名空间更新）
- 项目文件（.csproj）中的文件路径引用
- App.axaml 或引用已移动控件的资源字典

**无影响**：
- API 端点（本类项目无）
- 外部依赖
- 数据库架构
- 运行时行为（功能等价）

**开发影响**：
- 提升代码可发现性与可维护性
- 符合 Avalonia 项目最佳实践
- 便于后续模块化与组件复用

---

## 可视化

### 当前文件结构
（见原文：Views/ 下 MainWindow、LoginWindow、Dashboard、CustomButton、DataGridControl、SearchBox 等混合。）

### 目标文件结构
（见原文：Views/ 下仅窗口与页面；Views/Controls/ 下为所有控件。）

### 迁移流程
（见原文 mermaid 时序图：分析 Views → 识别控件 → 创建 Views/Controls → 移动文件 → 更新命名空间与引用 → 构建与测试 → 迁移成功。）

### 代码变更清单
（见原文表格：Views/*.axaml 命名空间更新、Views/Controls/* 移动并更新、.csproj 路径更新、App.axaml 可选资源更新。）
