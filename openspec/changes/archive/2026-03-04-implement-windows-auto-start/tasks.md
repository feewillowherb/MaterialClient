## 1. 实现

### 1.1 创建 WindowsAutoStartService

- [x] 创建 `MaterialClient.Common/Services/WindowsAutoStartService.cs`
- [x] 定义接口 `IWindowsAutoStartService`，包含方法：
  - `Task EnableAutoStartAsync()`
  - `Task DisableAutoStartAsync()`
  - `Task<bool> IsAutoStartEnabledAsync()`
- [x] 使用 `Microsoft.Win32.Registry` 实现注册表操作
- [x] 注册表路径：`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
- [x] 注册表值名称：应用程序可执行文件名
- [x] 注册表值数据：可执行文件完整路径
- [x] 为注册表权限错误添加错误处理
- [x] 为注册表操作添加日志
- [x] 在 DI 容器中注册服务（通过 `ITransientDependency` - ABP 自动注册）

### 1.2 与 SettingsService 集成

- [x] 修改 `SettingsService.SaveSettingsAsync()`：
  - 保存设置到数据库后，检查 `SystemSettings.EnableAutoStart`
  - 若为 `true`，调用 `IWindowsAutoStartService.EnableAutoStartAsync()`
  - 若为 `false`，调用 `IWindowsAutoStartService.DisableAutoStartAsync()`
- [x] 妥善处理异常（记录警告，不使保存失败）
- [ ] 为集成添加单元测试

### 1.3 增加启动时同步

- [x] 在 `StartupService` 或 `App.axaml.cs` 中创建方法 `SyncAutoStartOnStartupAsync()`
- [x] 在应用启动时（ABP 初始化之后）：
  - 通过 `ISettingsService.GetSettingsAsync()` 从数据库加载设置
  - 通过 `IWindowsAutoStartService.IsAutoStartEnabledAsync()` 检查注册表状态
  - 比较数据库设置与注册表状态
  - 若不一致，按数据库设置写回注册表进行修复
- [x] 记录不一致日志便于排查
- [x] 同步失败不阻断应用启动

### 1.4 更新 SettingsWindowViewModel（可选增强）

- [ ] 在 `LoadSettingsAsync()` 中可选地校验注册表状态与数据库是否一致
- [ ] 若发现不一致，记录警告（不在此处自动修复，由启动时同步负责）
- [ ] 保证界面显示的状态正确
- **说明**：当前跳过——启动时同步已能保证一致性，需要时可后续补充。

## 2. 测试

### 2.1 单元测试

- [ ] 创建 `WindowsAutoStartServiceTests.cs`
- [ ] 对测试中的 `RegistryKey` 进行 Mock
- [ ] 测试 `EnableAutoStartAsync()` —— 验证注册表写入
- [ ] 测试 `DisableAutoStartAsync()` —— 验证注册表删除
- [ ] 测试 `IsAutoStartEnabledAsync()` —— 验证注册表读取
- [ ] 测试错误处理（权限拒绝、注册表不可用）
- [ ] 使用不同可执行路径进行测试

### 2.2 集成测试

- [ ] 编写完整流程的集成测试：
  - 保存 `EnableAutoStart = true` 的设置
  - 验证注册表项被创建
  - 保存 `EnableAutoStart = false` 的设置
  - 验证注册表项被删除
- [ ] 测试启动时同步：
  - 数据库设为启用，注册表设为禁用
  - 启动应用
  - 验证注册表项被创建
- [ ] 在真实 Windows 环境测试（非仅 Mock）

### 2.3 手工测试

- [ ] 在设置中启用开机自启，验证注册表项
- [ ] 在设置中禁用开机自启，验证注册表项被删除
- [ ] 手动删除注册表项后重启应用，验证被修复
- [ ] 若可行，在注册表权限不足环境下测试
- [ ] 验证 Windows 开机后应用确实会启动

## 3. 文档

- [ ] 在 `openspec/docs/system-configuration.md` 中补充开机自启说明
- [ ] 添加说明注册表操作的代码注释
- [ ] 记录错误处理策略
- [ ] 编写注册表权限问题的排查指南

## 4. 验证

- [ ] 执行 `openspec validate implement-windows-auto-start --strict`
- [ ] 确保所有测试通过
- [ ] 确认无破坏性变更
- [ ] 检查新服务的代码覆盖率
