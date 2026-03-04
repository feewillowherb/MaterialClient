# 变更：修复 WeighingMatchingService 集成测试问题

**变更 ID**：`fix-weighing-matching-integration-test`
**状态**：Draft
**创建日期**：2026-01-22
**类型**：Refactoring

---

## 原因

### 问题

当前 `WeighingMatchingServiceSteps.cs` 集成测试存在若干关键问题：

1. **禁止的 EF Core 属性操作**：测试使用 `entry.Property("CreationTime").CurrentValue = creationTimeValue;`，此方式被明确禁止，会绕过实体封装并可能导致数据不一致。
2. **表格式不统一**：输入部分使用表格式（Given Weighing records as below），但验证步骤使用单独断言而非基于表的验证，导致测试冗长难维护。
3. **业务代码覆盖不全**：集成测试可能未充分覆盖 `AttendedWeighingService.cs` 与 `WeighingMatchingService.cs` 中的业务逻辑，尤其是匹配逻辑与运单创建。
4. **测试模式混杂**：表格式准备与基于单参数的步骤混用，不一致且可读性差。

### 影响

- **代码质量**：禁止用法违反编码标准
- **可维护性**：模式不统一使测试难以理解与扩展
- **测试覆盖**：业务逻辑可能存在缺口
- **可读性**：混合模式降低测试清晰度

---

## 变更内容

### 概览

重构 `WeighingMatchingServiceSteps.cs` 与 `WeighingMatchingService.feature`：  
1. 移除所有 `Property()` 用法，改用正确实体属性（用 `AddDate` 替代操作 `CreationTime`）  
2. 输入准备与验证均统一为表格式  
3. 确保业务逻辑覆盖完整  
4. 遵循集成测试标准模板与基于表的模式  

### 详细变更

#### 1. 修正实体属性访问  
将 `entry.Property("CreationTime").CurrentValue = creationTimeValue;` 改为 `record.AddDate = creationTimeValue;`，并在所有使用处用 `AddDate` 替代对 `Property("CreationTime")` 的访问。

#### 2. 验证步骤统一为表格式  
用基于表的验证（如 `Then Waybills as below` 与表）替代对单个运单属性的逐项断言；统一使用已有的 `ThenWaybillsAsBelow` 等方法。

#### 3. 合并步骤定义  
移除基于单参数的 Given/Then 步骤，仅保留基于表的 `Given Weighing records as below` 与 `Then Waybills as below`。

#### 4. 增强业务逻辑覆盖  
确保覆盖：`WeighingMatchingService.AutoMatchAsync()`、`CreateWaybillAsync()`、`WeighingRecord.TryMatch()` 及多候选、时间窗口、重量关系等边界情况。注：集成测试不强制覆盖查询类业务代码（如 `GetListItemsAsync`）。

#### 5. 更新 Feature 文件  
所有场景统一为：输入使用 `Given Weighing records as below` 表；验证使用 `Then Waybills as below` 表；移除单属性验证步骤。

---

## 影响

**受影响文件**：WeighingMatchingServiceSteps.cs（重构步骤定义）、WeighingMatchingService.feature（改为表格式场景）。  
**预期收益**：消除禁止用法、符合编码标准；表格式一致便于维护与阅读；场景更全面，业务逻辑覆盖更好。  
**风险与缓解**：破坏现有测试（高）→ 正确转换所有场景并运行完整套件；遗漏边界（中）→ 审阅业务覆盖并补充场景；测试数据复杂（低）→ 使用清晰表结构并文档化 DTO。

---

## 成功标准

- [ ] 测试代码中已移除所有 `Property()` 用法
- [ ] 所有场景的输入与验证均使用表格式
- [ ] 重构后所有现有场景通过
- [ ] WeighingMatchingService 与 AttendedWeighingService 业务逻辑得到适当覆盖
- [ ] 无残留的基于单参数的步骤（表格式不适用处除外）
- [ ] Feature 文件一致且可读

---

## 参考

- 当前实现与 Feature、业务代码（WeighingMatchingService、AttendedWeighingService）、实体 WeighingRecord
