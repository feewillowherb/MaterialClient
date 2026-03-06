## Why

当前项目缺乏 UI 自动化测试基础设施，使得 UI 组件难以在 CI/CD 环境中进行测试。`separate-ui-to-materialclient-ui` 变更正在将 UI 层分离到独立的 MaterialClient.UI 项目，这是建立 UI 测试基础设施的前提条件。为了确保 UI 组件的质量和可靠性，需要创建基于 Avalonia Headless 模式的测试项目，使其能够在无图形界面的环境中运行自动化测试。

## What Changes

### 项目创建

1. **MaterialClient.UI 项目**
   - 创建独立的 Avalonia UI 类库项目
   - 从 MaterialClient 主项目迁移所有 UI 相关代码（Views、ViewModels、Controls、Converters、Resources）
   - 配置项目依赖和资源嵌入规则

2. **MaterialClient.UI.Test 项目**
   - 基于 Avalonia.Headless.XUnit 的测试项目
   - 配置为在无头模式下运行测试
   - 引用 MaterialClient.UI 项目进行 UI 组件测试

### 配置和基础设施

- 配置 Avalonia Headless 测试环境
- 设置测试应用程序构建器（TestAppBuilder）
- 创建测试基类和辅助工具
- 配置 CI/CD 友好的测试运行环境

### 测试框架

- 使用 xUnit 作为测试框架（与现有 MaterialClient.Common.Tests 保持一致）
- 使用 NSubstitute 进行 Mock（与现有测试基础设施保持一致）
- 集成 Avalonia.Headless.XUnit 进行 UI 测试

## Capabilities

### New Capabilities

- `ui-testing`: 提供 UI 组件的自动化测试能力，支持 ViewModel、Converter、Control 和 Window 的单元测试和集成测试

### Modified Capabilities

- 无现有能力需要修改

## Impact

**受影响的代码和项目**：
- 创建 MaterialClient.UI 项目，包含所有 UI 代码
- 创建 MaterialClient.UI.Test 项目，包含 UI 测试代码
- MaterialClient 主项目移除 UI 相关代码，成为启动器
- MaterialClient.sln 添加两个新项目引用

**新增的依赖**：
- Avalonia.Headless.XUnit（用于 UI 测试）
- Avalonia.Skia（用于 Headless 渲染支持）

**测试覆盖范围**：
- ViewModel 的业务逻辑和状态管理
- Converter 的值转换逻辑
- 自定义 Control 的逻辑
- Window 的集成测试（可选）

**CI/CD 支持**：
- 测试可以在无图形界面的环境中运行
- 支持持续集成管道中的自动化测试
- 生成测试覆盖率报告

**与其他变更的关系**：
- 与 `separate-ui-to-materialclient-ui` 变更密切关联
- 建议与该变更合并或顺序执行（先完成 UI 项目分离，再创建测试项目）
- 如果 `separate-ui-to-materialclient-ui` 已完成，本变更仅创建测试项目
