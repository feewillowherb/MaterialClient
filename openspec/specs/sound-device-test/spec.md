# 音响设备测试 规范

## 目的
待定 - 由变更 sound-device-test-feature 归档后创建。归档后更新目的。

## 需求

### 需求：设置中的音响设备测试按钮

系统应在设置窗口的音响设备配置区域提供测试按钮，用于触发音频测试播放。

#### 场景：测试按钮的可见性与可用性
- **假设** 用户打开设置窗口
- **且** 进入音响设备配置区域
- **则** 系统应显示“测试音响”按钮
- **且** 按钮应位于音量配置字段之后
- **且** 仅在音响设备已启用（`SoundDeviceEnabled = true`）时按钮可用
- **且** 测试进行中时按钮应禁用

#### 场景：音响设备未启用时测试按钮禁用
- **假设** 用户打开设置窗口
- **且** 音响设备未启用（未勾选复选框）
- **则** 测试按钮应禁用
- **且** 当用户启用音响设备（勾选复选框）后，测试按钮应变为可用

---

### 需求：音响设备测试执行

系统应在用户点击测试按钮时，通过已配置的音响设备播放固定测试语。

#### 场景：测试播放成功
- **假设** 音响设备已启用且配置正确（IP、SN、LocalIP 有效）
- **且** 设备在线且可访问
- **当** 用户点击“测试音响”按钮
- **则** 系统应调用 `ISoundDeviceService.PlayTextV2TestAsync()`
- **且** 服务应播放固定测试文本“音柱测试”
- **且** 系统应记录信息日志：“Starting sound device test with text: 音柱测试”
- **且** 系统应在 UI 中显示“测试成功”
- **且** 测试执行期间测试按钮应保持禁用
- **且** 测试完成后测试按钮应恢复可用

#### 场景：配置无效时的测试
- **假设** 音响设备已启用但配置无效（IP、SN 或 LocalIP 缺失或错误）
- **当** 用户点击“测试音响”按钮
- **则** 系统应尝试调用 `PlayTextV2TestAsync()`
- **且** 服务应检测到无效配置
- **且** 服务应记录包含配置详情的警告日志
- **且** 服务应不播放音频即返回
- **且** 系统应显示错误信息：“测试失败: [错误详情]”
- **且** 错误后测试按钮应恢复可用

#### 场景：网络错误时的测试
- **假设** 音响设备已启用且配置有效
- **且** 设备离线或网络不可达
- **当** 用户点击“测试音响”按钮
- **则** 系统应调用 `PlayTextV2TestAsync()`
- **且** 服务应尝试向设备发起 HTTP 请求
- **且** HTTP 请求应因 `HttpRequestException` 或超时而失败
- **且** 服务应记录包含异常详情的错误日志
- **且** 服务应将异常重新抛给调用方
- **且** ViewModel 应捕获异常
- **且** 系统应显示用户可读错误信息：“测试失败: 网络错误，请检查音响设备IP地址”或“测试失败: 请求超时，请检查音响设备是否在线”
- **且** 错误后测试按钮应恢复可用

#### 场景：测试取消与超时
- **假设** 音响设备测试进行中
- **且** 设备无响应
- **当** 超过 30 秒仍无成功响应
- **则** HTTP 请求应超时
- **且** 服务应记录超时错误
- **且** 服务应重新抛出异常
- **且** ViewModel 应显示超时错误信息
- **且** 测试按钮应恢复可用

#### 场景：始终使用固定测试文本
- **假设** 触发音响设备测试
- **当** 调用 `PlayTextV2TestAsync()` 时
- **则** 服务应使用常量测试文本“音柱测试”
- **且** 测试文本不可配置或由用户定义
- **且** 测试文本应与功能名称一致以便理解

---

### 需求：测试状态反馈

系统应在测试执行期间及之后提供视觉反馈，告知用户测试状态。

#### 场景：显示测试进行中状态
- **假设** 用户点击“测试音响”按钮
- **当** 测试开始执行
- **则** 系统应设置 `IsSoundDeviceTestRunning = true`
- **且** 测试按钮应禁用（通过绑定 `IsSoundDeviceTestRunning`）
- **且** 应清空状态信息（`SoundDeviceTestResult = null`）

#### 场景：显示测试成功结果
- **假设** 测试成功完成
- **当** 音频播放完成且无错误
- **则** 系统应设置 `SoundDeviceTestResult = "测试成功"`
- **且** 状态文本块应显示成功信息
- **且** 状态文本应可见（通过可见性转换器）
- **且** 应将 `IsSoundDeviceTestRunning` 设为 `false`
- **且** 测试按钮应恢复可用

