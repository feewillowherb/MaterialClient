# token-refresh-on-auth-failure

## Purpose

当材料平台 API 调用返回 401 Unauthorized 错误时，系统自动触发会话刷新机制，允许后台轮询任务在下个周期恢复执行，无需手动干预。

## Requirements

### Requirement: 系统检测到 API 401 响应时发送会话刷新事件

`MaterialPlatformBearerTokenHandler` SHALL 在检测到 HTTP 401 响应时，通过 ABP LocalEventBus 发布 `SessionRefreshRequiredEto` 事件。

#### Scenario: API 调用返回 401 时发布刷新事件

- **WHEN** `MaterialPlatformBearerTokenHandler.SendAsync` 收到响应状态码为 401
- **THEN** 系统通过 `ILocalEventBus.Publish` 发布 `SessionRefreshRequiredEto`

#### Scenario: API 调用返回非 401 错误时不发布刷新事件

- **WHEN** `MaterialPlatformBearerTokenHandler.SendAsync` 收到响应状态码为 500 或其他非 401 错误
- **THEN** 系统不发布 `SessionRefreshRequiredEto`

#### Scenario: API 调用成功时不发布刷新事件

- **WHEN** `MaterialPlatformBearerTokenHandler.SendAsync` 收到响应状态码为 200 或其他成功状态码
- **THEN** 系统不发布 `SessionRefreshRequiredEto`

### Requirement: 会话刷新事件携带必要的上下文信息

`SessionRefreshRequiredEto` SHALL 包含发生认证失败的 API 端点、时间戳和原始状态码，供订阅方使用。

#### Scenario: 刷新事件包含 API 端点信息

- **WHEN** 创建 `SessionRefreshRequiredEto`
- **THEN** 事件包含 `ApiEndpoint` 属性，值为请求的 URI 路径（如 `/api/Order/SynchronizationOrder`）

#### Scenario: 刷新事件包含时间戳

- **WHEN** 创建 `SessionRefreshRequiredEto`
- **THEN** 事件包含 `OccurredAtUtc` 属性，值为当前 UTC 时间

#### Scenario: 刷新事件包含状态码

- **WHEN** 创建 `SessionRefreshRequiredEto`
- **THEN** 事件包含 `StatusCode` 属性，值为 HTTP 状态码 401

### Requirement: 后台轮询任务订阅会话刷新事件并尝试重新登录

后台轮询服务（如 `PollingBackgroundService`）SHOULD 订阅 `SessionRefreshRequiredEto`，收到事件后尝试使用保存的凭证重新登录。

#### Scenario: 轮询服务收到刷新事件后触发重新登录

- **WHEN** `PollingBackgroundService` 收到 `SessionRefreshRequiredEto`
- **THEN** 调用 `AuthenticationService.LoginAsync` 使用保存的凭证重新登录

#### Scenario: 重新登录成功后更新会话

- **WHEN** 重新登录成功并返回新的 `UserSession`
- **THEN** 新的会话信息被保存到数据库，后续 API 调用使用新 token

#### Scenario: 重新登录失败时记录错误日志

- **WHEN** 重新登录失败（如凭证无效、网络错误）
- **THEN** 记录错误日志，包含失败原因

### Requirement: 401 响应正常传播到调用方

`MaterialPlatformBearerTokenHandler` SHALL 不阻止 401 响应传播，允许当前 API 调用正常失败并抛出异常。

#### Scenario: 401 响应传递给调用方

- **WHEN** API 返回 401 且 `MaterialPlatformBearerTokenHandler` 发布了刷新事件
- **THEN** 原始 401 响应仍然返回给调用方，调用方收到 `Refit.ApiException` 或其他 HTTP 异常

#### Scenario: 当前同步任务允许失败

- **WHEN** `WeighingMatchingService.SyncNewWaybillAsync` 因 401 失败
- **THEN** 方法捕获异常并返回 false，记录警告日志，不阻塞后台任务队列

### Requirement: 事件 ETO 定义在约定目录

`SessionRefreshRequiredEto` SHALL 定义在 `MaterialClient.Common/Events/` 目录下，使用 `class` + primary constructor 格式。

#### Scenario: 事件类位置和格式

- **WHEN** 定义 `SessionRefreshRequiredEto`
- **THEN** 文件路径为 `MaterialClient.Common/Events/SessionRefreshRequiredEto.cs`，使用 class 和 primary constructor

### Requirement: Bearer Token 处理器使用独立工作单元

`MaterialPlatformBearerTokenHandler` SHALL 使用独立的 UnitOfWork 获取 session，避免污染调用方的事务。

#### Scenario: 独立 UOW 不影响调用方

- **WHEN** `MaterialPlatformBearerTokenHandler.SendAsync` 创建 UOW 读取 session
- **THEN** UOW 使用 `Begin(true, false)` 参数，确保独立事务且不污染调用方
