# 变更：集中构建配置与包管理

**变更 ID**：`centralize-build-configuration`
**状态**：Draft
**创建日期**：2026-01-23
**类型**：Refactoring

---

## 原因

### 背景

当前包引用与构建设置分散在各 `.csproj` 中。AutoConstructor 仅在 `MaterialClient.Common.csproj` 中引用，但应对所有项目可用。包版本在多个项目文件中重复，导致版本管理与更新繁琐。

### 问题

1. **包可用性不一致**：AutoConstructor 仅在一个项目中引用，其他项目可能也需要
2. **版本管理开销**：包版本在多个 `.csproj` 中重复，需在多处手动更新
3. **维护负担**：添加公共包或更新版本需编辑多个文件
4. **版本漂移风险**：不同项目可能误用同包的不同版本

---

## 变更内容

### 概览

本变更通过 MSBuild 目录级 props 文件集中通用构建配置与包版本管理：
- `Directory.Build.props`：通用设置与包引用（如 AutoConstructor）
- `Directory.Packages.props`：通过 Central Package Management (CPM) 集中管理包版本

### 详细变更

1. **在解决方案根目录创建 `Directory.Build.props`**：
   - 添加 AutoConstructor 包引用及合适元数据（如 `PrivateAssets` 设为 `all`，`IncludeAssets` 设为 `runtime; build; native; contentfiles; analyzers`），使 AutoConstructor 自动对所有项目可用

2. **在解决方案根目录创建 `Directory.Packages.props`**：
   - 启用集中包管理（`ManagePackageVersionsCentrally=true`）
   - 在一处定义所有包版本
   - 项目引用包时不写版本号（版本来自 Directory.Packages.props）

3. **更新所有 `.csproj`**：
   - 从 `MaterialClient.Common.csproj` 移除 AutoConstructor 包引用（已移至 Directory.Build.props）
   - 从 PackageReference 元素中移除版本号（版本来自 Directory.Packages.props）
   - 保留项目特有包引用但不写版本号

---

## 影响

### 预期收益

- **一致性**：所有项目自动获得 AutoConstructor 等公共包
- **可维护性**：包版本单一事实来源
- **效率**：在一处更新包版本而非多文件
- **减少错误**：消除项目间版本不一致风险
- **标准实践**：符合现代 .NET 项目管理最佳实践

### 风险与缓解

| 风险 | 影响 | 缓解 |
|------|--------|------------|
| 若未识别 Directory.Packages.props 可能导致构建失败 | 高 | 确认 .NET SDK 支持 CPM（需 .NET SDK 6.0+），变更后测试构建 |
| 各项目包需求可能不同 | 低 | Directory.Build.props 仅添加公共包；项目特有包仍在 .csproj |
| 迁移复杂度 | 中 | 逐项目更新，增量测试构建 |

### 受影响文件

- `Directory.Build.props`（新建）
- `Directory.Packages.props`（新建）
- `MaterialClient.Common/MaterialClient.Common.csproj`
- `MaterialClient.Common.Tests/MaterialClient.Common.Tests.csproj`
- `MaterialClient/MaterialClient.csproj`
- `MaterialClientToolkit/MaterialClientToolkit.csproj`（若存在）

---

## 成功标准

- [ ] 已创建含 AutoConstructor 引用的 `Directory.Build.props`
- [ ] 已创建含所有包版本的 `Directory.Packages.props`
- [ ] 所有 `.csproj` 已更新，从 PackageReference 移除版本号
- [ ] 已从各项目文件中移除 AutoConstructor
- [ ] 解决方案构建成功
- [ ] 所有项目可使用 AutoConstructor 特性
- [ ] 包版本已集中管理

---

## 后续步骤

1. 审阅并批准本提案
2. 按 tasks.md 实施变更
3. 测试构建并验证 AutoConstructor 在所有项目中可用
4. 验证包版本管理

---

## 参考

- [MSBuild Directory.Build.props 文档](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-your-build)
- [Central Package Management (CPM) 文档](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
