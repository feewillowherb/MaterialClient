## 1. 准备

- [x] 1.1 确认 .NET SDK 版本支持 Central Package Management（需 SDK 6.0+）
- [x] 1.2 列出解决方案中所有 .csproj 以确定受影响项目
- [x] 1.3 从所有 .csproj 中提取全部唯一包引用及其版本

## 2. 创建 Directory.Build.props

- [x] 2.1 在解决方案根目录创建 `Directory.Build.props`
- [x] 2.2 添加 AutoConstructor 包引用及元数据（PrivateAssets=all，IncludeAssets=runtime; build; native; contentfiles; analyzers）
- [x] 2.3 确认文件结构正确

## 3. 创建 Directory.Packages.props

- [x] 3.1 在解决方案根目录创建 `Directory.Packages.props`
- [x] 3.2 启用集中包管理：ManagePackageVersionsCentrally=true
- [x] 3.3 为步骤 1.3 中所有包添加 ItemGroup/PackageVersion 条目
- [x] 3.4 确保所有包版本与 .csproj 中当前版本一致

## 4. 更新 MaterialClient.Common.csproj

- [x] 4.1 移除 AutoConstructor 的 PackageReference（已移至 Directory.Build.props）
- [x] 4.2 移除所有 PackageReference 的版本属性
- [x] 4.3 确认包引用仍正常工作

## 5. 更新 MaterialClient.Common.Tests.csproj

- [x] 5.1 移除所有 PackageReference 的版本属性
- [x] 5.2 确认所有包均在 Directory.Packages.props 中定义
- [x] 5.3 确认包引用仍正常工作

## 6. 更新 MaterialClient.csproj

- [x] 6.1 移除所有 PackageReference 的版本属性
- [x] 6.2 确认所有包均在 Directory.Packages.props 中定义
- [x] 6.3 确认包引用仍正常工作

## 7. 更新其他项目（若有）

- [x] 7.1 检查是否存在 MaterialClientToolkit.csproj 或其他项目
- [x] 7.2 在其余项目中移除 PackageReference 的版本属性
- [x] 7.3 确认所有包均在 Directory.Packages.props 中定义

## 8. 验证

- [x] 8.1 运行 dotnet restore 验证包解析
- [x] 8.2 运行 dotnet build 验证编译成功
- [x] 8.3 验证 AutoConstructor 源生成器在所有项目中生效（检查生成构造函数）
- [ ] 8.4 运行现有测试以确保无回归
- [x] 8.5 确认无重复包引用或版本冲突

## 9. 文档

- [x] 9.1 如需则更新 project.md 以记录新构建配置方式
- [x] 9.2 在 Directory.Build.props 中添加说明其用途的注释
- [x] 9.3 在 Directory.Packages.props 中添加说明 CPM 用法的注释
