# 设计文档：音响设备测试功能

## 概览

本文说明在 MaterialClient 中实现音响设备测试功能的技术设计与架构决策。

## 架构上下文

### 现有音响设备架构

当前音响设备实现采用以下架构：

```
┌─────────────────────────────────────────────────────────────┐
│                    SettingsWindow (View)                     │
│  - Sound device configuration UI (checkbox, IP, SN, volume) │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Data Binding
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SettingsWindowViewModel (ViewModel)             │
│  - Sound device settings properties                         │
│  - Save/Cancel commands                                     │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Service Calls
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              ISoundDeviceService (Service Interface)         │
│  - PlayTextAsync(text, cancellationToken)                   │
│  - PlayTextV2Async(text, cancellationToken)                 │
│  - IsOnlineAsync()                                          │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Implementation
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SoundDeviceService (Service)                    │
│  - HTTP calls to sound device API                           │
│  - TTS URI construction                                     │
│  - Retry logic (8 attempts)                                 │
│  - Logging via Serilog                                      │
└─────────────────────────────────────────────────────────────┘
```

### 新增组件

本提案增加以下组件（以粗体标出）：

```
┌─────────────────────────────────────────────────────────────┐
│                    SettingsWindow (View)                     │
│  - Sound device configuration UI                            │
│  + Test button                                              │
│  + Status display                                           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SettingsWindowViewModel (ViewModel)             │
│  - Sound device settings properties                         │
│  - Save/Cancel commands                                     │
│  + IsSoundDeviceTestRunning property                        │
│  + SoundDeviceTestResult property                           │
│  + TestSoundDevice command                                  │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              ISoundDeviceService (Service Interface)         │
│  - PlayTextAsync(text, cancellationToken)                   │
│  - PlayTextV2Async(text, cancellationToken)                 │
│  - IsOnlineAsync()                                          │
│  + PlayTextV2TestAsync(cancellationToken)                   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              SoundDeviceService (Service)                    │
│  - HTTP calls to sound device API                           │
│  - TTS URI construction                                     │
│  - Retry logic (8 attempts)                                 │
│  - Logging via Serilog                                      │
│  + PlayTextV2TestAsync implementation                       │
└─────────────────────────────────────────────────────────────┘
```

## 设计决策

### 决策 1：专用测试方法 vs 复用 PlayTextV2Async

**问题**：应新增专用方法 `PlayTextV2TestAsync`，还是在 ViewModel 中直接调用 `PlayTextV2Async("音柱测试")`？

**选项**：

| 选项 | 优点 | 缺点 |
|--------|------|------|
| A) 新增专用测试方法 | • 语义清晰分离<br>• 便于后续增加测试专用逻辑<br>• 更易测试<br>• 符合面向服务架构 | • 代码略多 |
| B) 在 ViewModel 中调用 PlayTextV2Async | • 代码少<br>• 无需改接口 | • 紧耦合<br>• 难以扩展<br>• 职责混合 |

**决策**：**选项 A** - 新增专用方法 `PlayTextV2TestAsync`

**理由**：
1. **职责分离**：ViewModel 不应知晓测试专用实现细节
2. **可扩展性**：未来可能需要测试专用行为（不同超时、日志、诊断）
3. **可测试性**：更易 mock 与独立测试
4. **语义清晰**：业务上「测试」与「播放」含义不同
5. **一致性**：符合现有面向服务模式

**实现**：
```csharp
// In SoundDeviceService
public async Task PlayTextV2TestAsync(CancellationToken cancellationToken = default)
{
    const string testText = "音柱测试";
    _logger?.LogInformation("Starting sound device test with text: {TestText}", testText);

    try
    {
        await PlayTextV2Async(testText, cancellationToken);
        _logger?.LogInformation("Sound device test completed successfully");
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Sound device test failed");
        throw;
    }
}
```

---

### 决策 2：测试文本内容

**问题**：音响设备测试应使用什么文本？

**选项**：

| 选项 | 优点 | 缺点 |
|--------|------|------|
| A) 固定文本："音柱测试" | • 简单清晰<br>• 自解释<br>• 简短（缩短测试时间） | • 不如完整句子信息多 |
| B) 固定文本："音响设备测试中" | • 更描述性 | • 更长（增加测试时间） |
| C) 可配置测试文本 | • 最大灵活性 | • 增加复杂度（UI、设置）<br>• 对本用例过度 |
| D) 随机测试短语 | • 测试多样性 | • 用户困惑<br>• 难以文档化 |

**决策**：**选项 A** - 固定文本「音柱测试」

**理由**：
1. **简单性**：无需额外 UI 或配置
2. **清晰度**：用户立即理解正在测试什么
3. **效率**：短文本最小化测试时长
4. **一致性**：每次相同测试便于排障
5. **自解释**：文本名称与功能名称一致

---

### 决策 3：测试状态反馈机制

