# 任务：UI 测试基础设施实现

## 阶段 1：项目准备工作

- [x] 1.1 审查现有测试基础设施（MaterialClient.Common.Tests 使用 xUnit）
- [x] 1.2 确认与 `separate-ui-to-materialclient-ui` 变更的执行顺序
- [x] 1.3 创建特性分支：`feature/ui-testing-infrastructure`
- [x] 1.4 安装和配置 Avalonia.Headless.XUnit 包的文档和示例

## 阶段 2：创建 MaterialClient.UI 项目

- [x] 2.1 创建 MaterialClient.UI 项目文件夹结构
- [x] 2.2 创建 MaterialClient.UI.csproj 项目文件
- [x] 2.3 配置项目属性（RootNamespace、TargetFramework、Nullable 等）
- [x] 2.4 添加 Avalonia 相关包引用
- [x] 2.5 添加对 MaterialClient.Common 的项目引用
- [x] 2.6 配置 Avalonia 资源嵌入规则
- [x] 2.7 创建 App.axaml 和 App.axaml.cs
- [x] 2.8 创建 Assets 和 Backgrounds 目录结构
- [x] 2.9 验证项目可以成功编译

## 阶段 3：迁移 UI 代码到 MaterialClient.UI

- [x] 3.1 迁移 Views 目录及其所有子目录
- [x] 3.2 迁移 ViewModels 目录
- [x] 3.3 迁移 Controls 目录
- [x] 3.4 迁移 Converters 目录
- [x] 3.5 迁移 App.axaml 和 App.axaml.cs（已在阶段 2 创建）
- [x] 3.6 迁移 Assets 目录中的所有资源文件
- [x] 3.7 迁移 Backgrounds 目录中的所有背景文件
- [x] 3.8 更新所有文件的命名空间引用（MaterialClient.Views → MaterialClient.UI.Views）
- [x] 3.9 更新所有 XAML 文件中的命名空间引用
- [x] 3.10 更新所有 .axaml.cs 文件中的 using 语句
- [x] 3.11 验证 MaterialClient.UI 项目编译无错误
- [x] 3.12 修复编译错误和命名空间问题

## 阶段 4：创建 MaterialClient.UI.Test 项目

- [x] 4.1 创建 MaterialClient.UI.Test 项目文件夹结构
- [x] 4.2 创建 MaterialClient.UI.Test.csproj 测试项目文件
- [x] 4.3 配置测试项目属性（IsTestProject = true）
- [x] 4.4 添加 Avalonia.Headless.XUnit 包引用（版本 11.2.3）
- [x] 4.5 添加 Avalonia.Skia 包引用（版本 11.2.3）
- [x] 4.6 添加 xUnit 测试框架包引用
- [x] 4.7 添加 xunit.runner.visualstudio 包引用
- [x] 4.8 添加 NSubstitute Mock 框架包引用
- [x] 4.9 添加 Shouldly 断言库包引用
- [x] 4.10 添加 coverlet.collector 测试覆盖率包引用
- [x] 4.11 添加对 MaterialClient.UI 的项目引用
- [x] 4.12 添加对 MaterialClient.Common 的项目引用（可选）
- [x] 4.13 验证测试项目可以成功编译

## 阶段 5：创建测试基础设施

- [x] 5.1 创建 TestAppBuilder.cs，配置 Headless 应用构建器
- [x] 5.2 在 TestAppBuilder 中配置 AvaloniaHeadlessPlatformOptions（禁用 GPU，使用软件渲染）
- [x] 5.3 创建 TestBase.cs 基类，提供通用测试辅助方法
- [x] 5.4 添加程序集级别的 AvaloniaTestApplication 属性
- [x] 5.5 创建 TestHelper.cs 静态类，提供控件创建和初始化辅助方法
- [x] 5.6 创建 Mocks 目录和 Mock 工厂类
- [x] 5.7 实现常用 Mock 服务创建方法（AuthService、DataService 等）
- [x] 5.8 编写 TestAppBuilder 单元测试验证配置正确性
- [x] 5.9 验证测试基础设施可以在无头模式下运行

## 阶段 6：编写 ViewModel 测试

- [x] 6.1 创建 ViewModels 测试目录结构
- [x] 6.2 编写 LoginViewModelTests.cs 测试
  - [x] 6.2.1 测试构造函数初始化
  - [x] 6.2.2 测试 LoginCommand 执行
  - [x] 6.2.3 测试属性变更通知
  - [x] 6.2.4 测试 CanLogin 状态
- [x] 6.3 编写 MainWindowViewModelTests.cs 测试
  - [x] 6.3.1 测试窗口初始化
  - [x] 6.3.2 测试导航命令
- [x] 6.4 编写 SettingsWindowViewModelTests.cs 测试
  - [x] 6.4.1 测试设置加载
  - [x] 6.4.2 测试设置保存
  - [x] 6.4.3 测试设置验证
- [x] 6.5 为其他关键 ViewModel 编写测试
- [x] 6.6 运行所有 ViewModel 测试，验证通过
- [x] 6.7 修复失败的测试

## 阶段 7：编写 Converter 测试

- [x] 7.1 创建 Converters 测试目录结构
- [x] 7.2 编写 NullOrEmptyImageConverterTests.cs 测试
  - [x] 7.2.1 测试 null 值转换
  - [x] 7.2.2 测试空字符串转换
  - [x] 7.2.3 测试有效路径转换
- [ ] 7.3 编写 CarNullOrEmptyImageConverterTests.cs 测试
  - [ ] 7.3.1 测试 null 值转换
  - [ ] 7.3.2 测试空字符串转换
  - [ ] 7.3.3 测试有效路径转换
