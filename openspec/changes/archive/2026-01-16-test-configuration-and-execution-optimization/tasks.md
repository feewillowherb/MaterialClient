# 任务：测试配置与执行优化

**变更 ID**：`test-configuration-and-execution-optimization`
**总任务数**：4
**预估工期**：0.5 天

---

## 任务概览

本任务将分两个阶段进行：首先修复配置文件部署问题，然后评估和优化测试执行性能。第一阶段是必需的，第二阶段根据实际情况决定是否需要。

---

## 阶段 1：配置文件修复

### 任务 1.1：移除对 appsettings.json 的依赖

**状态**：已完成
**优先级**：高
**预估**：15 分钟
**实际**：15 分钟

**描述**：
移除测试项目对 appsettings.json 文件的依赖，改为使用内存配置（In-Memory Configuration）。这使得每个测试场景可以独立配置，更加快速和隔离。

**已做变更**：

1. **修改**：`MaterialClientTestBase.cs`
   - 移除：对 `AddJsonFile("appsettings.json")` 的依赖
   - 新增：带默认测试值的内存配置
   - 收益：测试更快、更隔离

2. **修改**：`MaterialClient.Common.Tests.csproj`
   - 移除：`<CopyToOutputDirectory>` 配置
   - 移除：文件部署要求
   - 收益：无构建时文件依赖

3. **新建**：`ConfigurationTestExamples.cs`
   - 展示不同配置策略的示例
   - 演示按测试覆盖配置
   - **推荐做法**：在场景初始化中用 `IOptions<T>` 替换
   - 含 WeighingConfiguration、SystemSettings 等示例

4. **新建**：`TEST_CONFIGURATION_GUIDE.md`
   - 测试配置最佳实践完整指南
   - 说明在测试场景中如何替换 `IOptions<XXX>`
   - 含迁移指南与完整示例

**新实现**：
```csharp
protected override void BeforeAddApplication(IServiceCollection services)
{
    // 使用默认内存配置
    // 测试可按具体场景需要覆盖配置值
    var inMemorySettings = new Dictionary<string, string>
    {
        // 默认测试配置
        ["ConnectionStrings:Default"] = "Data Source=:memory:",
        ["BasePlatform:BaseUrl"] = "http://test-base.publicapi.findong.com",
        ["BasePlatform:ProductCode"] = "5000",
        ["Encryption:AesKey"] = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI="
    };

    var builder = new ConfigurationBuilder();
    builder.AddInMemoryCollection(inMemorySettings);
    services.ReplaceConfiguration(builder.Build());
}
```

**验收**：
- [x] 已移除基于文件的配置依赖
- [x] 测试使用内存配置
- [x] 无需构建时复制文件
- [x] 已提供不同场景的配置示例

**收益**：
- ✅ 测试执行更快（无文件 I/O）
- ✅ 测试隔离更好（每个测试可有独立配置）
- ✅ 无「文件未找到」错误
- ✅ 测试配置更易理解
- ✅ 不同测试场景更灵活

**推荐用法示例**：
```csharp
// 在测试场景中直接替换配置
[Fact]
public void Should_Test_With_Custom_Config()
{
    // 1. 创建自定义配置对象
    var customConfig = new WeighingConfiguration
    {
        MinWeightThreshold = 1.0m,
        WeightStabilityThreshold = 0.1m,
        StabilityWindowMs = 5000
    };

    // 2. 创建 IOptions<T>
    var options = Options.Create(customConfig);

    // 3. 直接在测试中使用
    var service = new YourServiceUnderTest(options);

    // 或者验证配置
    customConfig.IsValid().ShouldBeTrue();
}
```

**产出**：采用内存配置的重构后测试基类

---

### 任务 1.2：验证测试在无文件依赖下运行

**状态**：待办
**优先级**：高
**预估**：10 分钟

**描述**：
验证测试可以在没有 appsettings.json 文件依赖的情况下正常运行。

**步骤**：
1. 删除或重命名 appsettings.json 文件（可选，用于验证）
2. 运行测试套件：`dotnet test MaterialClient.Common.Tests.csproj`
3. 验证：
   - 测试成功加载配置
   - 没有「appsettings.json was not found」错误
   - 测试执行速度提升

**验收**：
- [ ] 测试能够成功运行
- [ ] 没有配置文件相关的错误
- [ ] 所有测试使用内存配置
- [ ] 测试执行时间减少（无文件 I/O）

**预期收益**：
- ✅ 测试启动更快（无需读取文件）
- ✅ 测试更可靠（无文件系统依赖）
- ✅ CI/CD 环境兼容性更好
- ✅ 每个测试可以独立配置

**产出**：已验证的、采用内存配置的测试套件

---

### 任务 1.3：将测试更新为按场景配置（可选）

**状态**：待办
**优先级**：中
**预估**：1–2 小时

