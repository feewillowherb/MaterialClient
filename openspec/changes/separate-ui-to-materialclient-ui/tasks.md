# 任务：将 View 相关内容分离到 MaterialClient.UI 项目

## 阶段 1：准备工作
- [ ] 1.1 审查当前 MaterialClient 项目结构，确认所有需要迁移的 UI 文件
- [ ] 1.2 分析项目间的依赖关系，识别潜在的循环依赖
- [ ] 1.3 备份当前项目状态（创建 git tag 或分支）
- [ ] 1.4 确认迁移计划并获得团队批准

## 阶段 2：创建 MaterialClient.UI 项目
- [ ] 2.1 在解决方案根目录创建 `MaterialClient.UI` 文件夹
- [ ] 2.2 创建 `MaterialClient.UI/MaterialClient.UI.csproj` 项目文件
- [ ] 2.3 配置项目属性（Avalonia 支持、编译绑定等）
- [ ] 2.4 添加必要的 NuGet 包引用（Avalonia、ReactiveUI 等）
- [ ] 2.5 添加对 MaterialClient.Common 的项目引用
- [ ] 2.6 配置资源文件包含规则
- [ ] 2.7 在解决方案文件中添加新项目引用

## 阶段 3：迁移 View 文件
- [ ] 3.1 在 MaterialClient.UI 中创建 `Views/` 目录结构
- [ ] 3.2 迁移 `Views/MainWindow.axaml` 及其代码隐藏
- [ ] 3.3 迁移 `Views/LoginWindow.axaml` 及其代码隐藏
- [ ] 3.4 迁移 `Views/AuthCodeWindow.axaml` 及其代码隐藏
- [ ] 3.5 迁移 `Views/ManualMatchWindow.axaml` 及其代码隐藏
- [ ] 3.6 迁移 `Views/ManualMatchEditWindow.axaml` 及其代码隐藏
- [ ] 3.7 迁移 `Views/ImageViewerWindow.axaml` 及其代码隐藏
- [ ] 3.8 迁移 `Views/PrintPreviewWindow.axaml` 及其代码隐藏
- [ ] 3.9 迁移 `Views/ProjectInfoWindow.axaml` 及其代码隐藏
- [ ] 3.10 迁移 `Views/SettingsWindow.axaml` 及其代码隐藏
- [ ] 3.11 迁移 `Views/AttendedWeighing/` 目录下的所有文件
- [ ] 3.12 迁移 `Views/Dialogs/` 目录下的所有文件
- [ ] 3.13 迁移 `Views/Controls/` 目录下的所有文件
- [ ] 3.14 更新所有 .axaml.cs 文件中的命名空间引用

## 阶段 4：迁移 ViewModel 文件
- [ ] 4.1 在 MaterialClient.UI 中创建 `ViewModels/` 目录
- [ ] 4.2 迁移所有 ViewModel 文件
- [ ] 4.3 更新命名空间和 using 语句
- [ ] 4.4 验证 ViewModel 与 Service 的依赖关系

## 阶段 5：迁移控件和转换器
- [ ] 5.1 在 MaterialClient.UI 中创建 `Controls/` 目录
- [ ] 5.2 迁移所有自定义控件
- [ ] 5.3 在 MaterialClient.UI 中创建 `Converters/` 目录
- [ ] 5.4 迁移所有值转换器
- [ ] 5.5 更新命名空间引用

## 阶段 6：迁移应用资源和启动文件
- [ ] 6.1 在 MaterialClient.UI 中创建 `Assets/` 目录并迁移所有资源文件
- [ ] 6.2 在 MaterialClient.UI 中创建 `Backgrounds/` 目录并迁移文件
- [ ] 6.3 迁移 `App.axaml` 到 MaterialClient.UI
- [ ] 6.4 迁移 `App.axaml.cs` 到 MaterialClient.UI
- [ ] 6.5 更新 App.axaml 中的资源引用路径
- [ ] 6.6 配置 Avalonia 资源嵌入

