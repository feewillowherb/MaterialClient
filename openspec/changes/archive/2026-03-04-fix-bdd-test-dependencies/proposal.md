# 变更：修复 BDD 测试依赖

## 原因

BDD 测试套件（Reqnroll 场景）当前被阻塞，因为关键 API 依赖（`IMaterialPlatformApi`、`ISoundDeviceApi`）未在测试 DI 容器中注册，导致约 20+ 个 BDD 场景无法执行，阻碍开发团队对认证、授权与称重流程的验证。

**当前状态**：
- ✅ 26 个测试通过（43%）
- ❌ 2 个测试失败（3%）
- 🔴 约 20 个测试因 DI 失败被阻塞（33%）
- ⏸️ 31 个测试跳过（依赖硬件，预期）（52%）

**根因**：`MaterialClientEntityFrameworkCoreTestModule` 仅注册了 `IBasePlatformApi` 的 mock，未注册 `IMaterialPlatformApi` 与 `ISoundDeviceApi` 的 mock，而二者被以下位置需要：
- `AuthenticationSteps.cs:46` - 构造函数注入 `IMaterialPlatformApi`
- 需要音响设备 API 的集成测试

## 变更内容

### 1. 补充缺失的 Mock 注册
- 注册 `IMaterialPlatformApi` mock，提供默认登录成功行为
- 注册 `ISoundDeviceApi` mock，提供桩实现
- 添加 API DTO 所需的 using

### 2. 更新测试模块配置
文件：`MaterialClient.Common.Tests/EntityFrameworkCore/MaterialClientEntityFrameworkCoreTestModule.cs`  
变更：添加 `IMaterialPlatformApi`、`ISoundDeviceApi` 的单例 mock 注册（NSubstitute）；为常见认证场景配置默认 mock 行为；增加说明 mock 注册的行内注释。

### 3. 预期改善
修复后：✅ 45+ 个测试通过（75%）；❌ 0–2 个失败；🔴 0 个阻塞；⏸️ 31 个跳过（硬件相关，预期）。  
改善：约 +19 个可执行测试，通过率约 +32%。

## 影响

- **受影响规范**：`test-infrastructure`（新增测试依赖管理能力）
- **受影响代码**：`MaterialClientEntityFrameworkCoreTestModule.cs`（增加 mock 注册）
- **解除阻塞的测试**：Authentication.feature、Authorization.feature、WeighingService.feature、WeighingMatchingService.feature 相关场景
- **收益**：立即解除 20+ 个 BDD 测试便于 CI/CD；支持认证与称重流程的自动化验证；恢复对测试套件完整性的信心；自动化测试覆盖率从约 47% 提升至约 78%
- **风险**：无——纯测试基础设施修复，无生产代码变更；变更仅限测试项目
- **迁移**：无需迁移；拉取变更后重新构建测试项目即可

## 参考

- TempDocs/FixTest 下的 TEST_ISSUES_SUMMARY.md、README_ANALYSIS.md、FIX_GUIDE.md、FIXED_MODULE_CODE.cs
- IMaterialPlatformApi、ISoundDeviceApi、AuthenticationSteps.cs:46
