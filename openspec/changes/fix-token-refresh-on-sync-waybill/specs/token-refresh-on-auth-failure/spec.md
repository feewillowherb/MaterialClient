## ADDED Requirements

### Requirement: Automatic token refresh on 401 error
当材料平台 API 返回 401 Unauthorized 错误时，系统 SHALL 自动触发重新登录流程以获取新的访问令牌，并使用新令牌重试原始请求。

#### Scenario: Successful token refresh and request retry
- **WHEN** 同步运单的 API 调用返回 401 Unauthorized 错误
- **AND** 用户已保存登录凭证（用户名和密码）
- **THEN** 系统自动调用认证服务重新登录
- **AND** 使用新获取的 token 重试原始 API 请求
- **AND** 请求成功完成

#### Scenario: Token refresh with saved credentials
- **WHEN** API 调用因 token 失效返回 401 错误
- **AND** 数据库中存在有效的 UserCredential 记录
- **THEN** 系统使用保存的凭证自动重新登录
- **AND** 不需要用户手动干预

#### Scenario: Token refresh fails when no saved credentials
- **WHEN** API 调用返回 401 错误
- **AND** 用户未保存登录凭证（UserCredential 为空）
- **THEN** 系统不执行自动重新登录
- **AND** 返回原始错误信息给调用方
- **AND** 记录警告日志说明无法自动刷新 token

### Requirement: Retry attempt limit
为防止无限循环，系统 SHALL 限制每个请求的 token 刷新重试次数最多为 1 次。

#### Scenario: Single retry on 401 error
- **WHEN** API 调用返回 401 错误
- **THEN** 系统执行一次 token 刷新并重试请求
- **AND** 即使重试后仍然返回 401，也不再继续重试

#### Scenario: Retry with valid new token
- **WHEN** token 刷新成功获取新的有效 token
- **AND** 使用新 token 重试请求
- **THEN** 请求正常执行
- **AND** 不消耗重试次数（因为这不是 401 错误）

### Requirement: Session token update
成功重新登录后，系统 SHALL 更新数据库中 UserSession 实体的 AccessToken 字段，确保后续 API 请求使用新的认证令牌。

#### Scenario: Session token is updated after re-login
- **WHEN** 系统因 401 错误触发重新登录
- **AND** 重新登录成功返回新的 token
- **THEN** UserSession.AccessToken 被更新为新值
- **AND** LastActivityTime 被更新为当前时间
- **AND** 后续 API 调用使用新 token

### Requirement: Detailed error logging
当 token 刷新或请求重试失败时，系统 SHALL 记录详细的错误日志，包含运单 ID、订单号、失败原因和堆栈信息。

#### Scenario: Logging on token refresh failure
- **WHEN** 重新登录操作失败（如网络错误、密码错误等）
- **THEN** 记录错误日志包含：
  - 运单 ID 和订单号
  - 重新登录失败的异常信息
  - 原始 401 错误的上下文

#### Scenario: Logging on retry failure
- **WHEN** token 刷新成功但重试请求仍然失败
- **THEN** 记录警告日志包含：
  - 运单 ID 和订单号
  - 重试失败的错误信息
  - 表明已使用新 token 进行重试

### Requirement: Apply to both sync methods
token 刷新机制 SHALL 同时应用于新增运单同步（SyncNewWaybillAsync）和修改运单同步（SyncUpdatedWaybillAsync）两个方法。

#### Scenario: Token refresh on new waybill sync
- **WHEN** SyncNewWaybillAsync 方法调用 API 返回 401 错误
- **THEN** 执行 token 刷新和重试逻辑

#### Scenario: Token refresh on updated waybill sync
- **WHEN** SyncUpdatedWaybillAsync 方法调用 API 返回 401 错误
- **THEN** 执行 token 刷新和重试逻辑