#### 场景：显示测试错误结果
- **假设** 测试因异常失败
- **当** ViewModel 中捕获异常
- **则** 系统应将 `SoundDeviceTestResult` 设为包含异常详情的错误信息
- **且** 状态文本块应显示该错误信息
- **且** 错误信息应对用户友好（如“测试失败: 网络错误，请检查音响设备IP地址”）
- **且** 应通过 Serilog 记录错误信息
- **且** 应将 `IsSoundDeviceTestRunning` 设为 `false`
- **且** 测试按钮应恢复可用

#### 场景：状态信息保持
- **假设** 测试已完成（成功或失败）
- **且** 已显示状态信息
- **当** 用户在设置窗口中执行其他操作
- **则** 状态信息应保持可见直至下次触发测试
- **且** 属性变更后状态信息仍应可见
- **且** 仅在新测试开始时清空状态信息

---

### 需求：服务层测试方法

系统应在音响设备服务中提供专用测试方法，封装测试相关逻辑。

#### 场景：接口中存在专用测试方法
- **假设** 已定义 `ISoundDeviceService` 接口
- **则** 接口应包含方法签名：`Task PlayTextV2TestAsync(CancellationToken cancellationToken = default)`
- **且** 方法应有说明用途的 XML 文档注释
- **且** 方法应接受取消令牌以支持异步取消

#### 场景：测试方法实现使用固定文本
- **假设** `SoundDeviceService` 实现 `ISoundDeviceService`
- **当** 调用 `PlayTextV2TestAsync()` 时
- **则** 实现应定义常量测试文本“音柱测试”
- **且** 实现应记录带测试文本的测试开始日志
- **且** 实现应调用已有的 `PlayTextV2Async("音柱测试", cancellationToken)` 方法
- **且** 实现应记录成功完成日志
- **且** 实现应捕获、记录并重新抛出异常

#### 场景：正确处理取消令牌
- **假设** 使用取消令牌调用 `PlayTextV2TestAsync()`
- **当** 测试执行期间请求取消
- **则** 实现应将取消令牌传入底层 `PlayTextV2Async()` 调用
- **且** 操作应优雅取消
- **且** 若发生取消，系统应记录日志
- **且** 不应发生资源泄漏（HttpClient 正确释放）

---

### 需求：MVVM 集成

系统应遵循 MVVM 架构，在 View、ViewModel 与服务层之间保持清晰职责分离。

#### 场景：ViewModel 中测试功能命令
- **假设** 实例化 `SettingsWindowViewModel`
- **则** ViewModel 应有 `TestSoundDevice` 命令属性
- **且** 命令应以 `ReactiveCommand` 实现
- **且** 命令应执行异步委托 `TestSoundDeviceAsync()`
- **且** 命令应使用 `[ReactiveCommand]` 特性以支持源生成

#### 场景：ViewModel 具有测试状态属性
- **假设** 实例化 `SettingsWindowViewModel`
- **则** ViewModel 应有布尔属性 `IsSoundDeviceTestRunning`
- **且** 属性应使用 `[Reactive]` 特性以支持变更通知
- **且** ViewModel 应有字符串? 属性 `SoundDeviceTestResult`
- **且** 属性应使用 `[Reactive]` 特性以支持变更通知

#### 场景：服务依赖注入
- **假设** 通过依赖注入实例化 `SettingsWindowViewModel`
- **则** 构造函数应接受 `ISoundDeviceService` 参数
- **且** 参数应赋给只读字段 `_soundDeviceService`
- **且** 在 `TestSoundDeviceAsync()` 中应使用该字段调用测试方法

#### 场景：命令 canExecute 逻辑
- **假设** 已初始化 `SettingsWindowViewModel`
- **则** `TestSoundDevice` 命令应使用 canExecute 可观察对象创建
- **且** 仅当 `SoundDeviceEnabled` 为 true 时命令可执行
- **且** 命令应使用可观察对象 `this.WhenAnyValue(x => x.SoundDeviceEnabled)`
- **且** `SoundDeviceEnabled` 变化时，命令可执行性应更新

#### 场景：视图绑定 ViewModel
- **假设** 已定义设置窗口 XAML
- **则** 测试按钮的 Command 应绑定到 `{Binding TestSoundDevice}`
- **且** 测试按钮的 IsEnabled 应绑定到 `{Binding IsSoundDeviceTestRunning, Converter=...}`（取反）
- **且** 状态 TextBlock 的 Text 应绑定到 `{Binding SoundDeviceTestResult}`
- **且** 状态 TextBlock 的 Visibility 应通过转换器绑定，在结果非 null 时显示

---

### 需求：错误处理与日志

系统应妥善处理所有异常，并为调试与监控提供完整日志。

