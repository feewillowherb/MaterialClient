# 系统配置 规范

## 目的
待定 - 由变更 implement-windows-auto-start 归档后创建。归档后更新目的。

## 需求

### 需求：Windows 开机自启配置

系统应提供在 Windows 启动时启用或禁用应用程序自动启动的功能，并保持数据库设置与 Windows 注册表同步。

#### 场景：在设置中启用自启
- **当** 用户在设置窗口中勾选“开机自动启动”
- **且** 用户点击“保存”按钮
- **则** 系统应：
  - 将 `EnableAutoStart = true` 保存到数据库
  - 在 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` 下创建注册表项
  - 注册表值名称：应用程序名称（如 "MaterialClient"）
  - 注册表值数据：可执行文件完整路径
  - 记录注册表操作成功日志

#### 场景：在设置中禁用自启
- **当** 用户在设置窗口中取消“开机自动启动”
- **且** 用户点击“保存”按钮
- **则** 系统应：
  - 将 `EnableAutoStart = false` 保存到数据库
  - 从 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` 删除对应注册表项
  - 记录注册表操作成功日志

#### 场景：检查自启状态
- **当** 系统需要确认当前自启状态
- **则** 系统应：
  - 从 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` 读取注册表项
  - 若项存在且与可执行路径一致则返回 `true`
  - 若项不存在或路径不匹配则返回 `false`

### 需求：设置与注册表同步

系统应保持数据库设置与 Windows 注册表状态一致，在检测到不一致时自动修复。

#### 场景：保存设置时同步
- **当** 通过 `SettingsService.SaveSettingsAsync()` 保存设置
- **则** 系统应：
  - 先将设置写入数据库
  - 若 `EnableAutoStart = true`，调用 `WindowsAutoStartService.EnableAutoStartAsync()`
  - 若 `EnableAutoStart = false`，调用 `WindowsAutoStartService.DisableAutoStartAsync()`
  - 确保保存后数据库与注册表状态一致

#### 场景：启动时修复不一致
- **当** 应用程序启动
- **且** 数据库中的 `EnableAutoStart` 与注册表状态不一致
- **则** 系统应：
  - 通过对比数据库设置与注册表状态检测不一致
  - 将数据库设置应用到注册表（修复不一致）
  - 记录修复操作以便排查
  - 正常继续启动（不同步失败时不阻塞启动）

#### 场景：启动时状态一致
- **当** 应用程序启动
- **且** 数据库中的 `EnableAutoStart` 与注册表状态一致
- **则** 系统应：
  - 记录状态一致
  - 不修改注册表继续启动

### 需求：注册表操作的错误处理

系统应妥善处理注册表操作失败，不阻塞应用程序功能。

#### 场景：注册表权限不足
- **当** 因权限不足导致注册表写入失败
- **则** 系统应：
  - 捕获 `UnauthorizedAccessException` 或 `SecurityException`
  - 记录包含异常详情的警告日志
  - 不抛出异常，继续应用流程
  - 允许设置保存完成（即使注册表失败，数据库更新仍可成功）

#### 场景：注册表不可用
- **当** 注册表不可用或损坏
- **则** 系统应：
  - 捕获与注册表相关的异常（`IOException`、`ArgumentException` 等）
  - 记录包含异常详情的错误日志
  - 不抛出异常，继续应用流程
  - 允许应用程序正常启动和运行

#### 场景：注册表读取失败
- **当** 状态检查时读取注册表项失败
- **则** 系统应：
  - 捕获异常并记录警告
  - 以保守默认值返回 `false`（假定自启已禁用）
  - 不抛出异常，继续应用流程

### 需求：Windows 自启服务接口

系统应提供 `IWindowsAutoStartService` 接口，用于管理 Windows 自启功能。

#### 场景：服务注册
- **当** 应用程序初始化依赖注入容器
- **则** 系统应：
  - 将 `WindowsAutoStartService` 注册为 `IWindowsAutoStartService` 的实现
  - 使该服务可被注入到其他服务

#### 场景：服务方法
- **当** 使用 `IWindowsAutoStartService` 时
- **则** 系统应提供：
  - `Task EnableAutoStartAsync()`：在注册表中启用自启
  - `Task DisableAutoStartAsync()`：在注册表中禁用自启
  - `Task<bool> IsAutoStartEnabledAsync()`：检查当前注册表状态
  - 所有方法应为异步并返回相应类型
  - 所有方法应在内部处理异常（不向调用方抛出）
