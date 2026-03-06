# 变更：将 View 相关内容分离到 MaterialClient.UI 项目

## 原因

**当前架构问题**：

1. **单一项目职责不清**：MaterialClient 项目混合了业务逻辑、服务层和 UI 层，违反了单一职责原则。这使得项目难以维护、测试和扩展。

2. **UI 与业务逻辑耦合**：View 层（.axaml 文件和代码隐藏）直接与 ViewModel、Service 层耦合，增加了重构的复杂度。

3. **无法独立测试 UI 组件**：UI 组件被嵌入在主项目中，无法独立进行单元测试和集成测试。

4. **不利于团队协作**：大型项目中，UI 开发和业务逻辑开发难以并行进行，影响开发效率。

5. **项目依赖关系混乱**：MaterialClient 项目既依赖于 MaterialClient.Common，又包含所有 UI 内容，形成了一个庞大的单体项目。

**最佳实践参考**：

- **Avalonia 官方推荐**：UI 项目应与业务逻辑项目分离，便于维护和测试
- **企业级应用架构**：UI 层、业务层、数据层分离是成熟的做法（参考 .NET MAUI、WPF 分层架构）
- **可复用性**：独立的 UI 项目可以被其他应用引用和复用

## 变更内容

### 项目结构调整

1. **创建新项目**：`MaterialClient.UI`
   - 项目类型：Avalonia 项目（类库或应用程序）
   - 命名空间：`MaterialClient.UI`
   - 依赖项：MaterialClient.Common、Avalonia 相关包

2. **迁移 UI 内容**：
   - `Views/` 目录及其所有子目录（Windows、Dialogs、Controls）
   - `ViewModels/` 目录
   - `Controls/` 目录（自定义控件）
   - `Converters/` 目录（值转换器）
   - `App.axaml` 及相关资源文件

3. **更新解决方案**：
   - 在 `MaterialClient.sln` 中添加 `MaterialClient.UI` 项目引用
   - 建立项目依赖关系：MaterialClient → MaterialClient.UI → MaterialClient.Common

4. **更新项目引用**：
   - `MaterialClient` 项目添加对 `MaterialClient.UI` 的引用
   - 将 `MaterialClient` 项目从 WinExe 改为依赖 MaterialClient.UI 的启动器

### 依赖关系调整

**调整前**：
```
MaterialClient (WinExe)
    └─ MaterialClient.Common
```

**调整后**：
```
MaterialClient (启动器)
    └─ MaterialClient.UI
         └─ MaterialClient.Common
```

### 文件迁移清单

| 来源路径 | 目标路径 | 迁移类型 |
|---------|---------|---------|
| `MaterialClient/Views/**/*.axaml` | `MaterialClient.UI/Views/**/*.axaml` | 迁移 |
| `MaterialClient/Views/**/*.axaml.cs` | `MaterialClient.UI/Views/**/*.axaml.cs` | 迁移 |
| `MaterialClient/ViewModels/` | `MaterialClient.UI/ViewModels/` | 迁移 |
| `MaterialClient/Controls/` | `MaterialClient.UI/Controls/` | 迁移 |
| `MaterialClient/Converters/` | `MaterialClient.UI/Converters/` | 迁移 |
| `MaterialClient/App.axaml` | `MaterialClient.UI/App.axaml` | 迁移 |
| `MaterialClient/App.axaml.cs` | `MaterialClient.UI/App.axaml.cs` | 迁移 |
| `MaterialClient/Assets/` | `MaterialClient.UI/Assets/` | 迁移 |
| `MaterialClient/Backgrounds/` | `MaterialClient.UI/Backgrounds/` | 迁移 |

### 保留在 MaterialClient 的内容

- `Program.cs`：应用程序入口点
- `MaterialClientModule.cs`：依赖注入配置
- `Services/`：业务逻辑服务
- `McpTools/`：MCP 工具
- `UI.Abstractions/`：UI 抽象接口
- 配置文件：`appsettings.json`、`appsettings.secret.json`

### 包依赖迁移

将以下包引用从 `MaterialClient.csproj` 迁移到 `MaterialClient.UI.csproj`：

```xml
<PackageReference Include="Avalonia" />
<PackageReference Include="Avalonia.Desktop" />
<PackageReference Include="Avalonia.ReactiveUI" />
<PackageReference Include="Avalonia.Themes.Fluent" />
<PackageReference Include="Avalonia.Fonts.Inter" />
<PackageReference Include="Avalonia.Controls.DataGrid" />
<PackageReference Include="AvaloniaUI.DiagnosticsSupport" />
<PackageReference Include="Irihi.Avalonia.Shared" />
<PackageReference Include="Irihi.Ursa" />
<PackageReference Include="Irihi.Ursa.Themes.Semi" />
<PackageReference Include="MessageBox.Avalonia" />
<PackageReference Include="ReactiveUI.SourceGenerators" />
<PackageReference Include="Semi.Avalonia" />
```

## 影响

**受影响规范**：
- 项目架构规范（新增）
- UI 开发规范（新增）

**新增文件**：
- `MaterialClient.UI/MaterialClient.UI.csproj`
- 迁移后的所有 UI 相关文件

**修改文件**：
- `MaterialClient.sln`：添加新项目引用
- `MaterialClient/MaterialClient.csproj`：移除 UI 相关包引用，添加 MaterialClient.UI 项目引用
- `MaterialClient/Program.cs`：更新启动逻辑以使用 MaterialClient.UI

**破坏性变更**：
- 项目结构变更需要开发者适应新的项目布局
- 命名空间从 `MaterialClient.Views` 变更为 `MaterialClient.UI.Views`（可通过 using 别名平滑过渡）

**迁移策略**：
1. 采用渐进式迁移，确保每一步都可以编译通过
2. 在迁移过程中保持旧的命名空间别名，逐步替换
3. 提供详细的迁移指南和检查清单

## 成功标准

1. MaterialClient.UI 项目可以独立编译，无错误
2. MaterialClient 项目引用 MaterialClient.UI 后可以正常启动和运行
3. 所有现有 UI 功能保持不变，无回归
4. 项目结构清晰，职责分明
5. 可以对 MaterialClient.UI 进行独立的单元测试
6. 文档更新，反映新的项目结构
