# Tasks: Test Configuration and Execution Optimization

**Change ID**: `test-configuration-and-execution-optimization`
**Total Tasks**: 4
**Estimated Duration**: 0.5 day

---

## Task Overview

本任务将分两个阶段进行：首先修复配置文件部署问题，然后评估和优化测试执行性能。第一阶段是必需的，第二阶段根据实际情况决定是否需要。

---

## Phase 1: Configuration File Fix

### Task 1.1: Remove appsettings.json Dependencies

**Status**: Completed
**Priority**: High
**Estimated**: 15 minutes
**Actual**: 15 minutes

**Description**:
移除测试项目对 appsettings.json 文件的依赖，改为使用内存配置（In-Memory Configuration）。这使得每个测试场景可以独立配置，更加快速和隔离。

**Changes Made**:

1. **Modified**: `MaterialClientTestBase.cs`
   - Removed: `AddJsonFile("appsettings.json")` dependency
   - Added: In-memory configuration with default test values
   - Benefit: Tests are faster and more isolated

2. **Modified**: `MaterialClient.Common.Tests.csproj`
   - Removed: `<CopyToOutputDirectory>` configuration
   - Removed: File deployment requirements
   - Benefit: No build-time file dependencies

3. **Created**: `ConfigurationTestExamples.cs`
   - Examples showing different configuration strategies
   - Demonstrates per-test configuration overrides
   - **Recommended approach**: Replace `IOptions<T>` in scenario initialization
   - Examples for `WeighingConfiguration`, `SystemSettings`, etc.

4. **Created**: `TEST_CONFIGURATION_GUIDE.md`
   - Comprehensive guide for test configuration best practices
   - Shows how to replace `IOptions<XXX>` in test scenarios
   - Includes migration guide and complete examples

**New Implementation**:
```csharp
protected override void BeforeAddApplication(IServiceCollection services)
{
    // Use default in-memory configuration
    // Tests can override configuration values as needed for specific scenarios
    var inMemorySettings = new Dictionary<string, string>
    {
        // Default test configuration
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

**Validation**:
- [x] Removed file-based configuration dependencies
- [x] Tests use in-memory configuration
- [x] No build-time file copying required
- [x] Configuration examples provided for different scenarios

**Benefits**:
- ✅ Faster test execution (no file I/O)
- ✅ Better test isolation (each test can have unique config)
- ✅ No "file not found" errors
- ✅ Easier to understand test configuration
- ✅ More flexible for different test scenarios

**Recommended Usage Example**:
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

**Output**: Refactored test base class with in-memory configuration

---

### Task 1.2: Verify Tests Run Without File Dependencies

**Status**: Pending
**Priority**: High
**Estimated**: 10 minutes

**Description**:
验证测试可以在没有 appsettings.json 文件依赖的情况下正常运行。

**Steps**:
1. 删除或重命名 appsettings.json 文件（可选，用于验证）
2. 运行测试套件：`dotnet test MaterialClient.Common.Tests.csproj`
3. 验证：
   - 测试成功加载配置
   - 没有 "appsettings.json was not found" 错误
   - 测试执行速度提升

**Validation**:
- [ ] 测试能够成功运行
- [ ] 没有配置文件相关的错误
- [ ] 所有测试使用内存配置
- [ ] 测试执行时间减少（无文件 I/O）

**Expected Benefits**:
- ✅ 测试启动更快（无需读取文件）
- ✅ 测试更可靠（无文件系统依赖）
- ✅ CI/CD 环境兼容性更好
- ✅ 每个测试可以独立配置

**Output**: Verified test suite with in-memory configuration

---

### Task 1.3: Update Tests to Use Per-Scenario Configuration (Optional)

**Status**: Pending
**Priority**: Medium
**Estimated**: 1-2 hours

**Description**:
（可选）更新现有测试，为不同场景提供特定配置。参考 `ConfigurationTestExamples.cs` 中的示例。

**Steps**:
1. 审查现有测试，识别需要不同配置的场景
2. 为这些场景创建专用的测试模块
3. 使用 `AddInMemoryCollection` 提供场景特定的配置
4. 验证每个测试的独立性

**Example Scenarios**:
- API 集成测试（使用 mock API URL）
- 数据库测试（使用不同的数据库路径）
- 加密测试（使用不同的测试密钥）
- 外部服务测试（使用 mock 配置）

**Validation**:
- [ ] 每个测试场景有独立的配置
- [ ] 测试之间不共享配置状态
- [ ] 配置清晰且易于理解
- [ ] 测试仍然通过

**Output**: Improved test suite with scenario-specific configurations

**Note**: This task is optional. The in-memory configuration in MaterialClientTestBase is sufficient for most cases.

---

## Phase 2: Performance Evaluation and Optimization (Conditional)

### Task 2.1: Analyze Test Performance (If Needed)

**Status**: Pending
**Priority**: Medium
**Estimated**: 1-2 hours

**Description**:
如果 Phase 1 发现仍有超时问题，深入分析测试执行性能，识别瓶颈。

**Steps**:
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

**Validation**:
- [ ] 识别出所有耗时较长的测试
- [ ] 确定性能瓶颈的具体原因
- [ ] 评估优化方案的可行性

**Output**: Performance analysis report with optimization recommendations

---

### Task 2.2: Implement Performance Optimizations (If Needed)

**Status**: Pending
**Priority**: Medium
**Estimated**: 2-4 hours

**Description**:
根据性能分析结果实施优化措施。此任务仅在确认存在性能问题时执行。

**Possible Optimizations**:
1. **优化数据库初始化**
   - 使用共享数据库实例（如果安全）
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

**Steps**:
1. 根据分析结果选择合适的优化策略
2. 实施优化变更
3. 运行测试验证优化效果
4. 确保优化不影响测试的正确性和覆盖率

**Validation**:
- [ ] 优化后测试执行时间显著减少
- [ ] 所有测试仍然通过
- [ ] 测试覆盖率未降低
- [ ] 无回归问题引入

**Output**: Optimized test suite with improved performance

---

### Task 2.3: Final Validation and Documentation

**Status**: Pending
**Priority**: High
**Estimated**: 30 minutes

**Description**:
完成所有修复和优化后，进行最终验证并记录结果。

**Steps**:
1. 运行完整测试套件至少 3 次，确保稳定性
2. 记录最终测试执行时间和通过率
3. 更新相关文档（如需要）
4. 提交变更并创建 Pull Request

**Validation**:
- [ ] 测试连续 3 次完整运行成功
- [ ] 平均执行时间在可接受范围内
- [ ] 所有测试 100% 通过
- [ ] 代码变更已提交
- [ ] 提案状态更新为 "Applied"

**Output**: Completed change with verified test suite

---

## Progress Tracking

**Phase 1 Progress**: 1/3 tasks completed
- Task 1.1: ✅ Completed (Removed file dependencies, implemented in-memory config)
- Task 1.2: ⏳ Pending (Requires .NET SDK environment to verify)
- Task 1.3: ⏳ Pending (Optional - per-scenario configuration improvements)

**Phase 2 Progress**: 0/3 tasks completed (Likely not needed - in-memory config is fast)
**Overall Progress**: 1/6 tasks (17%)

**Note**: Task 1.1 implemented a superior solution (in-memory configuration) that likely eliminates the need for Phase 2 performance optimization entirely.

---

## Notes

- Phase 2 是条件性的，仅当 Phase 1 发现性能问题时才需要执行
- 如果 Phase 1 后测试运行正常且无超时，可以直接进入 Task 2.3 进行最终验证
- 在执行过程中如发现问题，应及时更新提案文档和任务状态
