# sound-device-test 规范

## 目的

提供音响设备测试能力，使用户无需完整称重流程即可快速验证音响设备配置与功能。

## ADDED 需求

### 需求：设置中的音响设备测试按钮

系统应在设置窗口的音响设备配置区块提供测试按钮，用于触发音频测试播放。

#### 场景：测试按钮的可见性与启用条件
- **给定** 用户打开设置窗口
- **且** 进入音响设备配置区块
- **则** 系统应显示「测试音响」按钮
- **且** 按钮应位于音量配置字段之后
- **且** 仅当音响设备已启用（SoundDeviceEnabled = true）时按钮应启用
- **且** 测试运行中时按钮应禁用

#### 场景：音响设备未启用时测试按钮禁用
- **给定** 用户打开设置窗口
- **且** 音响设备未启用（复选框未勾选）
- **则** 测试按钮应禁用
- **且** 当用户启用音响设备（勾选复选框）时
- **则** 测试按钮应变为启用

---

### 需求：音响设备测试执行

用户点击测试按钮时，系统应通过已配置的音响设备播放固定测试短语。

#### 场景：测试播放成功
- **给定** 音响设备已启用且配置正确（有效 IP、SN、LocalIP）
- **且** 设备在线且可访问
- **当** 用户点击「测试音响」按钮
- **则** 系统应调用 `ISoundDeviceService.PlayTextV2TestAsync()`
- **且** 服务应播放固定测试文本「音柱测试」
- **且** 系统应记录信息日志：「Starting sound device test with text: 音柱测试」
- **且** 系统应在 UI 显示「测试成功」
- **且** 测试执行期间测试按钮应保持禁用
- **且** 测试完成后测试按钮应变为启用

#### 场景：无效配置下的测试
- **给定** 音响设备已启用但配置无效（缺少或错误 IP、SN、LocalIP）
- **当** 用户点击「测试音响」按钮
- **则** 系统应尝试调用 `PlayTextV2TestAsync()`
- **且** 服务应检测到无效配置
- **且** 服务应记录带配置详情的警告日志
- **且** 服务应不播放音频即返回
- **且** 系统应显示错误信息：「测试失败: [错误详情]」
- **且** 错误后测试按钮应变为启用

#### 场景：网络错误时的测试
- **给定** 音响设备已启用且配置有效
- **且** 设备离线或网络不可达
- **当** 用户点击「测试音响」按钮
- **则** 系统应调用 `PlayTextV2TestAsync()`
- **且** 服务应尝试向设备发起 HTTP 请求
- **且** HTTP 请求应以 `HttpRequestException` 或超时失败
- **且** 服务应记录带异常详情的错误日志并向调用方重新抛出
- **且** ViewModel 应捕获异常
- **且** 系统应显示用户可读错误信息：「测试失败: 网络错误，请检查音响设备IP地址」或「测试失败: 请求超时，请检查音响设备是否在线」
- **且** 错误后测试按钮应变为启用

#### 场景：测试取消与超时
- **给定** 音响设备测试进行中
- **且** 设备无响应
- **当** 30 秒内未收到成功响应
- **则** HTTP 请求应超时
- **且** 服务应记录超时错误并重新抛出
- **且** ViewModel 应显示超时错误信息
- **且** 测试按钮应变为启用

#### 场景：始终使用固定测试文本
- **给定** 触发了音响设备测试
- **当** 调用 `PlayTextV2TestAsync()` 时
- **则** 服务应使用常量测试文本「音柱测试」
- **且** 测试文本不应可配置或由用户定义
- **且** 测试文本应与功能名称一致以便理解

---

### 需求：测试状态反馈

系统应在测试执行期间及之后提供视觉反馈，告知用户测试状态。

#### 场景：显示测试进行中状态
- **给定** 用户点击「测试音响」按钮
- **当** 测试开始执行
- **则** 系统应设置 `IsSoundDeviceTestRunning = true`
- **且** 测试按钮应变为禁用（通过绑定 IsSoundDeviceTestRunning）
- **且** 状态消息应被清空（SoundDeviceTestResult = null）

#### 场景：显示测试成功结果
- **给定** 测试执行成功完成
- **当** 音频播放完成且无错误
- **则** 系统应设置 `SoundDeviceTestResult = "测试成功"`
- **且** 状态文本块应显示成功信息
- **且** 状态文本应可见（通过可见性转换器）
- **且** 应设置 `IsSoundDeviceTestRunning = false`
- **且** 测试按钮应变为启用

