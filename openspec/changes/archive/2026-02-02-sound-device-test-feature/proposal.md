# 提案：音响设备测试功能

## 元数据

- **变更 ID**：`sound-device-test-feature`
- **标题**：音响设备测试功能
- **状态**：ExecutionCompleted
- **创建日期**：2025-02-02
- **作者**：AI Assistant

## 概览

在 MaterialClient 应用中为音响设备增加快速测试能力。当前用户可在设置窗口中启用/禁用音响设备，但无法在不走完整称重流程的情况下验证设备是否工作正常。本提案在设置 UI 中增加测试按钮，点击后播放固定测试语音「音柱测试」，以验证音响设备功能。

## 问题陈述

### 当前限制

1. **配置验证困难**：在设置窗口中启用音响设备后，用户无法立即验证设备配置是否正确（IP、SN、音量等）
2. **排障效率低**：音响设备故障只能在实际称重流程中发现，需完整业务流才能复现问题
3. **体验不佳**：缺少即时反馈机制，配置变更后对设备状态存在不确定性

### 影响

- 系统管理员需完成完整称重流程才能测试音响设备变更，浪费时间
- 配置错误（错误 IP、序列号、网络问题）发现较晚
- 无法在初次部署或更换硬件后验证设备功能

## 提议方案

### 1. UI 层变更（SettingsWindow）

在设置窗口的音响设备配置区块增加**测试按钮**：

- **位置**：紧邻音响设备启用/禁用开关
- **功能**：点击触发音响设备测试
- **状态反馈**：显示测试进行中及最终结果（成功/失败/超时）
- **可用性**：仅当音响设备已启用且配置正确时可用

### 2. 服务层实现（ISoundDeviceService）

在 `ISoundDeviceService` 接口中增加新的测试方法：

```csharp
Task PlayTextV2TestAsync(CancellationToken cancellationToken);
```

**实现要求**：

- **固定测试文本**：始终使用「音柱测试」
- **取消支持**：通过 `CancellationToken` 支持操作取消
- **异常处理**：捕获并记录设备异常，提供用户可读的错误信息
- **实现位置**：`MaterialClient.Common/Services/SoundDeviceService.cs`

### 3. MVVM 集成

遵循项目 MVVM 架构：

- **ViewModel**：在 `SettingsWindowViewModel.cs` 中增加测试命令
- **ReactiveUI**：使用 `ReactiveCommand` 绑定测试按钮
- **状态管理**：可观察属性反映测试状态
- **依赖注入**：通过构造函数注入 `ISoundDeviceService`

### 4. 错误处理与日志

- **Serilog**：记录测试开始、成功、失败
- **用户反馈**：在 UI 显示测试结果（成功/失败/超时信息）
- **超时机制**：配置合理超时避免长时间阻塞（默认 30 秒）

## 范围

### 范围内

- 在设置窗口音响设备区块增加测试按钮 UI
- 在 `SoundDeviceService` 中实现 `PlayTextV2TestAsync`
- 在 `SettingsWindowViewModel` 中用 ReactiveUI 增加测试命令
- 在 UI 中实现测试状态反馈（进行中、成功、失败）
- 为测试操作增加日志
- 正确传递与处理取消令牌

### 范围外

- 音响设备发现或自动配置
- 高级诊断（网络连通测试、音量校准）
- 多种测试短语或自定义测试文本
- 测试历史或统计
- 超出现有 `IsValid()` 的音响设备配置校验

## 影响分析

### 功能影响

- **新功能**：快速音响设备测试能力
- **修改文件**：SettingsWindow.axaml、SettingsWindowViewModel.cs、ISoundDeviceService、SoundDeviceService.cs

### 技术与性能影响

- **API**：在 `ISoundDeviceService` 增加新方法（向后兼容）
- **依赖**：无新外部依赖
- **内存**：须确保测试方法中 HttpClient 正确释放
- **线程**：使用 async/await 并正确处理取消令牌；测试短时（约 30 秒内），对现有称重流程无影响

### 安全与体验影响

- 无新增安全顾虑，使用现有音响设备 API；测试文本固定且非用户可控
- **正面**：可立即验证音响配置；减少排障摩擦；测试按钮为增量，不改变现有流程

## 技术约束

- **架构**：须遵循现有 MVVM（View-ViewModel 分离）、ReactiveUI（ReactiveCommand/ReactiveObject）、依赖注入（服务已注册为单例）
- **平台**：仅 Windows x64、.NET 10.0、Avalonia UI 11.3.9
- **代码风格**：异步方法带 Async 后缀、启用可空引用类型、使用 AutoConstructor 与 ReactiveUI.SourceGenerators、私有字段 _camelCase
- **内存**：Rx 订阅须正确释放；HttpClient 使用后须释放；取消令牌须正确注册避免泄漏

## 依赖

- **内部**：ISoundDeviceService、ISettingsService、IHttpClientFactory（或按现有模式直接实例化 HttpClient）
- **外部**：System.Reactive、Serilog、System.Text.Json

## 备选方案

### 备选 1：复用 PlayTextV2Async

在 ViewModel 中直接调用 `PlayTextV2Async("音柱测试")`。**拒绝**：紧耦合、无「测试」语义、后续难以增加测试专用行为。

### 备选 2：在主窗口增加测试按钮

**拒绝**：主 UI 杂乱；测试本质是配置验证，设置窗口是更合适位置。

### 备选 3：用 Refit 定义测试方法

**拒绝**：音响设备无专用「测试」端点，测试与生产共用播放 API，无需额外抽象。

## 迁移计划

- **完全向后兼容**：新增方法不破坏现有代码；无数据库或配置变更
- **部署**：可零停机部署；无需迁移步骤；测试按钮含义直观

## 测试策略

- **单元测试**：Mock ISettingsService，验证 PlayTextV2TestAsync、取消令牌、异常与日志、固定测试文本
- **集成测试**：与真实音响设备端到端；验证 UI 绑定与状态更新、超时与无效配置
- **手工测试**：启用设备后点击测试、禁用时按钮禁用、无效配置显示错误、关闭窗口不泄漏资源

## 成功标准

1. **功能**：测试按钮出现在设置窗口音响区块；点击播放「音柱测试」；UI 显示进行中/成功/错误；仅当设备启用且配置正确时可测
2. **技术**：无内存泄漏；取消令牌处理正确；异常均被捕获并记录；符合 MVVM 与 ReactiveUI
3. **体验**：测试在 30 秒内完成；结果反馈清晰；异步期间 UI 不卡顿

## 风险与缓解

- **硬件不可用**：用 mock 做单元测试，在真实硬件环境做集成验证，记录清晰不可达错误
- **Rx 订阅泄漏**：遵循现有命令释放模式、DisposeWith、按 AttendedWeighingServiceMemoryLeakTests 做内存泄漏测试
- **UI 阻塞**：全链路 async/await、ReactiveCommand 正确配置异步、30 秒超时

## 待决问题

目前无。

## 参考

- `openspec/project.md`、SoundDeviceService.cs、SettingsWindowViewModel.cs、SettingsWindow.axaml
