# 文档分类摘要

**变更 ID**：md-milestone-document-organization  
**生成日期**：2026-01-15  
**文档总数**：51

---

## 按类型分类

### 1. 规范（历史）
- **位置**：`specs/`
- **数量**：24 个文件，涉及 3 个功能
- **功能**：
  - `001-attended-weighing`：9 个文件（spec、research、plan、tasks、data-model、quickstart 等）
  - `001-entity-init`：9 个文件（结构相同）
  - `002-login-auth`：9 个文件（结构相同）
- **类型**：历史功能规范
- **当前相关性**：属 OpenSpec 之前的规范，现已被 OpenSpec 流程替代

### 2. 技术分析报告
- **位置**：`ReadOnlyMd/`、`ReadonlyMd/`
- **数量**：8 个文件
- **示例**：
  - `AttendedWeighingStatus状态机设计评估报告.md` — 状态机设计评估
  - `Avalonia ComboBox绑定问题分析报告.md` — UI 绑定问题分析
  - `TruckScaleWeightService背压风险评估报告.md` — 背压风险评估
  - `重量稳定性监控优化分析.md` — 重量稳定性监控分析
- **类型**：技术分析与设计文档
- **当前相关性**：混合 — 部分描述已解决问题，部分记录当前关注点

### 3. 实现说明
- **位置**：`ReadOnlyMd/`、`ReadonlyMd/`
- **数量**：2 个文件
- **示例**：
  - `称重拍照实现.md` — 称重拍照实现
  - `有人值守实现.md` — 有人值守实现
- **类型**：实现文档
- **当前相关性**：多为已完成功能的历史记录

### 4. 配置与搭建
- **位置**：`ReadOnlyMd/`、`ReadonlyMd/`
- **数量**：2 个文件
- **示例**：
  - `系统配置.md` — 系统配置
  - `cap.md` — 容量/规划文档
- **类型**：配置文档
- **当前相关性**：可能仍为当前配置或已过时

### 5. AI 生成报告
- **位置**：`docs/`、`docs/agents/`
- **数量**：10 个文件
- **示例**：
  - `HikvisionOpenStream-Crash-Analysis-Report.md` — 崩溃分析（48KB，最大）
  - `AttendedWeighingService-RxState-Optimization-Report.md` — Rx 优化报告
  - `ReaderWriterLockSlim-Performance-Evaluation.md` — 性能评估
  - `TruckScaleWeightService-Optimization-2025-12-22.md` — 优化报告
- **类型**：AI 生成的分析与优化报告
- **当前相关性**：混合 — 部分问题已解决，部分仍需关注

### 6. 实现报告
- **位置**：`docs/`
- **数量**：7 个文件
- **示例**：
  - `Complete-Crash-Fix-Summary.md` — 崩溃修复摘要
  - `Port-Pool-Integration-Fix.md` — 端口池集成修复
  - `AttendedWeighingDetailView-Performance-Optimization.md` — 性能优化
- **类型**：实现完成报告
- **当前相关性**：已完成工作的历史记录

### 7. 通用文档
- **位置**：`docs/`、`ReadonlyMd/`、`ReadOnlyMd/`
- **数量**：5 个文件
- **示例**：
  - `hikvision-integration.md` — 海康集成文档
  - `TimerToRx.md` — Timer 转 Rx 迁移指南
  - `物料定义实体.md` — 物料实体定义
  - `登录页面.md` — 登录页文档
- **类型**：通用技术文档
- **当前相关性**：因主题而异

---

## 初步状态评估

### 可能为 SUPERSEDED（已有 OpenSpec 等价物）
- `specs/` 下全部 24 个文件（历史规范格式，已被 OpenSpec 替代）

### 可能为 ARCHIVED（历史记录，问题已解决）
- `ReadOnlyMd/AttendedWeighingStatus状态机设计评估报告.md` — 设计评估，可能已实现
- `ReadOnlyMd/Avalonia ComboBox绑定问题分析报告.md` — 缺陷分析，可能已解决
- `docs/HikvisionOpenStream-Crash-Analysis-Report.md` — 崩溃分析，可能已解决（需确认）
- `docs/Complete-Crash-Fix-Summary.md` — 修复摘要，历史记录
- `docs/Port-Pool-Integration-Fix.md` — 修复记录，历史
- 各类实现报告

### 可能为 VALID（当前有参考价值）
- `ReadOnlyMd/系统配置.md` — 系统配置（需确认是否当前）
- `docs/hikvision-integration.md` — 集成文档（可能仍在使用）
- `docs/TimerToRx.md` — 技术模式文档
- 部分有待落实建议的 AI 报告

### 待评审（状态不明）
- `ReadonlyMd/cap.md` — 用途不明，需评审
- `ReadOnlyMd/NET_DVR_RealPlay_V40.md` — API 文档（需确认是否仍需）
- `docs/内存溢出问题分析报告.md` — 内存泄漏分析（需确认是否已解决）
- 实现状态不明的报告

---

## 存储分析

**总大小**：约 446 KB  
**最大文件**：
1. `ReadonlyMd/cap.md` — 152 KB  
2. `docs/HikvisionOpenStream-Crash-Analysis-Report.md` — 48 KB  
3. `docs/AttendedWeighingService-RxState-Optimization-Report.md` — 29 KB  
4. `specs/002-login-auth/research.md` — 30 KB  
5. `specs/002-login-auth/quickstart.md` — 25 KB  

**按目录**：
- `specs/`：约 199 KB（45%）
- `docs/`：约 88 KB（20%）
- `ReadOnlyMd/`：约 77 KB（17%）
- `ReadonlyMd/`：约 162 KB（36%）

---

## 建议

1. **全部 `specs/` 内容**：迁移至 OpenSpec 归档格式，标记为 SUPERSEDED  
2. **ReadOnlyMd/ReadonlyMd 分析报告**：评审当前相关性，倾向 ARCHIVE  
3. **AI 报告**：核对实现状态，已解决则 ARCHIVE，待办则 VALID  
4. **通用文档**：按文档评审当前相关性  
5. **配置文件**：与系统实际配置核对  

---

## 下一步

1. 为每份文档添加正式元数据头  
2. 进行团队评审以确认分类  
3. 进行依赖分析  
4. 执行压缩与迁移策略  
