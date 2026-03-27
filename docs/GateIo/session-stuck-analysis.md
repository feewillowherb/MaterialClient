# 道闸会话阻塞问题分析报告

> **文件**: `MaterialClient.Common/Services/GateIoControlService.cs`
> **日期**: 2026-03-27
> **状态**: 待修复

## 问题描述

**场景**: 车牌误识别 → 车辆未上磅 → 用户手动关闭道闸 → 下一辆车到达

**后果**: 下一辆车的道闸无法自动打开，车牌识别被系统拒绝。

---

## 根因分析

### 正常流程 vs 异常流程对比

```
正常流程:
  LPR识别 → 创建会话(SessionActive=true) → 开闸 → 车辆上磅
    → 状态变化(OffScale → WaitingForStability → ... → WaitingForDeparture)
    → 开出口闸 → 车辆下磅 → 状态变为OffScale → 清理会话(SessionActive=false)

异常流程(本次分析):
  LPR误识别 → 创建会话(SessionActive=true) → 开闸 → 车辆未上磅(离开)
    → 称重状态始终为OffScale(无状态变化) → 会话永远不会被清理
    → 用户手动关闸(系统无感知) → 下一辆车LPR识别 → 被会话阻塞拒绝
```

### 代码级分析

#### 1. 会话创建 — `HandlePlateRecognizedAsync` (第243-260行)

车牌识别通过所有校验后，创建新会话:

```csharp
// GateIoControlService.cs 第243-260行
lock (_sync)
{
    if (_session.SessionActive)
    {
        // 会话已激活，直接拒绝 — 这是问题所在
        return;
    }

    // 创建新会话
    _session.SessionActive = true;
    _session.EntrySide = config.Direction;
    _session.ExitOpened = false;
    _session.SessionStartedAt = DateTime.UtcNow;
}
```

#### 2. 会话清理的唯一入口 — `OnStatusChanged` (第330-336行)

会话清理**仅**在称重状态**变化为** `OffScale` 时触发:

```csharp
// GateIoControlService.cs 第330-336行
switch (newStatus)
{
    case AttendedWeighingStatus.OffScale:
        ClearSession();
        break;
    // ...
}
```

#### 3. 问题核心

| 条件 | 说明 |
|------|------|
| 称重状态始终为 `OffScale` | 车辆未上磅，没有触发任何状态变化 |
| `OnStatusChanged` 不会触发 | 只在状态**变化**时通过 MessageBus 推送 |
| 即使推送 `OffScale`，`switch` 也匹配 | 但实际上状态根本没变化，不会收到事件 |
| 用户手动关闸无感知 | 代码中没有任何机制感知道闸物理状态 |
| `_session.SessionActive` 保持 `true` | 会话永久阻塞 |

---

## 状态机分析

称重状态枚举 (`AttendedWeighingStatus`):

