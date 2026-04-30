## Why

材料客户端在同步运单到材料平台时，当访问令牌（token）因超时失效后返回 401 Unauthorized 错误。当前系统没有自动重新登录获取新 token 的机制，导致后续同步操作全部失败，需要用户手动重新登录才能恢复。这降低了系统的可靠性并影响了用户体验。

## What Changes

- **自动 token 刷新机制**：在 `MaterialPlatformBearerTokenHandler` 中捕获 401 响应，自动触发重新登录流程
- **请求重试**：使用新获取的 token 重新执行失败的 HTTP 请求
- **重试次数限制**：为防止无限循环，使用请求选项跟踪重试状态，最多重试 1 次
- **改进错误日志**：在重试失败时记录详细日志以便排查问题
- **会话更新**：确保 `UserSession` 实体中的 `AccessToken` 在重新登录后得到更新
- **零代码侵入**：服务层代码（如 `WeighingMatchingService`）无需任何修改，自动获得 token 刷新能力

## Capabilities

### New Capabilities
- `token-refresh-on-auth-failure`: 在 API 调用遇到 401 错误时自动重新登录并重试请求的能力

### Modified Capabilities
无。这是实现层面的改进，不改变现有功能的业务需求。

## Impact

### 受影响的代码
- `MaterialClient.Common/Api/MaterialPlatformBearerTokenHandler.cs`：
  - 添加 `IAuthenticationService` 依赖注入
  - 增强 `SendAsync` 方法：检查 401 响应并执行 token 刷新
  - 添加重试状态跟踪逻辑（使用 `HttpRequestMessage.Options`）
- `MaterialClient.Common/Services/WeighingMatchingService.cs`：**无需修改**
- `MaterialClient.Common/Entities/UserSession.cs`：已有 `UpdateAccessToken` 方法，将被用于更新 token
- `MaterialClient.Common/Services/Authentication/AuthenticationService.cs`：已有 `LoginAsync` 和 `GetSavedCredentialAsync` 方法，将被用于重新登录

### 新增依赖
无新增依赖。使用现有的 `IAuthenticationService` 和 `IRepository<UserSession, Guid>`。

### 新增依赖
无新增依赖。使用现有的 `IAuthenticationService` 和 `IRepository<UserSession, Guid>`。

### 向后兼容性
完全向后兼容。这是内部实现的改进，不影响公共 API 或用户界面。

### 性能影响
最小。仅在 token 失效时（预计很少发生）才会执行重新登录操作。
