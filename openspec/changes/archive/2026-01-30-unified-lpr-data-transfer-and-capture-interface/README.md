# 统一 LPR 数据传递和主动抓拍接口 - 提案总结

## 快速概览

**变更 ID**: `unified-lpr-data-transfer-and-capture-interface`
**状态**: 草稿(待批准)
**类型**: 重构/功能增强
**创建日期**: 2026-01-29

## 问题陈述

当前 LPR(车牌识别)硬件回调处理程序在 `MinimalWebHostService` 中直接调用 `IAttendedWeighingService.OnPlateNumberRecognized()`。这创建了硬件层和业务逻辑层之间的紧密耦合,使系统更难测试和扩展。

**当前流程**:
```
硬件 → MinimalWebHostService → weighingService.OnPlateNumberRecognized() → 处理
```

## 建议的解决方案

重构为使用 ReactiveUI MessageBus 进行解耦的事件交付,并实现统一的主动抓拍接口:

**新流程**:
```
硬件 → MinimalWebHostService → MessageBus → AttendedWeighingService → 处理
```

## 主要变更

1. **创建统一消息**: `LicensePlateRecognizedMessage` 类,包含车牌号、颜色、设备类型、设备名称和时间戳

2. **重构回调处理程序**: `MinimalWebHostService` 发布 MessageBus 消息而非直接调用服务

3. **添加服务订阅**: `AttendedWeighingService` 在构造函数中订阅 `LicensePlateRecognizedMessage`

4. **简化接口**: 从 `IAttendedWeighingService` 中移除 `OnPlateNumberRecognized()`(变为私有)

5. **确保清理**: 正确释放订阅以防止内存泄漏

6. **实现主动抓拍接口**: 定义 `ILprDevice` 接口,提供统一的主动抓拍能力
   - `HikvisionLprService`: 实现基于 `NET_DVR_ContinuousShoot` 的主动抓拍
   - `LprAllInOneService`: 适配现有实现
   - `HuaxiazhixinLprService`: 占位实现,标记不支持

## 收益

- ✅ **松耦合**: 硬件层不再依赖业务服务
- ✅ **可测试性**: 可独立测试回调和业务逻辑
- ✅ **可扩展性**: 易于添加新订阅者(日志、监控、报警)
- ✅ **一致性**: 与 ADR-009(MessageBus 用于跨组件通信)一致
- ✅ **性能**: MessageBus 添加 <1ms 延迟(对于每分钟 10-20 次 LPR 事件可忽略)
- ✅ **功能完整**: 海康威视设备支持主动抓拍

## 创建的文件

- `openspec/changes/unified-lpr-data-transfer-and-capture-interface/proposal.md` - 原因、内容、影响
- `openspec/changes/unified-lpr-data-transfer-and-capture-interface/tasks.md` - 28 个实施任务
- `openspec/changes/unified-lpr-data-transfer-and-capture-interface/design.md` - 技术架构和设计决策
- `openspec/changes/unified-lpr-data-transfer-and-capture-interface/specs/license-plate-recognition/spec.md` - 修改的需求
- `openspec/changes/unified-lpr-data-transfer-and-capture-interface/README.md` - 本总结

## 验证

```bash
openspec validate unified-lpr-data-transfer-and-capture-interface --strict
```

结果: ✅ **有效**

## 下一步

1. **审查提案**: 阅读 `proposal.md` 和 `design.md` 了解完整细节
2. **批准或请求变更**: 对方法提供反馈
3. **实施**: 按照 `tasks.md` 中的任务顺序执行
4. **测试**: 运行单元、集成和内存泄漏测试
5. **部署**: 监控变更后的系统

## 预计工作量

- **总任务数**: 28
- **预计工期**: 1-2 周
- **风险级别**: 中(需要仔细测试所有 LPR 设备类型)

## 与其他提案的关系

本提案包含并扩展了之前创建的 `unify-lpr-events-with-messagebus` 提案:

- `unify-lpr-events-with-messagebus`: 专注于使用 MessageBus 统一 LPR 事件传递
- `unified-lpr-data-transfer-and-capture-interface`: 额外实现统一的主动抓拍接口

## 参考

- ADR-009: MessageBus 用于跨组件通信
- `openspec/docs/timer-to-rx-pattern.md` - 响应式编程模式
- 相关: `hikvision-lpr-implementation`, `hikvision-lpr-integration`