```
OffScale (0) ──→ WaitingForStability (1) ──→ WeightStabilized (2) ──→ WaitingForDeparture (3) ──→ OffScale (0)
  ↑                                                                                                    │
  └────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- 会话清理只发生在 `→ OffScale` 这条转换路径上
- 如果状态从未离开 `OffScale`，清理逻辑永远不会执行

---

## 影响评估

| 影响维度 | 严重程度 | 说明 |
|----------|----------|------|
| 通行效率 | 高 | 下一辆车必须人工干预，无法自动通行 |
| 用户体验 | 高 | 需要重启服务或人工干预才能恢复 |
| 发生概率 | 中 | 依赖车牌误识别概率，在夜间或恶劣天气更易发生 |
| 恢复难度 | 中 | 需要重启 `GateIoControlService` 或整个应用 |

---

## 建议修复方案

### 方案一: 会话超时机制 (推荐)

在 `HandlePlateRecognizedAsync` 中检查会话时长，超时则自动清理:

```csharp
lock (_sync)
{
    if (_session.SessionActive)
    {
        var duration = DateTime.UtcNow - _session.SessionStartedAt;
        if (duration > _sessionTimeout) // 例如 3 分钟
        {
            _logger?.LogWarning("道闸会话超时，强制清理: Duration={Duration}", duration);
            _session.Reset();
        }
        else
        {
            return; // 会话未超时，正常拒绝
        }
    }
    // 创建新会话...
}
```

**优点**: 实现简单，自动恢复，无需用户干预
**缺点**: 需要合理设置超时时间

### 方案二: 手动重置接口

在 `IGateIoControlService` 中添加 `ResetSession()` 方法，供 UI 层调用:

```csharp
public interface IGateIoControlService
{
    Task StartAsync();
    Task StopAsync();
    void ResetSession(); // 新增
}
```

**优点**: 用户可控，立即生效
**缺点**: 需要用户知道问题并主动操作

### 方案三: 道闸状态反馈感知 (长期方案)

通过 LRP SDK 读取道闸物理状态，在检测到道闸已关闭且无称重活动时自动清理会话。

**优点**: 最精确，从根本上解决问题
**缺点**: 依赖硬件支持，开发周期长

### 方案四: 新车牌触发 Session 重置 (即时恢复)

在 `HandlePlateRecognizedAsync` 中，当检测到会话已激活但**从未触发过上磅流程**且**车牌不同**时，视为"幽灵会话"，直接重置并让新车牌走正常开闸流程:

#### 前置修改: `GateIoSession` 新增 `PlateNumber` 字段

```csharp
private sealed class GateIoSession
{
    public bool SessionActive { get; set; }
    public LicensePlateDirection? EntrySide { get; set; }
    public bool ExitOpened { get; set; }
    public DateTime SessionStartedAt { get; set; }
    public string PlateNumber { get; set; } = string.Empty; // 新增: 记录会话关联的车牌号

    public void Reset()
    {
        SessionActive = false;
        EntrySide = null;
        ExitOpened = false;
        SessionStartedAt = DateTime.MinValue;
        PlateNumber = string.Empty; // 新增
    }
    // ...
}
```

#### 核心逻辑

```csharp
// GateIoControlService.cs HandlePlateRecognizedAsync 中
lock (_sync)
{
    if (_session.SessionActive)
    {
        var isNewPlate = !string.Equals(message.PlateNumber, _session.PlateNumber, StringComparison.OrdinalIgnoreCase);

        // 同一车牌重复识别(车辆在闸口但未上磅): 正常跳过，不做任何处理
        if (!isNewPlate)
        {
            _logger?.LogDebug("同一车牌重复识别，跳过: Plate={Plate}, SessionStatus={SessionStatus}",
                message.PlateNumber, _session.GetStatus());
            return;
        }

        // 不同车牌 + 从未上磅(幽灵会话): 自动重置
        if (!_session.ExitOpened && _currentWeighingStatus == AttendedWeighingStatus.OffScale)
        {
            _logger?.LogWarning(
                "检测到幽灵会话(从未上磅)，新车牌触发重置: " +
                "OldPlate={OldPlate}, OldEntrySide={OldEntrySide}, OldDuration={OldDuration}, " +
                "NewPlate={NewPlate}, NewDevice={NewDevice}",
                _session.PlateNumber, _session.EntrySide,
                DateTime.UtcNow - _session.SessionStartedAt,
                message.PlateNumber, message.DeviceName);
            _session.Reset();
            // 不 return，继续往下走创建新会话
        }
        else
        {
            _logger?.LogInformation("道闸会话已激活且正在处理中，拒绝新车牌: Device={Device}, SessionPlate={SessionPlate}, NewPlate={NewPlate}",
                message.DeviceName, _session.PlateNumber, message.PlateNumber);
            return;
        }
    }

    // 创建新会话
    _session.SessionActive = true;
    _session.EntrySide = config.Direction;
    _session.ExitOpened = false;
    _session.SessionStartedAt = DateTime.UtcNow;
    _session.PlateNumber = message.PlateNumber; // 新增: 记录车牌号
}
```

#### 判断逻辑说明

```
SessionActive == true
  │
  ├─ 新车牌 == 旧车牌 (同一车牌重复识别)
  │    └─ 直接 return → 正常跳过，不做任何处理
  │       (车辆在闸口但还没上磅，LRP 会持续识别同一车牌)
  │
  └─ 新车牌 != 旧车牌 (不同车辆)
       │
       ├─ ExitOpened == false && WeighingStatus == OffScale
       │    └─ 幽灵会话 → 重置 → 创建新会话 → 开闸
       │       (上一辆车误识别后未进入，会话卡住)
       │
       └─ 其他情况
            └─ 正常拒绝 → 会话正在处理中
               (上一辆车已上磅/正在称重，不能打断)
