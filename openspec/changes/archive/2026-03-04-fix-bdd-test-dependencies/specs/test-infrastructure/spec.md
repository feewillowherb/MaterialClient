## ADDED 需求

### 需求：测试模块 API Mock 注册

测试基础设施应为所有外部 API 依赖提供 mock 实现，以便在无外部服务依赖的情况下执行 BDD 场景。

#### 场景：为认证测试注册 MaterialPlatformApi mock
- **给定** 正在配置测试模块
- **当** 调用 `ConfigureServices` 方法
- **则** 应将 `IMaterialPlatformApi` 注册为单例 mock
- **且** mock 应提供默认登录成功响应
- **且** 登录响应应包含有效的 `LoginUserDto`（含 UserId、UserName、Token、AuthEndTime）

#### 场景：为集成测试注册 SoundDeviceApi mock
- **给定** 正在配置测试模块
- **当** 调用 `ConfigureServices` 方法
- **则** 应将 `ISoundDeviceApi` 注册为单例 mock
- **且** mock 应为 `PlayAudioAsync` 提供桩实现
- **且** 桩应返回成功 JSON 响应且无需外部 HTTP 服务

#### 场景：AuthenticationSteps 可解析 IMaterialPlatformApi
- **给定** `MaterialClientEntityFrameworkCoreTestModule` 已初始化
- **当** `AuthenticationSteps` 构造函数尝试解析 `IMaterialPlatformApi`
- **则** DI 容器应成功解析 mock 实例
- **且** 不应抛出 `InvalidOperationException`

#### 场景：BDD 场景在无外部服务依赖下执行
- **给定** 测试模块中已注册所有 API mock
- **当** 执行 `Authentication.feature`、`Authorization.feature`、`WeighingService.feature`、`WeighingMatchingService.feature` 中的 BDD 场景
- **则** 所有场景应能初始化且无 DI 解析错误
- **且** 场景应能按用例配置 mock 行为
- **且** 测试执行无需外部 HTTP 服务

### 需求：测试隔离与 Mock 重置

测试基础设施应在每个场景执行前重置 mock 状态，确保场景间隔离。

#### 场景：每个 BDD 场景前重置 mock 状态
- **给定** BDD 测试步骤类具有 `[BeforeScenario]` 方法
- **当** 场景开始执行
- **则** 所有已注册 mock 的已接收调用应被清空
- **且** mock 行为应重置为默认配置
- **且** 后续场景不应受前一场景 mock 交互影响

#### 场景：可配置测试专用 mock 行为
- **给定** 测试模块中已注册默认 mock 行为
- **当** 某测试步骤配置特定 mock 行为（如登录失败响应）
- **则** 该测试的专用行为应覆盖默认行为
- **且** mock 应返回所配置的响应
- **且** 其他测试不应受该专用配置影响