**描述**：
（可选）更新现有测试，为不同场景提供特定配置。参考 `ConfigurationTestExamples.cs` 中的示例。

**步骤**：
1. 审查现有测试，识别需要不同配置的场景
2. 为这些场景创建专用的测试模块
3. 使用 `AddInMemoryCollection` 提供场景特定配置
4. 验证每个测试的独立性

**示例场景**：
- API 集成测试（使用 mock API URL）
- 数据库测试（使用不同数据库路径）
- 加密测试（使用不同测试密钥）
- 外部服务测试（使用 mock 配置）

**验收**：
- [ ] 每个测试场景有独立的配置
- [ ] 测试之间不共享配置状态
- [ ] 配置清晰且易于理解
- [ ] 测试仍然通过

**产出**：带场景特定配置的改进测试套件

**说明**：本任务为可选。MaterialClientTestBase 中的内存配置对多数情况已足够。

---

## 阶段 2：性能评估与优化（条件性）

### 任务 2.1：分析测试性能（若需要）

**状态**：待办
**优先级**：中
**预估**：1–2 小时

**描述**：
若阶段 1 发现仍有超时问题，深入分析测试执行性能，识别瓶颈。

**步骤**：
1. 运行带详细输出的测试：`dotnet test --logger "console;verbosity=detailed"`
2. 识别执行时间最长的测试
3. 分析测试基类的设置和清理逻辑：
   - `MaterialClientTestBase.cs`
   - `MaterialClientDomainTestBase.cs`
   - `MaterialClientEntityFrameworkCoreTestBase.cs`
4. 检查以下方面：
   - 数据库初始化是否高效
   - 依赖注入配置是否冗余
   - 是否有未释放的资源
   - 是否有不必要的延迟或等待
5. 检查是否有测试之间的相互依赖或隔离问题

**验收**：
- [ ] 识别出所有耗时较长的测试
- [ ] 确定性能瓶颈的具体原因
- [ ] 评估优化方案的可行性

**产出**：含优化建议的性能分析报告

---

### 任务 2.2：实施性能优化（若需要）

**状态**：待办
**优先级**：中
**预估**：2–4 小时

**描述**：
根据性能分析结果实施优化措施。此任务仅在确认存在性能问题时执行。

**可能优化**：
1. **优化数据库初始化**
   - 使用共享数据库实例（若安全）
   - 优化 Entity Framework 配置
   - 减少不必要的数据种子操作

2. **改进测试基类**
   - 优化模块配置和依赖注入设置
   - 实现更高效的对象清理逻辑
   - 使用轻量级的测试替身（test doubles）

3. **调整超时设置**
   - 为特定测试设置合理的超时时间
   - 使用 `[Fact(Timeout = X)]` 或 `[Trait("Category", "Slow")]`

4. **并行执行**
   - 评估是否可以安全地并行运行测试
   - 使用 `[Collection("Non-Parallel")]` 控制并行行为

**步骤**：
1. 根据分析结果选择合适的优化策略
2. 实施优化变更
3. 运行测试验证优化效果
4. 确保优化不影响测试的正确性和覆盖率

**验收**：
- [ ] 优化后测试执行时间显著减少
- [ ] 所有测试仍然通过
- [ ] 测试覆盖率未降低
- [ ] 无回归问题引入

**产出**：性能改进后的优化测试套件

---

### 任务 2.3：最终验证与文档

**状态**：待办
**优先级**：高
**预估**：30 分钟

**描述**：
完成所有修复和优化后，进行最终验证并记录结果。

**步骤**：
1. 运行完整测试套件至少 3 次，确保稳定性
2. 记录最终测试执行时间和通过率
3. 更新相关文档（如需要）
4. 提交变更并创建 Pull Request

**验收**：
- [ ] 测试连续 3 次完整运行成功
- [ ] 平均执行时间在可接受范围内
- [ ] 所有测试 100% 通过
- [ ] 代码变更已提交
- [ ] 提案状态更新为「已应用」

**产出**：已完成且验证通过的测试套件变更

---

## 进度跟踪

**阶段 1 进度**：1/3 项已完成
- 任务 1.1：✅ 已完成（移除文件依赖，实现内存配置）
- 任务 1.2：⏳ 待办（需 .NET SDK 环境验证）
- 任务 1.3：⏳ 待办（可选 - 按场景配置改进）

**阶段 2 进度**：0/3 项已完成（可能不需要 - 内存配置已足够快）
**总体进度**：1/6 项（17%）

**说明**：任务 1.1 实现的方案（内存配置）很可能使阶段 2 的性能优化完全不再需要。

---

## 备注

- 阶段 2 是条件性的，仅当阶段 1 发现性能问题时才需要执行
- 若阶段 1 后测试运行正常且无超时，可直接进入任务 2.3 进行最终验证
- 执行过程中如发现问题，应及时更新提案文档和任务状态