**问题**：测试状态应如何传达给用户？

**选项**：

| 选项 | 优点 | 缺点 |
|--------|------|------|
| A) 带状态消息的 TextBlock | • 实现简单<br>• 反馈清晰 | • 占用额外屏幕空间 |
| B) 测试期间按钮文字变化 | • 不占额外空间 | • 信息较少<br>• 难以展示错误详情 |
| C) 进度对话框 | • 非常明确 | • 对快速测试过度<br>• 阻塞 UI |
| D) Toast 通知 | • 非阻塞<br>• 现代 UX | • 可能被忽略<br>• 需 Toast 基础设施 |

**决策**：**选项 A** - 带状态消息的 TextBlock

**理由**：
1. **简单性**：符合现有 Avalonia UI 模式
2. **信息密度**：可展示详细错误消息
3. **可见性**：测试期间及之后始终可见
4. **一致性**：SettingsWindow 中已有类似模式
5. **无新基础设施**：利用现有绑定机制

**实现**：
```csharp
// ViewModel properties
[Reactive] private bool _isSoundDeviceTestRunning = false;
[Reactive] private string? _soundDeviceTestResult = null;

// Command
[ReactiveCommand]
private async Task TestSoundDeviceAsync()
{
    try
    {
        IsSoundDeviceTestRunning = true;
        SoundDeviceTestResult = null;

        await _soundDeviceService.PlayTextV2TestAsync(CancellationToken.None);

        SoundDeviceTestResult = "测试成功";
    }
    catch (Exception ex)
    {
        SoundDeviceTestResult = $"测试失败: {ex.Message}";
    }
    finally
    {
        IsSoundDeviceTestRunning = false;
    }
}
```

```xml
<!-- View -->
<Button Content="测试音响"
        Command="{Binding TestSoundDevice}"
        IsEnabled="{Binding IsSoundDeviceTestRunning, Converter={x:Static Converters:BoolNegationConverter.Instance}}" />

<TextBlock Text="{Binding SoundDeviceTestResult}"
           Visibility="{Binding SoundDeviceTestResult, Converter={x:Static Converters:StringNotNullToVisibilityConverter.Instance}}" />
```

---

### 决策 4：命令启用逻辑

**问题**：测试按钮何时应启用？

**选项**：

| 选项 | 优点 | 缺点 |
|--------|------|------|
| A) 仅当 SoundDeviceEnabled 为 true 时启用 | • 逻辑清晰<br>• 避免无效调用 | • 可能用不完整配置尝试测试 |
| B) SoundDeviceEnabled 且配置有效时启用 | • 更防御性 | • 需响应式校验<br>• 更复杂 |
| C) 始终启用，测试时显示错误 | • 最简单 | • 体验差<br>• 浪费用户时间 |

**决策**：**选项 A** - 仅当 `SoundDeviceEnabled` 为 true 时启用

**理由**：
1. **简单性**：直接的响应式绑定
2. **一致性**：与现有模式一致（如其他功能用启用/禁用开关）
3. **足够好**：现有 `PlayTextV2Async` 已校验配置并优雅返回
4. **性能**：避免响应式校验开销

**实现**：
```csharp
// In ViewModel constructor
TestSoundDevice = ReactiveCommand.CreateFromTask(
    TestSoundDeviceAsync,
    this.WhenAnyValue(x => x.SoundDeviceEnabled).Select(enabled => enabled)
);
```

**说明**：若出现校验问题，可后续无破坏性增强为选项 B。

---

### 决策 5：取消令牌处理

**问题**：测试命令是否支持取消？若支持，如何实现？

**选项**：

| 选项 | 优点 | 缺点 |
|--------|------|------|
| A) 使用 CancellationToken.None | • 简单<br>• 可预期的 30s 超时 | • 无法取消长时间测试 |
| B) 使用命令的 CancellationToken | • 支持取消 | • 更复杂<br>• 需取消令牌源 |
| C) 增加取消按钮对 | • 非常明确 | • 使 UI 杂乱<br>• 对 30s 操作过度 |

**决策**：**选项 A** - 先使用 `CancellationToken.None`，在文档中说明可后续增强

**理由**：
1. **简单性**：30 秒超时对测试操作合理
2. **无 UI 杂乱**：无需取消按钮
3. **足够**：用户可关闭设置窗口以「取消」
4. **可扩展性**：后续可无破坏性增加取消令牌支持

**未来增强路径**：
若取消变得重要：在 ViewModel 增加 `CancellationTokenSource` 字段；增加调用 `cts.Cancel()` 的取消命令；将 `cts.Token` 传入 `PlayTextV2TestAsync`；在 `Dispose()` 模式中确保正确释放。

---

### 决策 6：错误处理策略

**问题**：测试错误应如何处置并传达？

**选项**：

