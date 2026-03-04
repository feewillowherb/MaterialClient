## ADDED 需求

### 需求：集中构建配置

系统应在解决方案根目录使用 `Directory.Build.props` 定义适用于所有项目的通用构建设置与包引用。

#### 场景：AutoConstructor 对所有项目可用
- **当** 解决方案中的项目使用 `[AutoConstructor]` 特性
- **则** AutoConstructor 源生成器应可用，且无需在项目 `.csproj` 中显式包引用
- **且** 包引用应在 `Directory.Build.props` 中定义，且 PrivateAssets 为 all、IncludeAssets 为 runtime; build; native; contentfiles; analyzers

#### 场景：公共包自动可用
- **当** 在 `Directory.Build.props` 中添加包引用
- **则** 解决方案中所有项目应自动可访问该包
- **且** 项目无需在其 `.csproj` 中显式引用该包

### 需求：集中包版本管理

系统应使用 `Directory.Packages.props` 配合 Central Package Management (CPM) 在单一位置管理所有包版本。

#### 场景：包版本集中定义
- **当** 在 `Directory.Packages.props` 中更新某包版本
- **则** 使用该包的所有项目应自动使用更新后版本
- **且** 项目在其 `.csproj` 中引用包时不写版本号

#### 场景：跨项目版本一致
- **当** 多个项目引用同一包
- **则** 所有项目应使用 `Directory.Packages.props` 中定义的同一版本
- **且** 构建系统应避免版本冲突

#### 场景：无版本号的包引用
- **当** 项目在其 `.csproj` 中添加 PackageReference
- **则** PackageReference 不应包含 Version 属性
- **且** 版本应从 `Directory.Packages.props` 解析
- **除非** 该包为项目专用且未在 `Directory.Packages.props` 中定义

### 需求：构建配置文件结构

系统应在解决方案根目录维护构建配置文件。

#### 场景：解决方案根目录存在 Directory.Build.props
- **当** 构建解决方案
- **则** 解决方案根目录应存在 `Directory.Build.props`
- **且** 其应被解决方案中所有项目自动导入

#### 场景：解决方案根目录存在 Directory.Packages.props
- **当** 构建解决方案
- **则** 解决方案根目录应存在 `Directory.Packages.props`
- **且** 其中应包含 ManagePackageVersionsCentrally=true
- **且** 其中应包含所有包版本定义

#### 场景：项目引用包时不写版本
- **当** `.csproj` 中包含 PackageReference
- **则** PackageReference 不应包含 Version 属性
- **且** 版本应从 `Directory.Packages.props` 解析
- **除非** 该包未在 `Directory.Packages.props` 中定义（项目专用包）
