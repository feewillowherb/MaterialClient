## Context

`GateIoControlService` 通过 LRP 车牌识别事件创建道闸会话，会话在称重状态变为 `OffScale` 时清理。当车牌误识别导致车辆未上磅时，会话永远无法被清理（称重状态无变化），阻塞后续车辆通行。

当前 `HandlePlateRecognizedAsync` 方法中，会话激活时无条件拒绝所有 LRP 触发。需要在保持该方法职责清晰的前提下，增加幽灵会话检测与自动重置能力。

## Goals / Non-Goals

**Goals:**

- 当检测到幽灵会话（会话激活但车辆从未上磅）时，新车牌到来能自动重置并恢复正常流程
- 通过车牌比对区分同一车辆重复识别和不同车辆到达
- 将幽灵会话检测逻辑抽离为独立方法，保持 `HandlePlateRecognizedAsync` 单一职责
- 在 `GateIoSession` 中记录车牌号，支持车牌比对

**Non-Goals:**

- 不实现超时机制（方案一，另案处理）
- 不实现手动重置接口（方案二，另案处理）
- 不读取道闸物理状态（方案三，长期方案）
- 不改变会话清理的主逻辑（仍由 `OnStatusChanged` → `OffScale` → `ClearSession` 触发）

## Decisions

### 决策一: 幽灵会话检测逻辑抽离为独立方法

**选择**: 新增 `TryResetGhostSession` 私有方法，负责幽灵会话判断和重置。

**替代方案**:
- A) 直接在 `HandlePlateRecognizedAsync` 中内联所有判断逻辑
- B) 使用策略模式/状态机

**理由**: `HandlePlateRecognizedAsync` 已包含配置查找、状态门控、会话管理、开闸调用等多个职责。方案 A 会让方法进一步膨胀。方案 B 对当前只有一种检测条件的场景过度设计。独立方法是最平衡的选择——调用方只需关心返回值（是否重置了幽灵会话），检测细节封装在内部。

```
// 调用方式（伪代码）
if (_session.SessionActive)
{
    if (TryResetGhostSession(message.PlateNumber, message.DeviceName))
    {
        // 幽灵会话已重置，继续创建新会话
    }
    else
    {
        // 正常拒绝
        return;
    }
}
```

### 决策二: 幽灵会话的判断条件

**三个条件必须同时满足**:

| 条件 | 字段 | 说明 |
|------|------|------|
| 车牌不同 | `message.PlateNumber != _session.PlateNumber` | 排除同一车辆在闸口等待时的重复识别 |
| 出口未开 | `_session.ExitOpened == false` | 会话未完成称重流程 |
| 称重状态为离秤 | `_currentWeighingStatus == OffScale` | 车辆从未上磅 |

**与正常上磅流程的互斥性**: 车辆上磅后称重状态一定变为 `WaitingForStability`，不满足第三个条件。即使手动开闸下车导致异常退出，`ClearSession()` 也会在状态回到 `OffScale` 时正常清理会话，此时 `SessionActive` 已为 `false`，不会进入检测分支。

### 决策三: `GateIoSession` 新增 `PlateNumber` 字段

**选择**: 在 `GateIoSession` 中新增 `PlateNumber` 属性。

**替代方案**: 在 `HandlePlateRecognizedAsync` 中使用局部变量记录上次车牌号。

**理由**: 车牌号与会话生命周期绑定（会话创建时记录，会话重置时清空），属于会话状态的一部分，放在 `GateIoSession` 中更合理。`GetStatus()` 方法也会包含车牌信息，便于日志排查。

## Risks / Trade-offs

**[同一车牌多次识别被误跳过]** → 不会发生。同一车牌在会话激活期间被识别时，直接 `return` 跳过是正确行为——车辆已在闸口等待上磅，无需重复开闸。

**[正常上磅车辆被误重置]** → 不会发生。车辆上磅后 `_currentWeighingStatus` 一定不再是 `OffScale`，不满足幽灵会话条件。

**[手动开闸下车的车辆被误重置]** → 不会发生。手动开闸下车的车辆已经上过磅，称重状态已变化，`ClearSession()` 会正常清理会话，`SessionActive` 已为 `false`。