#### 场景：服务层异常处理
- **假设** 正在执行 `PlayTextV2TestAsync()` 方法
- **当** 发生异常（网络错误、超时、设备错误）
- **则** 方法应捕获异常
- **且** 方法应通过 Serilog 记录包含完整异常详情的错误
- **且** 方法应将异常重新抛给调用方
- **且** 日志消息应包含上下文：“Sound device test failed”

#### 场景：ViewModel 层异常处理
- **假设** 正在执行 `TestSoundDeviceAsync()` 命令
- **当** `PlayTextV2TestAsync()` 抛出异常
- **则** ViewModel 应在 catch 块中捕获异常
- **且** ViewModel 应记录包含异常详情的错误
- **且** ViewModel 应将 `SoundDeviceTestResult` 设为用户可读的错误信息
- **且** ViewModel 应在 finally 块中确保将 `IsSoundDeviceTestRunning` 设为 false
- **且** 用户应在 UI 中看到错误信息

#### 场景：网络错误的专门处理
- **假设** 测试执行遇到网络连接问题
- **当** 抛出 `HttpRequestException`
- **则** ViewModel 应捕获该异常类型
- **且** ViewModel 应记录包含异常详情的错误
- **且** ViewModel 应显示专门信息：“测试失败: 网络错误，请检查音响设备IP地址”

#### 场景：超时的专门处理
- **假设** 测试执行超时（超过 30 秒）
- **当** 抛出 `TaskCanceledException`
- **则** ViewModel 应捕获该异常类型
- **且** ViewModel 应记录超时错误
- **且** ViewModel 应显示专门信息：“测试失败: 请求超时，请检查音响设备是否在线”

#### 场景：各阶段均有日志
- **假设** 触发测试操作
- **当** 测试开始时，系统应记录：“Starting sound device test with text: 音柱测试”
- **当** 测试成功时，系统应记录：“Sound device test succeeded”
- **当** 测试失败时，系统应记录包含异常详情与上下文的错误，且所有日志应使用合适级别（Information、Warning、Error）

---

### 需求：内存管理与资源释放

系统应在测试操作中正确管理资源，避免内存泄漏。

#### 场景：服务层 HttpClient 释放
- **假设** 测试方法调用 `PlayTextV2Async()`
- **当** 为 HTTP 请求创建 HttpClient
- **则** HttpClient 应放在 try-finally 中
- **且** 应在 finally 中释放 HttpClient
- **且** 不应发生套接字泄漏

#### 场景：Rx 订阅管理
- **假设** 使用 canExecute 可观察对象创建 `TestSoundDevice` 命令
- **当** ViewModel 被释放或窗口关闭
- **则** 命令订阅应被正确释放
- **且** Rx 订阅不应导致内存泄漏
- **且** 多次执行测试后内存使用应保持稳定

#### 场景：ReactiveUI 命令生命周期
- **假设** 通过 `ReactiveCommand.CreateFromTask()` 创建 `TestSoundDevice` 命令
- **当** 多次执行命令
- **则** 每次执行应干净结束
- **且** 命令不应累积状态
- **且** 内存使用应保持稳定

---

### 需求：测试操作超时

系统应对测试操作施加合理超时，避免无限阻塞。

#### 场景：HTTP 请求超时
- **假设** 测试执行进行中
- **且** 音响设备无响应
- **当** HTTP 请求在 30 秒内未完成
- **则** HttpClient 应因超时抛出 `TaskCanceledException`
- **且** 应捕获并记录异常
- **且** 用户应看到超时错误信息
- **且** 测试按钮应恢复可用

#### 场景：整体操作超时
- **假设** 测试包含多次重试（按现有 `PlayTextV2Async` 逻辑）
- **且** 每次尝试超时时间为 30 秒
- **当** 全部 8 次重试均超时
- **则** 总测试时长不应超过约 240 秒（8 × 30s）
- **且** 操作应以错误结束
- **且** 执行期间 UI 应保持响应

---

### 需求：UI 线程不阻塞

系统应在异步测试操作期间保持 UI 响应。

#### 场景：调用链全程使用 async/await
- **假设** 用户点击测试按钮
- **当** `TestSoundDeviceAsync()` 命令执行
- **则** 方法应标记为 `async Task`
- **且** 调用 `PlayTextV2TestAsync()` 时应使用 `await`
- **且** HTTP 请求期间不应阻塞 UI 线程
- **且** 窗口应保持可拖动和响应

#### 场景：在 UI 线程更新状态
- **假设** 后台线程上测试执行进行中
- **当** 设置 `IsSoundDeviceTestRunning` 或 `SoundDeviceTestResult` 属性
- **则** ReactiveUI 应自动将属性变更封送到 UI 线程
- **且** UI 应立即更新，无需 `Dispatcher.Invoke`
- **且** 按钮可用性与状态文本应同步更新

---