```

**三个判断条件**:
- `车牌不同`: 排除同一车辆在闸口等待时的重复识别，避免误重置正常流程
- `ExitOpened == false`: 出口闸未打开，说明当前会话未完成称重流程
- `_currentWeighingStatus == OffScale`: 称重状态未变化，说明车辆从未上磅
- 三个条件同时满足 → 确认是"幽灵会话"(车牌误识别/车辆未进入)

**优点**: 即时恢复，下一辆车到达即可自动恢复，无需等待超时或人工干预；车牌比对确保不会误重置正在等待上磅的正常车辆
**缺点**: 无明显缺点

#### 边界场景验证: 车辆上磅后手动开闸下车

**场景**: 车辆已上磅 → 称重未稳定 → 用户手动开出口闸下车 → `ExitOpened` 仍为 `false`

```
LPR识别车牌A → 创建会话 → 开入口闸 → 车辆上磅
  → 称重状态: OffScale → WaitingForStability (已变化!)
  → 用户等不及，手动开出口闸下车 (系统无感知)
  → 车辆下磅 → 称重状态: WaitingForStability → OffScale
  → OnStatusChanged 匹配 OffScale → ClearSession() → 会话正常清理 ✓
```

**方案四在此场景下不受影响**，原因:

| 条件 | 实际值 | 说明 |
|------|--------|------|
| `ExitOpened == false` | true | 出口闸从未被系统打开 |
| `_currentWeighingStatus == OffScale` | true | 最终回到 OffScale |

虽然两个条件同时满足，但**会话已被正常清理**: 车辆上磅后称重状态经历了 `OffScale → WaitingForStability → OffScale` 的变化，最终回到 `OffScale` 时 `OnStatusChanged`（第332-334行）会触发 `ClearSession()`。会话清理后 `_session.SessionActive == false`，下一辆车直接走正常创建会话流程，**不会进入方案四的重置分支**。

**结论**: 方案四的重置条件(`ExitOpened == false && WeighingStatus == OffScale`)只在**会话仍然激活**的前提下才判断。车辆上过磅的会话会由 `ClearSession()` 正常清理，与方案四的触发路径互斥。

### 建议组合: 方案一 + 方案二 + 方案四

| 方案 | 角色 | 说明 |
|------|------|------|
| 方案四 | **第一道防线** (即时恢复) | 新车牌到达时自动检测并清除幽灵会话，无需等待 |
| 方案一 | **第二道防线** (兜底) | 如果长时间无新车牌到达，超时机制兜底清理 |
| 方案二 | **第三道防线** (人工干预) | 兜底手段，极端情况下运维人员可手动重置 |

---

## 相关文件

| 文件 | 说明 |
|------|------|
| `MaterialClient.Common/Services/GateIoControlService.cs` | 道闸 I/O 控制服务主逻辑 |
| `MaterialClient.Common/Entities/Enums/AttendedWeighingStatus.cs` | 称重状态枚举定义 |
| `MaterialClient.Common/Entities/Enums/LicensePlateDirection.cs` | 车牌识别设备物理侧别枚举 |
