# 实施任务

## 1. 更新测试模块配置
- [x] 1.1 在 MaterialClientEntityFrameworkCoreTestModule.cs 中添加 using MaterialClient.Common.Api.Dtos
- [x] 1.2 在 ConfigureServices 中添加 IMaterialPlatformApi mock 注册
- [x] 1.3 为 UserLoginAsync 配置默认登录响应行为
- [x] 1.4 在 ConfigureServices 中添加 ISoundDeviceApi mock 注册
- [x] 1.5 为 PlayAudioAsync 配置桩实现

## 2. 验证
- [x] 2.1 成功构建测试项目
- [x] 2.2 运行 dotnet test --filter "FullyQualifiedName~Authentication" 验证认证场景
- [x] 2.3 运行 dotnet test --filter "FullyQualifiedName~Authorization" 验证授权场景
- [x] 2.4 运行 dotnet test --filter "FullyQualifiedName~WeighingService" 验证称重场景
- [x] 2.5 运行 dotnet test --filter "FullyQualifiedName~WeighingMatchingService" 验证匹配场景
- [x] 2.6 运行完整测试套件并验证约 45+ 通过（结果：197 总数，138 通过，26 跳过-硬件，33 失败-测试逻辑问题，非 DI 阻塞）

## 3. 文档
- [x] 3.1 添加说明 mock 注册的行内注释
- [ ] 3.2 若存在则更新 TEST_CONFIGURATION_GUIDE.md 中的 mock 注册模式（已跳过：文件不存在）
- [ ] 3.3 实施成功后归档 TempDocs/FixTest 中的分析文档（已推迟：可在提交后处理）

## 4. 收尾
- [ ] 4.1 移除或归档 TempDocs/FixTest 中的临时分析文档（已推迟：可单独处理）
- [x] 4.2 确认未引入新的 linter 警告（已确认：仅存在既有警告）
- [ ] 4.3 使用描述性提交信息提交变更（就绪：变更已完成并验证）

## 总结

✅ **主要目标已达成**：所有 BDD 场景现均可初始化并运行，无 DI 阻塞错误。

**修复前**：约 20 个 BDD 场景因「Cannot resolve parameter 'IMaterialPlatformApi'」被阻塞；测试无法初始化或运行。

**修复后**：所有 BDD 场景成功初始化；共 197 个测试运行（此前约 49）；无「Cannot resolve parameter」类 DI 错误；mock 注册工作正常；Authentication、Authorization、WeighingService、WeighingMatchingService 等 BDD 功能均可执行。

**剩余问题（范围外）**：33 个测试因测试逻辑问题失败（WeighingRecord 测试中的实体类型不匹配），与 DI mock 注册修复无关，应在后续变更中处理。