#### 场景：显示测试错误结果
- **给定** 测试执行因异常失败
- **当** ViewModel 捕获异常
- **则** 系统应将 `SoundDeviceTestResult` 设为包含异常详情的错误信息
- **且** 状态文本块应显示该错误信息
- **且** 错误信息应用户友好（如「测试失败: 网络错误，请检查音响设备IP地址」）
- **且** 应通过 Serilog 记录错误日志
- **且** 应设置 `IsSoundDeviceTestRunning = false`
- **且** 测试按钮应变为启用

#### 场景：状态消息持久显示
- **给定** 测试已完成（成功或失败）且已显示状态消息
- **当** 用户在设置窗口进行其他操作
- **则** 状态消息应保持可见直至下次触发测试
- **且** 状态消息应在属性变更后仍可见
- **且** 仅当新测试开始时才清空状态消息

---

### 需求：服务层测试方法

系统应在音响设备服务中提供专用测试方法，封装测试专用逻辑。

#### 场景：接口中存在专用测试方法
- **给定** 已定义 `ISoundDeviceService` 接口
- **则** 接口应包含方法签名：`Task PlayTextV2TestAsync(CancellationToken cancellationToken = default)`
- **且** 方法应有说明用途的 XML 文档注释
- **且** 方法应接受取消令牌以支持异步取消

#### 场景：测试方法实现使用固定文本
- **给定** `SoundDeviceService` 实现 `ISoundDeviceService`
- **当** 调用 `PlayTextV2TestAsync()` 时
- **则** 实现应定义常量测试文本「音柱测试」
- **且** 实现应记录带测试文本的测试开始日志
- **且** 实现应调用现有方法 `PlayTextV2Async("音柱测试", cancellationToken)`
- **且** 实现应记录成功完成日志
- **且** 实现应捕获、记录并重新抛出异常

#### 场景：取消令牌被正确处理
- **给定** 使用取消令牌调用 `PlayTextV2TestAsync()`
- **当** 测试执行期间请求取消
- **则** 实现应将取消令牌传入底层 `PlayTextV2Async()` 调用
- **且** 操作应被优雅取消
- **且** 若发生取消，系统应记录日志
- **且** 不应发生资源泄漏（HttpClient 正确释放）

---

### 需求：MVVM 集成

系统应遵循 MVVM 架构，在 View、ViewModel 与服务层之间保持职责分离。

#### 场景：ViewModel 中的测试命令
- **给定** 实例化 `SettingsWindowViewModel`
- **则** ViewModel 应有 `TestSoundDevice` 命令属性
- **且** 命令应以 `ReactiveCommand` 实现
- **且** 命令应执行异步委托 `TestSoundDeviceAsync()`
- **且** 命令应使用 `[ReactiveCommand]` 属性以支持源生成

#### 场景：ViewModel 具有测试状态属性
- **给定** 实例化 `SettingsWindowViewModel`
- **则** ViewModel 应有布尔属性 `IsSoundDeviceTestRunning`
- **且** 属性应有 `[Reactive]` 以支持变更通知
- **且** ViewModel 应有 string? 属性 `SoundDeviceTestResult`
- **且** 该属性应有 `[Reactive]` 以支持变更通知

#### 场景：服务依赖注入
- **给定** 通过依赖注入实例化 `SettingsWindowViewModel`
- **则** 构造函数应接受 `ISoundDeviceService` 参数
- **且** 参数应赋给只读字段 `_soundDeviceService`
- **且** 在 `TestSoundDeviceAsync()` 中应使用该字段调用测试方法

#### 场景：命令 canExecute 逻辑
- **给定** 已初始化 `SettingsWindowViewModel`
- **则** `TestSoundDevice` 命令应使用 canExecute 可观察量创建
- **且** 仅当 `SoundDeviceEnabled` 为 true 时命令应可执行
- **且** 命令应使用 `this.WhenAnyValue(x => x.SoundDeviceEnabled)` 可观察量
- **且** 当 `SoundDeviceEnabled` 变化时，命令可执行性应更新

#### 场景：View 绑定 ViewModel
- **给定** 已定义设置窗口 XAML
- **则** 测试按钮的 Command 应绑定到 `{Binding TestSoundDevice}`
- **且** 测试按钮的 IsEnabled 应绑定到 `{Binding IsSoundDeviceTestRunning, Converter=...}`（取反）
- **且** 状态 TextBlock 的 Text 应绑定到 `{Binding SoundDeviceTestResult}`
- **且** 状态 TextBlock 的 Visibility 应通过转换器在结果非空时显示

---

### 需求：错误处理与日志

系统应优雅处理所有异常，并为调试与监控提供完整日志。

