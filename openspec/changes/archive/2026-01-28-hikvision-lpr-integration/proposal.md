# Change: 新增海康威视LPR设备支持

**Change ID**: `hikvision-lpr-integration`
**Status**: ExecutionCompleted
**Created**: 2026-01-28
**Completed**: 2026-01-28
**Type**: Feature

---

## Why

### Background

MaterialClient 当前使用 LPRAllInOne SDK 进行车牌识别服务集成。系统需要扩展支持海康威视(Hikvision)车牌识别设备,以满足不同场景的硬件需求。现有架构已集成 HCNetSDK 用于海康威视摄像头功能,可在此基础上实现海康威视 LPR 功能。

`LprDeviceType` 枚举已定义 `Hikvision = 0`,且 `SystemSettings.LprDeviceType` 默认值为 `Hikvision`,但系统尚未实现海康威视 LPR 设备的配置和集成。

### Problems

1. **配置模型不完整** - 当前 `LicensePlateRecognitionConfig` 配置类缺少海康威视设备所需的认证和连接参数(用户名、密码、端口、通道)
2. **设备类型区分缺失** - 系统未区分不同 LPR 设备类型的配置需求,所有设备共享相同配置结构
3. **UI 界面固化** - `SettingsWindow` 设置窗口未实现基于设备类型的动态字段显示,导致海康威视特定参数无法配置
4. **监听服务未定义** - 海康威视 LPR 设备的事件监听和服务集成架构尚未确定

---

## What Changes

### Overview

扩展 MaterialClient 的车牌识别功能,支持海康威视 LPR 设备。通过扩展配置模型、实现动态 UI 和设计监听服务架构,使系统能够根据 `SystemSettings.LprDeviceType` 动态适配不同的 LPR 设备类型。

### Detailed Changes

#### 1. 扩展配置模型

修改 `LicensePlateRecognitionConfig` 实体类,新增海康威视专用字段:
- `UserName` (string?) - 设备认证用户名
- `Password` (string?) - 设备认证密码
- `Port` (string?) - 设备服务端口号
- `Channel` (string?) - 通道号,默认值为 "1"

**条件可见性**: 当 `SystemSettings.LprDeviceType == Hikvision` 时启用这些字段

#### 2. 实现动态 UI

修改 `SettingsWindow` 视图、`SettingsWindowViewModel` 和 `AddLprDialogViewModel`:
- 添加设备类型判断逻辑,基于 `SystemSettings.LprDeviceType` 枚举值
- 仅当选择 `Hikvision` 类型时显示海康威视配置字段组
- `Channel` 字段使用硬编码默认值 "1",不提供 UI 编辑入口
- 保持现有 `LprAllInOne` 配置字段的向后兼容性

#### 3. 定义海康威视 LPR 服务接口(本提案仅定义接口)

在本提案中,我们将定义 `IHikvisionLprService` 接口,为后续的监听服务实现预留扩展点。

**接口定义范围**:
- 定义服务接口 `IHikvisionLprService`
- 声明核心方法签名(如连接、断开连接、开始监听、停止监听)
- 定义车牌识别事件类型

**不在本提案范围内**:
- 具体服务实现
- HCNetSDK LPR 组件集成细节
- ReactiveUI 事件流实现
- 生命周期管理和内存泄漏预防

这些内容将在后续的独立提案中完成。

---

## Impact

### Expected Benefits

- **设备灵活性**: 支持多种 LPR 设备类型,满足不同场景的硬件需求
- **配置完整性**: 提供完整的海康威视设备认证和连接参数配置
- **用户体验**: 动态 UI 根据设备类型自动调整,避免无关字段干扰
- **架构扩展性**: 为未来添加更多 LPR 设备类型(如 `Huaxiazhixin`)奠定基础

### Risks and Mitigations

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 新字段为可空类型可能导致配置不完整 | 中 | 在 UI 层提供默认值提示;旧数据反序列化时自动填充默认值 |
| UI 动态字段逻辑复杂,容易引入 Bug | 高 | 单元测试覆盖设备类型条件判断逻辑;集成测试验证 UI 显示逻辑 |
| JSON 反序列化兼容性问题 | 低 | 新增字段为可空类型;旧数据缺少字段时自动使用 null 或默认值 |
| 海康威视监听服务接口未实现 | 高 | 本提案仅定义接口,具体实现在后续提案中 |
| 向后兼容性问题 | 低 | 新增字段为可空类型,不影响现有 LPRAllInOne 设备配置 |

---

## Success Criteria

- [x] 创建 OpenSpec 提案文档
- [x] 通过 `openspec validate hikvision-lpr-integration --strict` 验证
- [x] `LicensePlateRecognitionConfig` 实体类添加海康威视专用字段
- [x] `SettingsWindowViewModel` 实现基于 `LprDeviceType` 的动态字段逻辑
- [x] `AddLprDialogViewModel` 支持海康威视配置字段
- [x] `SettingsWindow.axaml` 和 `AddLprDialog.axaml` UI 绑定动态字段
- [x] 定义 `IHikvisionLprService` 接口
- [x] 单元测试覆盖设备类型条件判断逻辑
- [x] 集成测试验证海康威视配置的保存和加载(包括旧数据兼容性)
- [ ] UI 测试验证字段显示逻辑(需要手动测试)

---

## Next Steps

1. **技术评审**: 评审本提案,确认配置模型和 UI 设计方案
2. **监听服务架构设计**: 启动海康威视 LPR 监听服务的技术评审和设计文档
3. **实施**: 按照本提案的 `tasks.md` 实施配置和 UI 部分
4. **测试**: 执行单元测试、集成测试和 UI 测试
5. **归档**: 验收通过后归档本提案,更新相关规范文档

---

## References

- `openspec/project.md` - 项目技术栈和架构约定
- `openspec/specs/attended-weighing/spec.md` - 现有功能规范
- `MaterialClient.Common/Entities/Enums/LprDeviceType.cs` - 设备类型枚举定义
- `MaterialClient.Common/Configuration/LicensePlateRecognitionConfig.cs` - 当前配置模型
- `MaterialClient.Common/Configuration/SystemSettings.cs` - 系统设置
- `MaterialClient/ViewModels/SettingsWindowViewModel.cs` - 设置窗口 ViewModel
- `MaterialClient/ViewModels/AddLprDialogViewModel.cs` - 添加 LPR 对话框 ViewModel
- `MaterialClient.Common/HCNetSDK/` - 海康威视 SDK 集成
