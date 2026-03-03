# 现有文档清单

**日期**: 2026-01-15
**目的**: 现有设计文档和技术报告的全面清单

---

## 摘要统计

| 类别 | 数量 | 描述 |
|------|------|------|
| **技术报告** | 17 | 性能分析、崩溃报告、优化研究 |
| **功能规范** | 3 | 包含数据模型和需求的功能规范 |
| **架构文档** | 0 | 没有专门的 SDD |
| **文档总数** | 20 | 所有 Markdown 文档 |

---

## 1. 技术报告 (docs/)

### 1.1 响应式扩展与状态管理

#### [AttendedWeighingService-RxState-Optimization-Report.md](./AttendedWeighingService-RxState-Optimization-Report.md)
**日期**: 2025-01-31 | **类型**: 架构提案
**内容**: 提议在 `AttendedWeighingService` 中使用 RxState 模式进行统一状态管理

**关键主题**:
- 当前问题：状态分散（多个 BehaviorSubject）、状态同步问题
- 提议方案：统一状态对象、纯函数归约器、分离副作用
- 代码示例：`WeighingServiceState` 记录、`StateAction` 类型、归约器函数
- 实施计划：3 个阶段（准备、重构、测试）
- 预估工作量：2-3 天

**状态**: **未实施** - 仅提案

---

#### [AttendedWeighingService-Rx-Evaluation-Report.md](./AttendedWeighingService-Rx-Evaluation-Report.md)
**类型**: 技术评估
**内容**: 对代码库中响应式扩展 (Rx.NET) 的评估

**关键主题**:
- Rx.NET 与传统事件驱动编程的对比
- 优点：统一的异步模型、强大的运算符、声明式风格
- 缺点：学习曲线、调试困难、内存泄漏风险
- 建议：在严格指导下使用 Rx

---

#### [TimerToRx.md](./TimerToRx.md)
**类型**: 迁移指南
**内容**: 从基于 Timer 到基于 Rx 模式的迁移指南

---

### 1.2 性能与并发

#### [ReaderWriterLockSlim-Performance-Evaluation.md](./ReaderWriterLockSlim-Performance-Evaluation.md)
**日期**: 2025-12-22 | **类型**: 性能分析
**内容**: 对 `TruckScaleWeightService` 中 `ReaderWriterLockSlim` 使用的全面评估

**关键发现**:
- 当前实现：使用 `readonly struct` 实现零分配（良好）
- 关键问题：串口 I/O 期间持有写锁 8ms（阻塞所有读操作）
- 嵌套锁问题：递归锁导致 15-20% 的性能损失
- 优化建议：5 个优先级（P0-P3）
- 预期改进：优化后读操作快 400,000 倍

**状态**: **未实施** - 建议待定

---

#### [ReaderWriterLockSlim-Performance-Summary.md](./ReaderWriterLockSlim-Performance-Summary.md)
**类型**: 总结报告
**内容**: 性能评估的精简版本

---

### 1.3 崩溃分析与错误修复

#### [Complete-Crash-Fix-Summary.md](./Complete-Crash-Fix-Summary.md)
**类型**: 错误修复总结
**内容**: 崩溃修复和稳定性改进的总结

---

#### [HikvisionOpenStream-Crash-Analysis-Report.md](./HikvisionOpenStream-Crash-Analysis-Report.md)
**类型**: 崩溃分析
**内容**: 海康威视摄像头流打开崩溃的分析

**关键发现**:
- 端口池耗尽问题
- 缺少资源释放
- 提议修复：实现带有释放功能的正确端口池

---

#### [Port-Pool-Integration-Fix.md](./Port-Pool-Integration-Fix.md)
**类型**: 修复文档
**内容**: 海康威视解码器的端口池修复集成

---

#### [内存溢出问题分析报告.md](./内存溢出问题分析报告.md)
**类型**: 错误分析（中文）
**内容**: 内存溢出问题的分析

---

### 1.4 UI 性能优化

#### [AttendedWeighingDetailView-Performance-Optimization.md](./AttendedWeighingDetailView-Performance-Optimization.md)
**日期**: 2025-12-22 | **类型**: 优化报告
**内容**: Avalonia UI 详细视图的性能优化

---

#### [AttendedWeighingDetailView-Code-Changes-2025-12-22.md](./AttendedWeighingDetailView-Code-Changes-2025-12-22.md)
**类型**: 变更日志
**内容**: UI 优化的具体代码变更

---

#### [AttendedWeighingDetailView-Optimization-Summary-2025-12-22.md](./AttendedWeighingDetailView-Optimization-Summary-2025-12-22.md)
**类型**: 总结
**内容**: UI 优化工作的总结

---

#### [AttendedWeighingDetailView-Code-Analysis-2025-12-22.md](./AttendedWeighingDetailView-Code-Analysis-2025-12-22.md)
**类型**: 代码分析
**内容**: 详细视图的详细代码分析

---

### 1.5 硬件集成

#### [hikvision-integration.md](./hikvision-integration.md)
**类型**: 集成指南
**内容**: 海康威视摄像头 SDK 集成指南

