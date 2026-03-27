## Why

车牌误识别（广告牌/路人等）后系统创建道闸会话并开闸，但无车辆进入。保安手动关闸后系统无感知，会话永久阻塞，导致下一辆车的车牌识别被拒绝。当前唯一恢复方式是重启服务。

需要在检测到"幽灵会话"（会话激活但车辆从未上磅）时，当新车牌到来时自动重置会话并恢复正常流程。

## What Changes

- `GateIoSession` 新增 `PlateNumber` 字段，会话创建时记录关联的车牌号
- 在 LRP 车牌识别处理中，当会话已激活但车牌不同且从未上磅时，自动重置幽灵会话并让新车牌走正常开闸流程
- 将幽灵会话检测逻辑从 `HandlePlateRecognizedAsync` 中抽离为独立的私有方法，保持单一职责

## Capabilities

### New Capabilities

_(无新增能力)_

### Modified Capabilities

- `gate-io-session-management`: 新增幽灵会话自动检测与重置行为，修改会话期间拒绝重复触发的逻辑（增加车牌比对和幽灵会话重置分支），新增 `PlateNumber` 字段

## Impact

- **代码变更**: `MaterialClient.Common/Services/GateIoControlService.cs` — `GateIoSession` 类、`HandlePlateRecognizedAsync` 方法、新增幽灵会话检测方法
- **Spec 变更**: `openspec/specs/gate-io-session-management/spec.md` — 新增幽灵会话检测与重置的 REQUIREMENTS
- **行为变更**: 会话激活期间不再无条件拒绝所有 LRP 触发，而是根据车牌和称重状态判断是否为幽灵会话
- **风险**: 低 — 幽灵会话判断条件（车牌不同 + 出口未开 + 称重状态为离秤）与正常上磅流程互斥，不会误重置
