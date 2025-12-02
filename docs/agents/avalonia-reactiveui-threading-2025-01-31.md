# Agent 会话归档（Avalonia ReactiveUI 跨线程问题修复）

日期：2025-01-31

## 问题描述
在 Avalonia + ReactiveUI 应用中，授权码验证窗口在执行异步 HTTP 请求后出现跨线程访问错误：
```
System.InvalidOperationException: Call from invalid thread
at Avalonia.Controls.Button.get_Command()
at Avalonia.Controls.Button.CanExecuteChanged(Object sender, EventArgs e)
at ReactiveUI.ReactiveCommandBase.OnCanExecuteChanged(Boolean newValue)
```

## 问题根源
1. **异步上下文切换**：
   - 用户点击按钮 → 在 UI 线程执行 `VerifyAuthorizationCodeAsync()`
   - 执行 `await _basePlatformApi.GetAuthClientLicenseAsync(request)` → HTTP 请求在后台线程完成
   - `await` 后的代码在后台线程继续执行
   - 属性更新（`IsVerified = true`）在后台线程触发 ReactiveCommand 的 `CanExecuteChanged`
   - Button 响应事件时在后台线程访问 Command 属性 → 跨线程错误

2. **ReactiveUI 未正确初始化**：
   - 应用启动时缺少 `.UseReactiveUI()` 配置
   - 导致 ReactiveUI 与 Avalonia UI 线程未正确集成

## 解决方案

### 1. ReactiveCommand 指定 outputScheduler
修改 `AuthCodeWindowViewModel.cs`，为 ReactiveCommand 指定输出调度器：

```csharp
using Avalonia.ReactiveUI;  // 添加此 using

public AuthCodeWindowViewModel(ILicenseService licenseService)
{
    _licenseService = licenseService;
    
    // 指定 outputScheduler 确保所有通知在 UI 线程触发
    VerifyCommand = ReactiveCommand.CreateFromTask(
        VerifyAuthorizationCodeAsync,
        outputScheduler: AvaloniaScheduler.Instance  // 关键修改
    );
    RetryCommand = ReactiveCommand.Create(
        ResetForm,
        outputScheduler: AvaloniaScheduler.Instance  // 关键修改
    );
}
```

**原理**：
- `outputScheduler` 参数告诉 ReactiveCommand 所有状态变化通知都在指定调度器执行
- 即使属性在后台线程设置，`CanExecuteChanged` 也会调度到 UI 线程
- Button 响应通知时已在 UI 线程，避免跨线程访问

### 2. 启用 ReactiveUI 集成
修改 `Program.cs`，在应用构建时启用 ReactiveUI：

```csharp
using Avalonia.ReactiveUI;  // 添加此 using

public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .UseReactiveUI();  // 关键配置
```

**作用**：
- 正确初始化 ReactiveUI 与 Avalonia 的集成
- 确保 `AvaloniaScheduler.Instance` 能正常工作
- 使 ReactiveUI 的调度机制与 Avalonia UI 线程同步

## 关键路径
- ViewModel：`MaterialClient/ViewModels/AuthCodeWindowViewModel.cs`
- 应用入口：`MaterialClient/Program.cs`
- View：`MaterialClient/Views/AuthCodeWindow.axaml.cs`

## 尝试过的方案（未成功）

### 方案 1：属性 setter 中手动调度
在每个属性的 setter 中使用 `Dispatcher.UIThread.CheckAccess()` 和 `Post()`：
```csharp
public bool IsVerifying
{
    set
    {
        if (Dispatcher.UIThread.CheckAccess())
            this.RaiseAndSetIfChanged(ref _isVerifying, value);
        else
            Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _isVerifying, value));
    }
}
```

**失败原因**：
- `Post()` 是异步的，setter 立即返回
- ReactiveCommand 的内部机制在 `Post()` 调度之前就触发了 `CanExecuteChanged`
- 仍然在原线程（后台线程）触发通知

### 方案 2：View 中使用 ObserveOn
在 View 的订阅中添加 `ObserveOn`：
```csharp
viewModel.WhenAnyValue(vm => vm.IsVerified)
    .ObserveOn(AvaloniaScheduler.Instance)
    .Subscribe(async isVerified => { ... });
```

**失败原因**：
- 只解决了 View 订阅的线程问题
- 未解决 Button 的 Command 绑定触发的 `CanExecuteChanged` 跨线程问题
- ReactiveCommand 仍在后台线程触发通知

## 技术要点

### ReactiveCommand 的 outputScheduler 参数
- **作用**：指定命令执行结果和状态变化通知的调度器
- **默认行为**：在调用线程触发通知
- **UI 应用**：必须指定 UI 线程调度器（Avalonia 使用 `AvaloniaScheduler.Instance`）

### Avalonia 中的调度器选择
- ❌ `RxApp.MainThreadScheduler`：ReactiveUI 通用调度器，在 Avalonia 中可能未正确初始化
- ✅ `AvaloniaScheduler.Instance`：Avalonia 专用调度器，与 Avalonia UI 线程完美集成

### async/await 的线程上下文
- `await` 前：在调用线程（UI 线程）
- `await` 期间：请求在后台线程/线程池执行
- `await` 后：默认在完成时的线程（通常是后台线程）继续执行
- **解决**：使用 outputScheduler 确保通知在 UI 线程

## 最佳实践

1. **所有 ReactiveCommand 都指定 outputScheduler**：
   ```csharp
   ReactiveCommand.CreateFromTask(method, outputScheduler: AvaloniaScheduler.Instance)
   ```

2. **应用启动时启用 ReactiveUI**：
   ```csharp
   BuildAvaloniaApp().UseReactiveUI()
   ```

3. **属性保持简洁**：
   ```csharp
   // 推荐：简单的 setter
   set => this.RaiseAndSetIfChanged(ref _field, value);
   
   // 不推荐：手动处理线程切换（复杂且易出错）
   ```

4. **异步方法中直接更新属性**：
   - 不需要在 async 方法中手动切换到 UI 线程
   - outputScheduler 会自动处理通知的线程调度

## 验证方法
1. 运行应用，打开授权码窗口
2. 输入授权码，点击"确定"
3. 等待异步 HTTP 请求完成
4. 验证不再出现 "Call from invalid thread" 错误
5. 确认 UI 正常更新（显示"授权成功！"并自动关闭窗口）

## 相关资源
- [ReactiveUI Scheduling](https://www.reactiveui.net/docs/handbook/scheduling/)
- [Avalonia ReactiveUI Integration](https://docs.avaloniaui.net/docs/concepts/reactiveui/)
- ReactiveCommand outputScheduler 参数说明

## 后续建议
- 检查其他 ViewModel 中的 ReactiveCommand 是否也需要添加 outputScheduler
- 特别是包含异步操作的 Command（如 `LoginViewModel`、`AttendedWeighingViewModel`）
- 考虑创建基类辅助方法简化 outputScheduler 的使用