| 选项 | 优点 | 缺点 |
|--------|------|------|
| A) 捕获所有异常，在 UI 中展示 | • 用户友好<br>• 不崩溃 | • 可能掩盖编程错误 |
| B) 让异常传播 | • 快速失败<br>• 易调试 | • 体验差<br>• 可能崩溃应用 |
| C) 记录并重新抛出 | • 两全其美 | • 需全局错误处理 |

**决策**：**选项 A** - 捕获所有异常，在 UI 中展示并记录日志

**理由**：
1. **用户体验**：测试操作非关键，不应导致崩溃
2. **调试**：Serilog 记录完整异常详情
3. **一致性**：与现有 `PlayTextV2Async` 错误处理一致
4. **安全**：网络错误、超时、设备离线为预期场景

**实现**：（见原文完整 catch 块，含 HttpRequestException、TaskCanceledException 等）

---

### 决策 7：UI 按钮位置

**问题**：测试按钮应放在设置窗口何处？

**选项**：

| 选项 | 优点 | 缺点 |
|--------|------|------|
| A) 音量 TextBox 之后、下一节之前 | • 逻辑分组<br>• 易找到 | • 可能使区块拥挤 |
| B) 单独「测试」区块 | • 非常规整 | • 对单个按钮过度 |
| C) 窗口底部工具栏 | • 全局测试区 | • 上下文少<br>• 难找到 |

**决策**：**选项 A** - 音量 TextBox 之后、下一节之前

**理由**：
1. **逻辑分组**：测试属于音响设备配置
2. **可发现性**：启用设备的用户会立即看到测试选项
3. **一致性**：与相机区块「测试抓拍」放置方式类似
4. **最小改动**：无需大改 UI 结构

**UI 布局**：（见原文 ASCII 布局图）

---

## 数据流

### 正常测试流（成功）

用户点击「测试音响」→ TestSoundDevice 执行 → IsSoundDeviceTestRunning = true → 按钮禁用 → await PlayTextV2TestAsync → 服务内 PlayTextV2Async("音柱测试") → 成功 → SoundDeviceTestResult = "测试成功" → IsSoundDeviceTestRunning = false → 按钮启用 → 状态显示「测试成功」。

### 错误流

请求失败/超时 → 服务记录并抛出 → ViewModel catch 设置 SoundDeviceTestResult → finally 中 IsSoundDeviceTestRunning = false → 状态显示错误信息。

---

## 内存管理

### Rx 订阅释放

**关注点**：ReactiveUI 命令订阅若未正确释放可能导致内存泄漏。

**缓解**：ReactiveCommand 使用 CreateFromTask 正确管理订阅；ViewModel 为瞬时生命周期；SettingsWindow 短生命周期；测试命令无长生命周期订阅。

**验证计划**：按 AttendedWeighingServiceMemoryLeakTests 模式做内存泄漏测试；执行 1000 次测试命令；验证内存使用稳定。

### HttpClient 释放

**现状**：PlayTextV2Async 中 HttpClient 已在 try-finally 的 finally 中 Dispose，已正确处置。

---

## 安全考虑

### 输入验证

测试文本为固定常量，无用户输入；无 SQL 注入风险；桌面应用无 XSS 风险。

### 网络安全

使用现有 HTTP 客户端基础设施；无新网络端点暴露；TLS/SSL 遵循现有音响设备 API 配置。

### 日志安全

测试文本会记录，无敏感数据；错误信息可能含设备 IP/SN，与现有代码一致；不涉及用户凭据或密钥。

---

## 性能考虑

### 异步操作

测试全异步，不阻塞 UI；30 秒超时防止无限阻塞；HttpClient 超时已适当配置。

### 内存与 CPU

仅增加少量内存（两个字符串属性、一个命令）；无大缓冲或集合；无后台定时器或计划任务；主要为 I/O 等待。

---

## 测试策略

### 单元测试

验证：PlayTextV2TestAsync 使用固定测试文本；日志调用适当；异常被捕获并重新抛出；取消令牌传递。使用 ISettingsService mock 与内存测试替身。

### 集成测试

端到端与真实音响设备；UI 按钮启用/禁用正确；状态更新传递到 UI；设备离线时的错误处理。

### 内存泄漏测试

重复执行后无内存泄漏；Rx 订阅正确释放；HttpClient 正确释放。按现有模式做 1000+ 次迭代，用 dotMemory 或 VS Profiler 监控。

---

## 未来增强

可考虑：高级诊断（网络连通、设备状态、音量校准）；可配置测试文本；测试历史；批量测试；增强取消（取消按钮 + CancellationTokenSource）。当前设计支持在不破坏变更的前提下增加上述能力。

---

## 结论

本设计文档描述了一种简单、可维护的音响设备测试实现：遵循现有 MVVM 与 ReactiveUI 模式、保持职责分离、提供清晰反馈、优雅处理错误、避免内存泄漏并支持后续增强。实现直接、风险低，能立即让用户快速验证音响设备配置。