---

#### [agents/hikvision-agent-2025-10-30.md](./agents/hikvision-agent-2025-10-30.md)
**类型**: 代理报告
**内容**: AI 代理对海康威视集成的分析

---

#### [agents/TruckScaleWeightService-Optimization-2025-12-22.md](./agents/TruckScaleWeightService-Optimization-2025-12-22.md)
**类型**: 代理报告
**内容**: AI 代理对卡车秤服务的优化建议

---

#### [agents/avalonia-reactiveui-threading-2025-01-31.md](./agents/avalonia-reactiveui-threading-2025-01-31.md)
**类型**: 代理报告
**内容**: Avalonia ReactiveUI 集成中的线程分析

---

## 2. 功能规范 (specs/)

### 2.1 已完成功能

#### [001-attended-weighing/](../specs/001-attended-weighing/)
**状态**: ✅ **完成** (48/48 任务完成)
**目的**: 带有自动匹配功能的人工称重

**文档**:
- `spec.md` - 功能需求和用户故事
- `data-model.md` - 实体修改和新枚举
- `research.md` - 技术研究发现
- `plan.md` - 实施计划
- `quickstart.md` - 快速入门指南
- `tasks.md` - 任务分解（全部完成）
- `contracts/` - API 契约（如有）

**关键实体**:
- `WeighingRecord`（已修改 - 添加了 `WeighingRecordType`）
- `Waybill`（未更改）
- 新枚举：`VehicleWeightStatus`、`DeliveryType`、`WeighingRecordType`

---

#### [001-entity-init/](../specs/001-entity-init/)
**状态**: ✅ **完成** (45/45 任务完成)
**目的**: 核心物料管理实体定义

**文档**:
- `spec.md` - 实体需求
- `data-model.md` - 6 个核心实体 + 关系
- `research.md` - 技术研究
- `plan.md` - 实施计划
- `quickstart.md` - 设置指南
- `tasks.md` - 任务分解（全部完成）

**关键实体**:
- `MaterialDefinition` - 物料定义
- `MaterialUnit` - 物料单位
- `Provider` - 供应商
- `Waybill` - 运输订单
- `WeighingRecord` - 称重记录
- `AttachmentFile` - 文件附件
- 加上 2 个关联表：`WaybillAttachment`、`WeighingRecordAttachment`

**枚举**:
- `OffsetResultType`、`OrderSource`、`AttachType`

---

### 2.2 未完成功能

#### [002-login-auth/](../specs/002-login-auth/)
**状态**: ⚠️ **已归档** (69/102 任务完成，68%)
**目的**: 软件授权和用户登录

**文档**:
- `spec.md` - 授权和登录需求
- `data-model.md` - 认证实体（`LicenseInfo`、`UserCredential`、`UserSession`）
- `research.md` - 技术研究
- `plan.md` - 实施计划
- `quickstart.md` - 设置指南
- `tasks.md` - 任务分解（部分完成）

**注意**: 已标记为归档，不继续实施

---

## 3. 技术栈文档

### 3.1 项目文件分析

