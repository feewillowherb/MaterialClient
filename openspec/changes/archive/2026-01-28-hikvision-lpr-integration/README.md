# 新增海康威视LPR设备支持

**Change ID**: `hikvision-lpr-integration`
**Status**: ✅ Valid - Ready for Review
**Created**: 2026-01-28

---

## 快速概览

本提案扩展 MaterialClient 的车牌识别功能,支持海康威视(Hikvision)LPR 设备的配置和UI集成。

### 核心变更

1. **配置模型扩展** - 为 `LicensePlateRecognitionConfig` 添加海康威视专用字段(UserName、Password、Port、Channel)
2. **数据库迁移** - 创建 EF Core 迁移添加新列
3. **动态UI实现** - SettingsWindow 和 AddLprDialog 根据设备类型动态显示字段
4. **条件显示逻辑** - 基于枚举值 `LprDeviceType == Hikvision` 控制字段可见性

### 范围说明

**✅ 包含**:
- 配置模型和数据库架构
- 动态UI字段显示
- 设备类型条件判断逻辑
- 配置保存和加载

**❌ 不包含**:
- 海康威示LPR监听服务实现(需单独提案)
- HCNetSDK LPR组件集成
- 车牌识别事件流设计

---

## 文档结构

```
openspec/changes/hikvision-lpr-integration/
├── README.md                              # 本文件
├── proposal.md                            # 提案主文档(为什么、改什么、影响)
├── design.md                              # 技术设计文档(架构、数据流、安全)
├── tasks.md                               # 实施任务清单(21个任务)
└── specs/
    └── license-plate-recognition/
        └── spec.md                        # 规范增量(新增/修改需求)
```

---

## 实施阶段

### 阶段1: 配置模型扩展(3个任务)
- Task 1.1: 扩展 LicensePlateRecognitionConfig 实体类
- Task 1.2: 创建数据库迁移
- Task 1.3: 更新 LicensePlateRecognitionConfigViewModel

### 阶段2: 动态UI实现(6个任务)
- Task 2.1: 更新 SettingsWindowViewModel 加载逻辑
- Task 2.2: 更新 SettingsWindowViewModel 保存逻辑
- Task 2.3: 添加设备类型判断属性
- Task 2.4: 更新 AddLprDialogViewModel
- Task 2.5: 添加 LprDeviceType 参数到对话框
- Task 2.6: 更新 EditLprAsync 命令
- Task 2.7: 更新 SettingsWindow.axaml
- Task 2.8: 更新 AddLprDialog.axaml

### 阶段3: 测试和验证(7个任务)
- Task 3.1: 编写配置模型单元测试
- Task 3.2: 编写 ViewModel 单元测试
- Task 3.3: 编写集成测试
- Task 3.4: UI 手动测试
- Task 3.5: 数据库迁移验证
- Task 3.6: 代码审查和重构
- Task 3.7: 更新文档

**总计**: 21个任务 | 预计工期: 3-4天

---

## 验证状态

```bash
$ openspec validate hikvision-lpr-integration --strict
✅ Change 'hikvision-lpr-integration' is valid
```

所有 OpenSpec 验证规则已通过:
- ✅ proposal.md 格式正确
- ✅ tasks.md 包含可执行的任务清单
- ✅ design.md 包含完整的架构设计
- ✅ spec.md 包含规范增量(ADDED/MODIFIED/REMOVED)
- ✅ 所有需求包含至少一个 Scenario

---

## 关键设计决策

### 1. 可空字段设计
**决策**: 新增的海康威示字段使用可空类型(`string?`)

**理由**:
- 向后兼容现有 LPRAllInOne 配置
- 避免强制用户填写不需要的字段
- 与现有 `CameraConfig` 模式保持一致

### 2. Channel 字段固定值
**决策**: Channel 字段在 UI 中显示为只读,固定值 "1"

**理由**:
- 根据需求,通道号固定为 1
- 避免用户输入错误导致连接失败
- 简化配置流程

### 3. 条件显示而非禁用
**决策**: 非海康威示设备完全隐藏字段,而非禁用

**理由**:
- 减少UI混乱,仅显示相关字段
- 避免用户误填不适用字段
- 符合"渐进式披露"设计原则

### 4. 密码明文存储
**决策**: 保持密码明文存储,与现有 `CameraConfig.Password` 一致

**理由**:
- 避免引入新的安全机制复杂性
- 保持项目安全模型一致性
- 未来可统一改进所有密码存储

---

## 下一步行动

### 审批前
1. **技术评审**: 评审配置模型设计和UI方案
2. **风险评估**: 确认向后兼容性和数据迁移策略
3. **资源评估**: 确认开发时间和测试资源

### 审批后
1. **启动实施**: 按照 tasks.md 顺序执行任务
2. **监听服务设计**: 并行启动海康威示LPR监听服务的技术评审
3. **进度跟踪**: 使用 OpenSpec 任务跟踪功能

### 完成后
1. **归档提案**: 将变更移动到 `openspec/changes/archive/`
2. **更新规范**: 合并 spec.md 到 `openspec/specs/license-plate-recognition/spec.md`
3. **文档同步**: 更新 SDD.md 和项目文档

---

## 相关文档

- `openspec/project.md` - 项目技术栈和架构约定
- `openspec/specs/attended-weighing/spec.md` - 现有功能规范
- `openspec/AGENTS.md` - OpenSpec 工作流程指南
- `MaterialClient.Common/Configuration/LicensePlateRecognitionConfig.cs` - 当前配置模型
- `MaterialClient.Common/HCNetSDK/README.md` - 海康威示SDK文档

---

## 常见问题

### Q: 为什么不包含监听服务实现?
**A**: 监听服务涉及复杂的架构决策(SDK集成方式、事件流设计、生命周期管理),需要单独的技术评审和设计文档。本提案聚焦于配置和UI,为后续服务实现奠定基础。

### Q: 现有 LPRAllInOne 配置会受影响吗?
**A**: 不会。新增字段为可空类型,现有配置保持不变,`IsValid()` 方法不验证新字段。

### Q: 如果用户忘记填写 UserName 或 Password 会怎样?
**A**: 配置仍然可以保存,但在后续连接海康威示设备时会失败。未来的"测试连接"功能可以提前发现此问题。

### Q: 华夏智信(Huaxiazhixin)设备支持吗?
**A**: 不支持。`Huaxiazhixin` 枚举值已存在,但本提案不实现其配置字段。需要单独提案定义其配置需求。

### Q: 数据库迁移会失败吗?
**A**: 风险较低。迁移仅添加可空列,不修改现有数据。建议在测试环境验证后再应用到生产环境。

---

## 联系方式

如有疑问或需要澄清,请联系:
- **提案负责人**: [待填写]
- **技术评审人**: [待填写]
- **文档路径**: `openspec/changes/hikvision-lpr-integration/`
