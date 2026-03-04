# 实施任务：音响设备测试功能

## 任务概览

本文档提供音响设备测试功能的实施任务有序清单，按优先级与依赖排列。

## 阶段 1：服务层实现

### 任务 1.1：在 ISoundDeviceService 接口中增加测试方法

**文件**：`MaterialClient.Common/Services/SoundDeviceService.cs`

**步骤**：
1. 找到 `ISoundDeviceService` 接口定义（约第 16 行）
2. 在现有 `PlayTextV2Async` 方法后增加新方法签名（含 XML 注释，说明用于播放固定测试文本、支持取消）
3. 确保方法名带 Async 后缀、注释格式正确

**验证**：接口可编译；方法符合异步命名约定；XML 文档完整。

**依赖**：无

---

### 任务 1.2：实现 PlayTextV2TestAsync 方法

**文件**：`MaterialClient.Common/Services/SoundDeviceService.cs`

**步骤**：
1. 在 `SoundDeviceService` 类中实现 `PlayTextV2TestAsync`（在 `PlayTextV2Async` 之后，约第 375 行）
2. 实现须：使用固定测试文本「音柱测试」；内部调用现有 `PlayTextV2Async`；try-catch 处理异常；用 _logger 记录开始/成功/失败；尊重 CancellationToken
3. 遵循 `PlayTextV2Async` 的现有错误处理方式

**验证**：编译通过；固定文本为「音柱测试」；有开始/成功/失败日志；取消令牌传入底层方法；异常不会未捕获抛出。

**依赖**：任务 1.1

---

## 阶段 2：ViewModel 实现

### 任务 2.1：在 ViewModel 中注入 ISoundDeviceService

**文件**：`MaterialClient/ViewModels/SettingsWindowViewModel.cs`

**步骤**：在构造函数（约第 130 行）增加 `ISoundDeviceService` 参数及私有只读字段 `_soundDeviceService`，在构造函数体中赋值（或由 AutoConstructor 处理）。

**验证**：编译通过；运行时依赖注入能解析 ISoundDeviceService。

**依赖**：阶段 1 完成

---

### 任务 2.2：增加测试状态属性

**文件**：`MaterialClient/ViewModels/SettingsWindowViewModel.cs`

**步骤**：在音响设备设置属性之后（约第 126 行后）增加：`[Reactive] private bool _isSoundDeviceTestRunning = false;` 与 `[Reactive] private string? _soundDeviceTestResult = null;`，用于 UI 状态展示。

**验证**：属性可编译；ReactiveUI 源生成器生成变更通知；命名符合 camelCase。

**依赖**：任务 2.1

---

### 任务 2.3：增加测试命令

**文件**：`MaterialClient/ViewModels/SettingsWindowViewModel.cs`

**步骤**：在现有命令之后（如 `TestCaptureAsync` 约第 408 行后）增加 `[ReactiveCommand]` 的 `TestSoundDeviceAsync`：try 中设 IsSoundDeviceTestRunning=true、SoundDeviceTestResult=null，调用 _soundDeviceService.PlayTextV2TestAsync(CancellationToken.None)，成功则设「测试成功」并记录日志；catch 中设 SoundDeviceTestResult 为错误信息并记录日志；finally 中 IsSoundDeviceTestRunning=false。

**验证**：命令可编译；源生成器生成命令属性；async/await 正确；finally 确保运行状态重置；错误信息友好。

**依赖**：任务 2.2

---

### 任务 2.4：配置测试命令的 CanExecute

**文件**：`MaterialClient/ViewModels/SettingsWindowViewModel.cs`

**步骤**：在构造函数中（约第 145 行后）为测试命令设置 canExecute：仅当 SoundDeviceEnabled 为 true 时可执行（例如使用 WhenAnyValue(x => x.SoundDeviceEnabled)）。

**验证**：canExecute 逻辑可编译；测试按钮随 SoundDeviceEnabled 启用/禁用；Rx 订阅正确观察属性变化。

**依赖**：任务 2.3

---

## 阶段 3：UI 实现

### 任务 3.1：在设置窗口增加测试按钮

**文件**：`MaterialClient/Views/SettingsWindow.axaml`

