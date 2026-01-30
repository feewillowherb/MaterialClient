# 提案更新日志 - 会话管理优化

**日期**: 2026-01-29
**提案 ID**: `unified-lpr-data-transfer-and-capture-interface`
**更新类型**: 设计优化

---

## 更新概述

根据代码审查反馈,更新了海康威视 LPR 主动抓拍实现的设计,加入会话管理机制以避免频繁重复的设备登录/登出操作。

---

## 问题背景

原始设计中,每次调用 `TriggerCaptureAsync()` 都会:
1. 登录设备 (`NET_DVR_Login_V40`)
2. 触发抓拍 (`NET_DVR_ContinuousShoot`)
3. 等待结果
4. 登出设备 (`NET_DVR_Logout`)

这种设计存在以下问题:
- **性能开销**: 每次抓拍都需要完整的登录/登出流程,增加延迟
- **资源浪费**: 频繁的登录/登出操作消耗网络和设备资源
- **不符合现有模式**: `HikvisionService` 已经实现了会话缓存机制(`EnsureLogin()`)

---

## 解决方案

参考 `HikvisionService.EnsureLogin()` 的成熟实现,使用 `ConcurrentDictionary` 缓存登录会话:

### 核心设计

```csharp
private readonly ConcurrentDictionary<string, int> _deviceKeyToUserId = new();

public IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(
    LicensePlateRecognitionConfig config)
{
    return Observable.Create<LicensePlateRecognizedEvent>(observer =>
    {
        // 确保登录(使用会话缓存)
        var key = BuildDeviceKey(config);
        var userId = _deviceKeyToUserId.AddOrUpdate(
            key,
            _ => LoginDevice(config),           // 首次登录
            (_, existingUserId) => existingUserId >= 0
                ? existingUserId                 // 复用现有会话
                : LoginDevice(config));          // 会话失效,重新登录

        if (userId < 0)
        {
            observer.OnError(new Exception($"设备登录失败: {config.Name}"));
            return Disposable.Empty;
        }

        // 使用缓存的 userId 触发抓拍
        var result = HikvisionSdk.NET_DVR_ContinuousShoot(userId, ref snapCfg, 1);

        // ... 订阅结果 ...

        // 返回清理函数(不登出设备)
        return Disposable.Create(() =>
        {
            subscription?.Dispose();
            // 注意: 不调用 NET_DVR_Logout,保持会话
        });
    });
}
```

### 关键特性

1. **会话复用**:
   - 首次调用时登录设备
   - 后续调用复用现有 userId
   - 仅在会话失效时重新登录

2. **避免登出**:
   - 清理函数不调用 `NET_DVR_Logout`
   - 保持会话供后续抓拍复用
   - 会话在服务停止时统一清理

3. **线程安全**:
   - 使用 `ConcurrentDictionary` 保证线程安全
   - `AddOrUpdate` 原子操作

---

## 更新的文件

### 1. proposal.md

**位置**: 第 113-119 行

**变更**: 更新 `HikvisionLprService 实现` 部分
- 添加会话管理说明
- 明确参考 `HikvisionService.EnsureLogin()` 设计
- 说明资源管理策略(不主动登出)

### 2. design.md

**位置**: 第 361-477 行

**变更**:
- 添加 `_deviceKeyToUserId` 字段定义
- 更新 `TriggerCaptureAsync()` 实现代码示例
- 添加 `LoginDevice()` 和 `BuildDeviceKey()` 辅助方法
- 更新"关键点"部分,详细说明会话管理机制

### 3. tasks.md

**位置**: 第 271-306 行

**变更**: 更新任务 4.2 的实施步骤
- 添加会话缓存字段和辅助方法的实现步骤
- 明确资源管理策略(不登出设备)
- 更新验收标准,包含会话复用测试

---

## 收益

1. **性能提升**: 避免每次抓拍的登录开销,降低延迟
2. **资源节约**: 减少网络请求和设备负载
3. **一致性**: 与现有 `HikvisionService` 设计保持一致
4. **可靠性**: 成熟的会话管理模式,已验证可用

---

## 向后兼容性

此更新不破坏现有接口或API,仅优化内部实现:
- `ILprDevice` 接口保持不变
- `TriggerCaptureAsync()` 方法签名不变
- 仅优化内部登录会话管理逻辑

---

## 验证状态

```bash
openspec validate unified-lpr-data-transfer-and-capture-interface --strict
```

结果: ✅ **有效**

---

## 相关参考

- `MaterialClient.Common/Services/Hikvision/HikvisionService.cs:706-716` - `EnsureLogin()` 实现
- `MaterialClient.Common/Services/Hikvision/HikvisionLprService.cs:506-522` - 现有登录逻辑
- `MaterialClient.Common/Services/Hikvision/HikvisionLprService.cs:42` - `deviceKeyToUserId` 字段参考