## 阶段 7：更新 MaterialClient 主项目
- [ ] 7.1 从 MaterialClient.csproj 中移除 UI 相关的 NuGet 包引用
- [ ] 7.2 从 MaterialClient.csproj 中移除 UI 相关的 Compile Update 配置
- [ ] 7.3 添加对 MaterialClient.UI 的项目引用
- [ ] 7.4 更新 Program.cs 以正确启动 MaterialClient.UI 应用
- [ ] 7.5 更新 MaterialClientModule.cs 以注册 MaterialClient.UI 的服务
- [ ] 7.6 删除已迁移的文件夹（Views、ViewModels、Controls、Converters、Assets、Backgrounds）
- [ ] 7.7 保留 ViewLocator.cs 或迁移到合适的位置

## 阶段 8：命名空间和引用更新
- [ ] 8.1 在 MaterialClient.UI 中添加命名空间别名（可选，用于平滑过渡）
- [ ] 8.2 更新 MaterialClient 项目中的 using 语句
- [ ] 8.3 验证所有命名空间引用正确
- [ ] 8.4 更新 XAML 中的 x:Class 和 xmlns 引用

## 阶段 9：构建和编译
- [ ] 9.1 清理解决方案（Clean Solution）
- [ ] 9.2 重新构建解决方案（Rebuild Solution）
- [ ] 9.3 修复所有编译错误
- [ ] 9.4 解决项目引用问题
- [ ] 9.5 验证资源文件正确加载

## 阶段 10：运行时测试
- [ ] 10.1 启动应用程序，验证主窗口正确显示
- [ ] 10.2 测试登录功能
- [ ] 10.3 测试所有窗口的打开和关闭
- [ ] 10.4 测试自定义控件的渲染
- [ ] 10.5 测试值转换器的功能
- [ ] 10.6 测试图片和资源的加载
- [ ] 10.7 测试人工称重流程的所有 UI 交互
- [ ] 10.8 测试设置窗口的所有功能

## 阶段 11：代码审查和优化
- [ ] 11.1 审查代码，确保没有遗留的 UI 代码在 MaterialClient 项目中
- [ ] 11.2 检查 MaterialClient.UI 中的依赖关系，确保没有引用 MaterialClient（仅引用 MaterialClient.Common）
- [ ] 11.3 验证 UI 抽象接口（UI.Abstractions）的正确使用
- [ ] 11.4 添加 XML 文档注释到新项目
- [ ] 11.5 代码格式化和一致性检查

## 阶段 12：文档更新
- [ ] 12.1 更新项目 README.md，反映新的项目结构
- [ ] 12.2 更新开发指南，说明如何在 MaterialClient.UI 中开发新功能
- [ ] 12.3 创建项目架构文档，说明各项目职责和依赖关系
- [ ] 12.4 更新贡献者指南，说明代码提交规范

## 阶段 13：测试和验证
- [ ] 13.1 执行完整的回归测试
- [ ] 13.2 验证所有现有功能正常工作
- [ ] 13.3 检查内存使用和性能是否受影响
- [ ] 13.4 测试应用程序打包和发布
- [ ] 13.5 验证在不同操作系统平台上的运行（如适用）

## 阶段 14：提交和合并
- [ ] 14.1 提交所有更改到版本控制
- [ ] 14.2 创建 Pull Request 并请求代码审查
- [ ] 14.3 解决审查意见
- [ ] 14.4 合并到主分支
- [ ] 14.5 更新变更日志

## 阶段 15：后续优化（可选）
- [ ] 15.1 为 MaterialClient.UI 添加独立的单元测试项目
- [ ] 15.2 优化项目依赖关系，移除不必要的包引用
- [ ] 15.3 重构 UI 抽象接口，改进解耦
- [ ] 15.4 考虑将 UI 样式提取到独立的主题项目

---

**关键路径**：阶段 2–10（创建项目、迁移文件、更新主项目、构建和运行时测试）
**重要阶段**：阶段 7–8（确保项目依赖和命名空间正确）
**验证阶段**：阶段 9–10（确保编译通过和功能正常）

**预估工时**：约 8–12 小时（关键路径）
**预估总工时**：约 12–16 小时（包含文档和优化）