#### 场景：服务层异常处理
- **给定** `PlayTextV2TestAsync()` 正在执行
- **当** 发生异常（网络错误、超时、设备错误）
- **则** 方法应捕获异常
- **且** 方法应通过 Serilog 记录带完整异常详情的错误
- **且** 方法应向调用方重新抛出异常
- **且** 日志消息应包含上下文：「Sound device test failed」

#### 场景：ViewModel 层异常处理
- **给定** `TestSoundDeviceAsync()` 命令正在执行
- **当** `PlayTextV2TestAsync()` 抛出异常
- **则** ViewModel 应在 catch 块中捕获异常
- **且** ViewModel 应记录带异常详情的错误
- **且** ViewModel 应将 `SoundDeviceTestResult` 设为用户友好的错误信息
- **且** ViewModel 应在 finally 块中确保 `IsSoundDeviceTestRunning` 设为 false
- **且** 用户应在 UI 中看到错误信息

#### 场景：网络错误的专门处理
- **给定** 测试执行遇到网络连通问题
- **当** 抛出 `HttpRequestException`
- **则** ViewModel 应捕获该异常类型
- **且** ViewModel 应记录错误并显示专门消息：「测试失败: 网络错误，请检查音响设备IP地址」

#### 场景：超时的专门处理
- **给定** 测试执行超时（30 秒已过）
- **当** 抛出 `TaskCanceledException`
- **则** ViewModel 应捕获该异常类型并记录超时错误
- **且** ViewModel 应显示专门消息：「测试失败: 请求超时，请检查音响设备是否在线」

#### 场景：各阶段均记录日志
- **给定** 触发测试操作
- **当** 测试开始，系统应记录：「Starting sound device test with text: 音柱测试」
- **当** 测试成功，系统应记录：「Sound device test succeeded」
- **当** 测试失败，系统应记录带异常详情与上下文的错误；所有日志应使用适当级别（Information、Warning、Error）

---

### 需求：内存管理与资源释放

系统应在测试操作中正确管理资源并避免内存泄漏。

#### 场景：服务层 HttpClient 释放
- **给定** 测试方法调用 `PlayTextV2Async()`
- **当** 为 HTTP 请求创建 HttpClient
- **则** HttpClient 应置于 try-finally 中，并在 finally 中释放；不应发生套接字泄漏

#### 场景：Rx 订阅管理
- **给定** 使用 canExecute 可观察量创建 `TestSoundDevice` 命令
- **当** ViewModel 被释放或窗口关闭
- **则** 命令订阅应被正确释放；Rx 订阅不应导致内存泄漏；重复执行测试时内存使用应保持稳定

#### 场景：ReactiveUI 命令生命周期
- **给定** 通过 `ReactiveCommand.CreateFromTask()` 创建 `TestSoundDevice` 命令
- **当** 多次执行命令
- **则** 每次执行应干净完成；命令不应累积状态；内存使用应保持恒定

---

### 需求：测试操作超时

系统应对测试操作实施合理超时，防止无限阻塞。

#### 场景：HTTP 请求超时
- **给定** 测试执行进行中且音响设备无响应
- **当** HTTP 请求在 30 秒内未完成
- **则** HttpClient 应因超时抛出 `TaskCanceledException`；异常应被捕获并记录；用户应看到超时错误信息；测试按钮应变为启用

#### 场景：整体操作超时
- **给定** 测试包含多次重试（按现有 `PlayTextV2Async` 逻辑），每次尝试 30 秒超时
- **当** 全部 8 次重试均超时
- **则** 总测试时长不应超过约 240 秒（8×30s）；操作应以错误结束；执行期间 UI 应保持响应

---

### 需求：UI 线程不阻塞

系统应在异步测试操作期间保持 UI 响应。

#### 场景：调用链全程使用 async/await
- **给定** 用户点击测试按钮
- **当** `TestSoundDeviceAsync()` 命令执行
- **则** 方法应标记为 `async Task`；调用 `PlayTextV2TestAsync()` 时应使用 await；HTTP 请求期间 UI 线程不应被阻塞；窗口应仍可拖动和响应

#### 场景：在 UI 线程上的状态更新
- **给定** 测试在后台线程执行
- **当** 设置 `IsSoundDeviceTestRunning` 或 `SoundDeviceTestResult`
- **则** ReactiveUI 应自动将属性变更封送到 UI 线程；UI 应立即更新且无需 Dispatcher.Invoke；按钮启用状态与状态文本应同步更新

---

## MODIFIED 需求

*无——此为新增功能，未修改现有需求。*

---

## REMOVED 需求

*无——此为新增功能，未移除任何需求。*
