# 使用 MessageBus 统一 LPR 事件 - 提案摘要

## 简要概述

**变更 ID**：`unify-lpr-events-with-messagebus`
**状态**：草稿（待批准）
**类型**：重构
**创建日期**：2026-01-29

## 问题陈述

当前 LPR（车牌识别）硬件回调在 `MinimalWebHostService` 中直接调用 `IAttendedWeighingService.OnPlateNumberRecognized()`，导致硬件层与业务逻辑层紧耦合，系统难以测试和扩展。

**当前流程**：
```
硬件 → MinimalWebHostService → weighingService.OnPlateNumberRecognized() → 处理
```

## 拟议方案

重构为使用 ReactiveUI MessageBus 进行解耦事件投递：

**新流程**：
```
硬件 → MinimalWebHostService → MessageBus → AttendedWeighingService → 处理
```

## 主要变更

1. **创建统一消息**：`LicensePlateRecognizedMessage` 类，包含车牌号、颜色、设备类型、设备名称与时间戳

2. **重构回调处理**：`MinimalWebHostService` 改为发布 MessageBus 消息，不再直接调用服务

3. **增加服务订阅**：`AttendedWeighingService` 在构造函数中订阅 `LicensePlateRecognizedMessage`

4. **简化接口**：从 `IAttendedWeighingService` 中移除 `OnPlateNumberRecognized()`（改为 private）

5. **确保清理**：正确释放订阅以防内存泄漏

## 收益

- ✅ **松耦合**：硬件层不再依赖业务服务
- ✅ **可测试**：可分别测试回调与业务逻辑
- ✅ **可扩展**：易于新增订阅方（日志、监控、告警）
- ✅ **一致**：与 ADR-009（跨组件通信使用 MessageBus）一致
- ✅ **性能**：MessageBus 增加 <1ms 延迟（对 10–20 次/分钟 LPR 可忽略）

## 创建的文件

- `openspec/changes/unify-lpr-events-with-messagebus/proposal.md` —— 原因、内容、影响
- `openspec/changes/unify-lpr-events-with-messagebus/tasks.md` —— 15 项实施任务
- `openspec/changes/unify-lpr-events-with-messagebus/design.md` —— 技术架构与设计决策
- `openspec/changes/unify-lpr-events-with-messagebus/specs/license-plate-recognition/spec.md` —— 修改后的需求
- `openspec/changes/unify-lpr-events-with-messagebus/README.md` —— 本摘要

## 验证

```bash
openspec validate unify-lpr-events-with-messagebus --strict
```

结果：✅ **有效**

## 后续步骤

1. **评审提案**：阅读 `proposal.md` 与 `design.md` 了解完整细节
2. **批准或提出修改**：对方案给出反馈
3. **实施**：按 `tasks.md` 顺序执行任务
4. **测试**：运行单元、集成与内存泄漏测试
5. **部署**：变更后监控系统

## 预估工作量

- **任务总数**：15
- **预估周期**：3–4 天
- **风险等级**：中（需对所有 LPR 设备类型做细致测试）

## 参考

- ADR-009：跨组件通信使用 MessageBus
- `openspec/docs/timer-to-rx-pattern.md` —— 响应式编程模式
- 相关：`hikvision-lpr-implementation`、`hikvision-lpr-integration`