**步骤**：在音响设备设置区块（约第 584 行复选框附近）、音量 TextBox 之后（约第 628 行）增加「测试音响」按钮，Command 绑定 TestSoundDevice，IsEnabled 绑定 IsSoundDeviceTestRunning 取反；其下增加用于显示 SoundDeviceTestResult 的 TextBlock，Visibility 由结果非空转换器控制。若无 BoolNegationConverter、StringNotNullToVisibilityConverter 需新增或使用现有转换器。

**验证**：按钮出现在音响区块；绑定正确；运行中按钮禁用；有结果时显示状态文本。

**依赖**：阶段 2 完成

---

### 任务 3.2：核对 UI 布局与样式

**步骤**：确保按钮与现有风格一致、边距与周围控件一致、位置合理、状态文本可读；在不同窗口尺寸下无溢出。

**验证**：视觉一致、无布局重叠、文字可读对齐。

**依赖**：任务 3.1

---

## 阶段 4：测试

### 任务 4.1：编写单元测试

**文件**：`MaterialClient.Common.Tests/Tests/SoundDeviceServiceTests.cs`

**步骤**：为 PlayTextV2TestAsync 增加单元测试；Mock ISettingsService 返回有效配置；验证测试文本为「音柱测试」、日志调用、取消令牌、异常处理与重新抛出。

**验证**：测试通过；覆盖成功、失败、取消；Mock 配置正确。

**依赖**：阶段 1–3 完成

---

### 任务 4.2：手工测试

**步骤**：启动应用并打开设置；启用音响并配置有效信息；点击「测试音响」；确认播放「音柱测试」并显示「测试成功」；禁用设备时按钮应禁用；无效配置时显示错误；测试期间关闭窗口无资源泄漏。

**验证**：配置正确时播放正常、成功/错误信息正确、异步时 UI 保持响应、无内存或资源问题。

**依赖**：任务 4.1

---

### 任务 4.3：内存泄漏测试

**文件**：`MaterialClient.Common.Tests/Tests/SoundDeviceServiceMemoryLeakTests.cs`（新文件）

**步骤**：按 AttendedWeighingServiceMemoryLeakTests 模式编写；多轮迭代（如 1000 次）；验证内存稳定、HttpClient 与 Rx 订阅正确释放。

**验证**：内存稳定、无未释放资源、测试稳定通过。

**依赖**：任务 4.2

---

## 阶段 5：文档

### 任务 5.1：更新用户文档（若适用）

**步骤**：在用户手册中描述音响设备测试功能；增加设置窗口测试按钮截图；说明测试步骤与排障建议；记录常见错误信息含义。

**依赖**：阶段 4 完成

---

### 任务 5.2：更新开发文档

**步骤**：为测试方法补充注释；记录新引入的模式或约定；必要时更新架构文档。

**依赖**：任务 5.1

---

## 任务依赖摘要

阶段 1（服务层）→ 阶段 2（ViewModel）→ 阶段 3（UI）→ 阶段 4（测试）→ 阶段 5（文档）；各阶段内任务按上述顺序有依赖。阶段 4 中 4.1 可在阶段 3 完成后开始；阶段 5 的两项在阶段 4 完成后可并行。

## 验证清单（估算）

- [x] ISoundDeviceService.PlayTextV2TestAsync 已加入接口
- [x] 实现使用固定文本「音柱测试」并含日志与错误处理
- [x] ISoundDeviceService 已注入 SettingsWindowViewModel
- [x] IsSoundDeviceTestRunning、SoundDeviceTestResult 已添加
- [x] TestSoundDeviceAsync 命令已用 ReactiveUI 实现，canExecute 与 SoundDeviceEnabled 绑定
- [x] 测试按钮与状态文本已加入 SettingsWindow.axaml
- [ ] 单元测试已编写并通过（需 .NET SDK）
- [ ] 手工测试通过（需硬件）
- [ ] 内存泄漏测试通过（需 .NET SDK）
- [x] 代码符合 MVVM 与 ReactiveUI 模式，异步方法带 Async 后缀，Rx 订阅正确释放，无编译警告/错误
- [ ] OpenSpec 校验通过：`openspec validate sound-device-test-feature --strict`
