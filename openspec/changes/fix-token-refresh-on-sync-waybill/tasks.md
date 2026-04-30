## 1. MaterialPlatformBearerTokenHandler 依赖注入

- [ ] 1.1 在 `MaterialPlatformBearerTokenHandler` 中添加 `IAuthenticationService` 依赖注入
- [ ] 1.2 更新构造函数参数，添加 `IAuthenticationService authenticationService`

## 2. 请求重试状态跟踪

- [ ] 2.1 创建 `HttpRequestMessageExtensions` 静态类
- [ ] 2.2 实现 `MarkTokenRefreshRetried(this HttpRequestMessage request)` 扩展方法
- [ ] 2.3 实现 `IsTokenRefreshRetried(this HttpRequestMessage request)` 扩展方法
- [ ] 2.4 使用 `HttpRequestMessage.Options` 存储重试状态（类型：`bool`）

## 3. SendAsync 方法增强

- [ ] 3.1 修改 `SendAsync` 方法，将返回值存储在变量中以便检查
- [ ] 3.2 在响应返回后检查 `response.StatusCode` 是否为 `HttpStatusCode.Unauthorized`
- [ ] 3.3 检查请求是否已重试（调用 `IsTokenRefreshRetried(request)`）
- [ ] 3.4 如果是 401 且未重试，调用 `HandleTokenRefreshAndRetryAsync` 方法

## 4. HandleTokenRefreshAndRetryAsync 方法实现

- [ ] 4.1 创建 `HandleTokenRefreshAndRetryAsync` 私有方法
- [ ] 4.2 标记原始请求为已重试（调用 `MarkTokenRefreshRetried(originalRequest)`）
- [ ] 4.3 调用 `_authenticationService.GetSavedCredentialAsync()` 获取保存的凭证
- [ ] 4.4 检查凭证是否存在：
  - 如果不存在，记录警告日志并返回 401 响应
- [ ] 4.5 调用 `_authenticationService.LoginAsync(username, password, rememberMe: false)` 重新登录
- [ ] 4.6 如果登录成功，记录信息日志并重试原始请求
- [ ] 4.7 如果登录失败，记录错误日志并返回 401 响应
- [ ] 4.8 实现请求克隆逻辑（`CloneHttpRequestMessageAsync`）以重新发送请求
- [ ] 4.9 递归调用 `SendAsync(retryRequest, cancellationToken)` 重试请求

## 5. 请求克隆实现

- [ ] 5.1 实现 `CloneHttpRequestMessageAsync` 私有方法
- [ ] 5.2 复制原始请求的 HTTP 方法、URL、请求头和内容
- [ ] 5.3 确保新请求能够重新通过 Handler 添加新的 Bearer Token

## 6. 错误处理和日志记录

- [ ] 6.1 在 token 刷新成功时记录信息日志（包含请求 URL）
- [ ] 6.2 在 token 刷新失败时记录错误日志（包含异常信息）
- [ ] 6.3 在无保存凭证时记录警告日志
- [ ] 6.4 在重试仍然失败时记录警告日志

## 7. 单元测试

- [ ] 7.1 编写测试：401 错误触发 token 刷新并重试成功
- [ ] 7.2 编写测试：无保存凭证时不执行 token 刷新
- [ ] 7.3 编写测试：登录失败时返回 401 响应
- [ ] 7.4 编写测试：重试后仍然 401 时不再重试（检查重试标记）
- [ ] 7.5 编写测试：非 401 错误不触发 token 刷新
- [ ] 7.6 编写测试：重试状态标记正确设置和检查

## 8. 集成验证

- [ ] 8.1 在开发环境测试完整的 token 刷新流程（使用 WeighingMatchingService）
- [ ] 8.2 验证 UserSession.AccessToken 在重新登录后正确更新
- [ ] 8.3 验证后续 API 调用使用新 token
- [ ] 8.4 验证重试次数限制（最多 1 次）
- [ ] 8.5 验证 `WeighingMatchingService` 无需修改即可工作
- [ ] 8.6 验证错误日志包含完整的上下文信息（请求 URL）

## 9. 代码审查准备

- [ ] 9.1 确保代码符合 AGENTS.md 中的编码规范
- [ ] 9.2 确保使用现代 C# 语法（主构造函数、nullable 等）
- [ ] 9.3 确保所有方法都有适当的 XML 文档注释
- [ ] 9.4 确保异常处理遵循项目约定
- [ ] 9.5 确保不会影响现有的 Polly 重试策略
