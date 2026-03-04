# 变更：按 StoreSample 模式重构 WeighingMatchingService BDD 测试

**变更 ID**：`refactor-weighing-matching-bdd-tests`
**状态**：Draft
**创建日期**：2026-01-22
**类型**：Refactoring

---

## 原因

### 背景

MaterialClient 项目中针对 `WeighingMatchingService` 的 BDD 测试当前采用冗长、临时性的模式：独立步骤定义文件、直接访问仓储、复杂参数解析、Feature 文件中中英混用，与 StoreSample 参考实现不一致。StoreSample 采用更清晰、可维护的模式：统一的 Steps.cs、基于表的 DTO 数据准备、通过依赖注入集中访问的 TestManager、表驱动的可读 Feature 与一致的异常处理。

### 问题

1. **可维护性**：步骤定义冗长难维护  
2. **一致性**：不同测试文件模式不一，理解与扩展困难  
3. **可读性**：Feature 混用语言且缺少基于表的数据准备  
4. **可测性**：步骤中直接访问仓储不利于 mock 与扩展  
5. **重复**：类似模式在多个步骤定义中重复  

---

## 变更内容

### 概览

将 `WeighingMatchingService.feature` 与 `WeighingMatchingServiceSteps.cs` 重构为遵循 StoreSample BDD 模式：创建 TestManager 集中测试依赖；用基于表的 DTO 数据准备重构步骤；将公共步骤合并到主 Steps.cs；更新 Feature 为基于表场景；通过 TestManager 简化步骤定义。

### 详细变更

1. **创建 TestManager 类**（在 MaterialClient.Common.Tests）：注入仓储与 WeighingMatchingService，提供集中访问。  
2. **重构 WeighingMatchingServiceSteps.cs**：用 TestManager 替代直接仓储访问；为表格式准备创建 DTO（WeighingRecordDto、WaybillVerifyDto）；用表解析简化步骤；移除冗长参数解析。  
3. **更新 WeighingMatchingService.feature**：将冗长步骤改为基于表格式；统一使用英文；记录准备与验证均用表。  
4. **增强主 Steps.cs**：补充可复用公共步骤（如适用）；与 StoreSample 模式保持一致。

---

## 影响

**预期收益**：可维护性提升（集中依赖）；可读性更好（表驱动场景）；与 StoreSample 一致；更易扩展（TestManager）；减少重复。  
**风险与缓解**：破坏现有测试（高）→ 重构后运行全部测试并保持场景等价；团队学习成本（中）→ 在测试文件中文档化模式并参考 StoreSample；对简单测试过度设计（低）→ TestManager 保持简单、按需添加。

---

## 成功标准

- [ ] 重构后所有现有场景通过  
- [ ] Feature 使用基于表的数据准备  
- [ ] TestManager 模式已实现并一致使用  
- [ ] 步骤定义简化且更易维护  
- [ ] 代码结构符合 StoreSample 模式  
- [ ] 测试覆盖无回归  

---

## 后续步骤与参考

审阅并批准提案 → 实现 TestManager → 重构步骤定义 → 更新 Feature → 运行测试并验证 → 必要时更新文档。参考：StoreSample 的 Steps.cs 与 OrderManagerTest.feature；当前实现与 Feature 路径。