- [ ] 7.4 为其他 Converter 编写测试
- [ ] 7.5 运行所有 Converter 测试，验证通过
- [ ] 7.6 修复失败的测试

## 阶段 8：编写自定义控件测试

- [x] 8.1 创建 Controls 测试目录结构
- [ ] 8.2 为关键自定义控件编写测试
- [ ] 8.3 测试控件创建和初始化
- [ ] 8.4 测试控件属性设置和获取
- [ ] 8.5 测试控件事件触发
- [ ] 8.6 测试控件交互行为
- [ ] 8.7 运行所有控件测试，验证通过
- [ ] 8.8 修复失败的测试

## 阶段 9：编写集成测试

- [x] 9.1 创建 Integration 测试目录结构
- [ ] 9.2 编写 LoginWindow 集成测试
  - [ ] 9.2.1 测试窗口创建和显示
  - [ ] 9.2.2 测试表单提交流程
  - [ ] 9.2.3 测试错误处理和显示
- [ ] 9.3 编写简单场景的用户流程集成测试
- [ ] 9.4 测试多个组件协同工作
- [ ] 9.5 验证数据流从 UI 到 ViewModel 再到服务
- [ ] 9.6 运行所有集成测试，验证通过
- [ ] 9.7 修复失败的测试

## 阶段 10：更新 MaterialClient 主项目

- [ ] 10.1 从 MaterialClient.csproj 中移除 UI 相关包引用
- [ ] 10.2 从 MaterialClient.csproj 中移除 UI 相关的 Compile Update 配置
- [ ] 10.3 添加对 MaterialClient.UI 的项目引用
- [ ] 10.4 更新 Program.cs，配置应用程序以使用 MaterialClient.UI
- [ ] 10.5 更新 MaterialClientModule.cs，注册 UI 相关服务
- [ ] 10.6 从 MaterialClient 项目中删除已迁移的目录
- [ ] 10.7 验证解决方案可以成功编译
- [ ] 10.8 验证应用程序可以正常启动和运行

## 阶段 11：更新解决方案文件

- [x] 11.1 在 MaterialClient.sln 中添加 MaterialClient.UI 项目引用
- [x] 11.2 在 MaterialClient.sln 中添加 MaterialClient.UI.Test 项目引用
- [x] 11.3 配置项目启动顺序和依赖关系
- [x] 11.4 验证解决方案可以在 Visual Studio 中正常打开
- [x] 11.5 验证解决方案可以成功编译

## 阶段 12：配置测试覆盖率

- [ ] 12.1 配置 coverlet.collector 收集器
- [ ] 12.2 设置测试覆盖率目标（如 80%）
- [ ] 12.3 配置覆盖率报告格式（HTML、Cobertura）
- [ ] 12.4 在测试项目中添加覆盖率配置文件
- [ ] 12.5 运行测试并生成覆盖率报告
- [ ] 12.6 审查覆盖率报告，识别未覆盖的代码区域
- [ ] 12.7 为关键未覆盖区域添加测试

## 阶段 13：CI/CD 集成

- [ ] 13.1 识别和审查现有 CI/CD 配置
- [ ] 13.2 在 CI/CD 管道中添加 UI 测试运行步骤
- [ ] 13.3 配置测试在无头环境中运行
- [ ] 13.4 配置测试结果报告上传（JUnit 格式）
- [ ] 13.5 配置测试覆盖率报告上传（Cobertura 格式）
- [ ] 13.6 设置测试覆盖率质量门控
- [ ] 13.7 在 CI/CD 环境中验证测试运行
- [ ] 13.8 验证测试结果和覆盖率报告正确生成

## 阶段 14：文档和指南

- [x] 14.1 创建测试编写指南文档
- [x] 14.2 记录 Mock 工厂的使用方法
- [ ] 14.3 创建测试最佳实践文档
- [x] 14.4 在 MaterialClient.UI.Test 中添加示例测试文件
- [x] 14.5 更新项目 README.md，说明测试基础设施
- [ ] 14.6 添加测试覆盖率说明文档

## 阶段 15：验证和测试

- [x] 15.1 在本地开发环境运行所有测试
- [ ] 15.2 验证测试在无头模式下正常运行
- [ ] 15.3 验证测试覆盖率符合目标
- [x] 15.4 测试应用程序启动和基本功能
- [ ] 15.5 执行回归测试，确保现有功能正常工作
- [ ] 15.6 验证 CI/CD 管道成功运行测试
- [ ] 15.7 检查测试执行时间和性能
- [ ] 15.8 优化测试性能（如果需要）

## 阶段 16：代码审查和优化

- [ ] 16.1 审查测试代码质量
- [ ] 16.2 检查测试覆盖率分布
- [ ] 16.3 优化 Mock 工厂和辅助方法
- [ ] 16.4 重构重复的测试代码
- [ ] 16.5 改进测试可读性和可维护性
- [ ] 16.6 添加缺失的测试文档注释

## 阶段 17：协作和协调

- [ ] 17.1 与 `separate-ui-to-materialclient-ui` 变更协调执行
- [ ] 17.2 确认变更执行顺序
- [ ] 17.3 解决潜在的冲突和依赖问题
- [ ] 17.4 同步命名空间和代码风格
- [ ] 17.5 验证两个变更可以正确集成

---

**关键路径**：阶段 2–10（创建 MaterialClient.UI 和 MaterialClient.UI.Test 项目、迁移代码、更新主项目）
**重要阶段**：阶段 5（测试基础设施）、阶段 6–9（编写测试）、阶段 13（CI/CD 集成）
**验证阶段**：阶段 12–15（覆盖率配置、CI/CD 集成、验证测试）

**预估工时**：约 24–32 小时（关键路径）
**预估总工时**：约 32–40 小时（包含文档和优化）