#### MaterialClient.Common (共享库)
**框架**: .NET 10.0 (C# 13)
**平台**: 仅 Windows x64
**关键依赖**:

| 包 | 版本 | 用途 |
|---------|---------|---------|
| EntityFrameworkCore.Sqlite | 10.0.1 | ORM 与数据库 |
| Volo.Abp.Core | 10.0.1 | DDD 框架 |
| Volo.Abp.Autofac | 10.0.1 | 依赖注入 |
| Volo.Abp.EntityFrameworkCore.Sqlite | 10.0.1 | ABP EF Core 集成 |
| System.Reactive | 7.0.0-preview.1 | 响应式扩展 (Rx) |
| ReactiveUI | 20.1.1 | 带 Rx 的 MVVM |
| Refit.HttpClientFactory | 9.0.2 | 类型安全的 HTTP 客户端 |
| Microsoft.Extensions.Http.Polly | 10.0.1 | HTTP 弹性 |
| Serilog | 4.3.0 | 结构化日志 |
| FlashCap | 1.11.0 | 摄像头采集 |
| Aliyun.OSS.SDK.NetCore | 2.14.1 | 云存储 |
| System.IO.Ports | 10.0.1 | 串口通信 |
| System.Management | 10.0.1 | WMI 访问（机器码） |
| Yitter.IdGenerator | 1.0.14 | 分布式 ID 生成 |

**硬件依赖**:
- HCNetSDK - 海康威视摄像头 SDK（原生 DLL）

---

#### MaterialClient (Avalonia UI 应用程序)
**框架**: .NET 10.0 (C# 13)
**输出**: WinExe（Windows 可执行文件）
**关键依赖**:

| 包 | 版本 | 用途 |
|---------|---------|---------|
| Avalonia | 11.3.9 | 跨平台 UI 框架 |
| Avalonia.ReactiveUI | 11.3.8 | Avalonia 的 ReactiveUI |
| Avalonia.Themes.Fluent | 11.3.9 | Fluent 主题 |
| Irihi.Ursa | 1.14.0 | UI 组件库 |
| Semi.Avalonia | 11.3.7.1 | Semi 设计主题 |
| MessageBox.Avalonia | 3.3.1.1 | 消息框 |
| Volo.Abp.Autofac | 10.0.1 | DI 容器 |

**发布配置**:
- `PublishSingleFile: true`
- `SelfContained: true`
- `PublishReadyToRun: true`

---

### 3.2 核心服务清单

**位置**: `MaterialClient.Common/Services/`

| 服务 | 职责 | 状态 |
|---------|----------------|--------|
| `AttendedWeighingService` | 人工称重逻辑 | ✅ 完成 |
| `WeighingMatchingService` | 自动匹配称重记录 | ✅ 完成 |
| `TruckScaleWeightService` | 串口重量读取 | ✅ 完成 |
| `HikvisionService` | 海康威视摄像头集成 | ✅ 完成 |
| `LPRAllInOneService` | 车牌识别 | ✅ 完成 |
| `PlateRecognitionService` | 车牌识别服务 | ✅ 完成 |
| `AttachmentService` | 文件附件管理 | ✅ 完成 |
| `OssUploadService` | 阿里云 OSS 上传 | ✅ 完成 |
| `MaterialService` | 物料管理 | ✅ 完成 |
| `SyncMaterialService` | 从远程同步物料 | ✅ 完成 |
| `DeviceManagerService` | 硬件设备管理器 | ✅ 完成 |
| `SoundDeviceService` | 声音播放 | ✅ 完成 |
| `SettingsService` | 应用程序设置 | ✅ 完成 |
| `SerialPortFactory` | 串口工厂 | ✅ 完成 |
| `SerialPortWrapper` | 串口抽象 | ✅ 完成 |
| `UsbCameraService` | USB 摄像头服务 | ✅ 完成 |
| `PlayM4PortPool` | 海康威视解码器端口池 | ✅ 完成 |
| `PlayM4Decoder` | 海康威视解码器包装器 | ✅ 完成 |
| `AuthenticationService` | 用户登录（已归档） | ⚠️ 已归档 |
| `LicenseService` | 软件许可证（已归档） | ⚠️ 已归档 |
| `MachineCodeService` | 机器码生成（已归档） | ⚠️ 已归档 |
| `PasswordEncryptionService` | 密码加密（已归档） | ⚠️ 已归档 |

---

## 4. 缺少的内容

### 4.1 架构文档
- ❌ **没有软件设计文档 (SDD)**
- ❌ 没有架构图（组件、序列、部署、数据流）
- ❌ 没有技术决策记录 (ADR 格式)
- ❌ 没有系统边界文档

### 4.2 开发指南
- ❌ 没有 Rx.NET 编程指南（尽管大量使用 Rx）
- ❌ 没有内存泄漏预防指南
- ❌ 没有硬件集成最佳实践
- ❌ 没有测试策略文档

### 4.3 运维文档
- ❌ 没有部署指南
- ❌ 没有故障排除指南
- ❌ 没有性能调优指南
- ❌ 没有配置参考

---

## 5. 文档质量评估

### 优点
✅ **详细的技术分析** - 性能报告详尽，包含基准测试
✅ **具体的代码示例** - 报告包含前后代码对比
✅ **可操作的建议** - 明确的优先级（P0-P3）和实施步骤
✅ **功能规范** - 结构良好的规范文档，包含数据模型
✅ **任务跟踪** - 功能的详细任务分解

### 缺点
❌ **分散** - 报告分散，没有单一事实来源
❌ **过时的提案** - RxState 和锁优化提案未实施
❌ **没有 SDD** - 缺少高级架构文档
❌ **没有图表** - 没有可视化架构表示
❌ **语言混合** - 部分报告是中文，部分是英文
❌ **没有维护流程** - 没有定义的流程来保持文档最新

---

## 6. SDD 创建建议

### 高优先级部分
1. **架构概述** - 系统定位、技术栈、模式
2. **模块设计** - 服务职责、接口、依赖
3. **状态管理架构** - Rx 模式使用、最佳实践
4. **数据模型** - 核心实体和关系
5. **架构图** - 组件、序列、数据流、部署

### 中等优先级部分
6. **技术决策** - 关键技术选择的记录
7. **约束与风险** - 平台、硬件、性能约束
8. **开发指南** - Rx 编程、硬件集成
9. **测试策略** - 单元测试、集成测试、内存泄漏测试

### 低优先级部分
10. **部署指南** - 安装、配置
11. **故障排除** - 常见问题和解决方案

---

## 7. 文档维护

### 当前状态
- ❌ 没有定义的维护流程
- ❌ 没有分配负责人
- ❌ 没有审查计划
- ❌ 版本控制存在但没有更新触发器

### 需要的改进
- ✅ 定义 SDD 维护流程
- ✅ 分配文档负责人
- ✅ 设置季度审查计划
- ✅ 与 OpenSpec 工作流集成

---

**下一步**:
1. 完成差距分析（任务 1.2）
2. 评估文档质量（任务 1.3）
3. 开始 SDD 创建（阶段 2）
