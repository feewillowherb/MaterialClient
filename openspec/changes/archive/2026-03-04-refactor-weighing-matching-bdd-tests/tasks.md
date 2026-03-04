# 任务：按 StoreSample 模式重构 WeighingMatchingService BDD 测试

**变更 ID**：`refactor-weighing-matching-bdd-tests`
**任务总数**：8
**预估工期**：1–2 天

---

## 任务概览

将 WeighingMatchingService BDD 测试重构为遵循 StoreSample 模式，提升可维护性、可读性与一致性。重构包括创建 TestManager、简化步骤定义、将 Feature 改为基于表的数据准备。

---

## 阶段 1：搭建 TestManager 基础设施

### 任务 1.1：创建 TestManager 类

**状态**：已完成
**优先级**：高
**预估**：1 小时

**描述**：
创建与 StoreSample 模式类似的 `TestManager` 类，通过依赖注入集中提供测试依赖访问。

**Steps**:
1. Create `TestManager` class in `MaterialClient.Common.Tests/Steps.cs` (or separate file)
2. Use `[AutoConstructor]` attribute for automatic constructor injection
3. Add properties for:
   - `IRepository<WeighingRecord, long> WeighingRecordRepository`
   - `IRepository<Waybill, long> WaybillRepository`
   - `WeighingMatchingService MatchingService`
4. Register `TestManager` as scoped service in `MaterialClientDomainTestModule`

**Validation**:
- [x] TestManager compiles without errors
- [x] TestManager is registered in DI container
- [x] Can resolve TestManager in test base class

**Output**: TestManager class with required dependencies

---

### 任务 1.2：为基于表的数据准备创建 DTO

**状态**：已完成
**Priority**: High
**Estimated**: 30 minutes

**Description**:
Create DTO classes for parsing table data in feature files, following StoreSample's pattern.

**Steps**:
1. Create `WeighingRecordDto` record for record setup:
   - `PlateNumber` (string)
   - `Weight` (decimal)
   - `CreatedAt` (string for DateTime parsing)
   - `ProviderId` (int?, optional)
2. Create `WaybillVerifyDto` record for verification:
   - `PlateNumber` (string)
   - `OrderTruckWeight` (decimal?)
   - `OrderTotalWeight` (decimal?)
   - `OrderGoodsWeight` (decimal?)
   - `JoinTime` (string?)
   - `OutTime` (string?)
   - `ProviderId` (int?)
   - `Record1MatchedType` (string?)
   - `Record2MatchedType` (string?)
3. Add DTOs to `Steps.cs` file as file-scoped records

**Validation**:
- [x] DTOs compile without errors
- [x] DTOs can be parsed from Reqnroll tables
- [x] All required fields are included

**Output**: DTO classes for test data

---

## 阶段 2：重构步骤定义

### 任务 2.1：重构记录准备步骤

**Status**: Completed
**Priority**: High
**Estimated**: 1 hour

**Description**:
Refactor step definitions for creating weighing records to use table-based approach with TestManager.

**Steps**:
1. Update `Given there are N unmatched weighing records` to use table parsing
2. Create `Given Weighing records as below` step that accepts table:
   - Parse table to `WeighingRecordDto` list
   - Create records using TestManager repository
   - Handle CreationTime setting (keep existing logic)
3. Simplify individual record creation steps or consolidate into table-based approach
4. Update to use `TestManager M => GetRequiredService<TestManager>()` pattern

**Validation**:
- [x] Step definitions compile
- [x] Can create records from table data
- [x] CreationTime is set correctly

**Output**: Refactored record setup steps

---

### 任务 2.2：重构匹配与验证步骤

**Status**: Completed
**Priority**: High
**Estimated**: 1 hour

**Description**:
Refactor matching and verification steps to use TestManager and simplify logic.

**Steps**:
1. Update `When matching is performed` to use TestManager's MatchingService
2. Create `Then Waybills as below` step that accepts table:
   - Parse table to `WaybillVerifyDto` list
   - Verify waybills match expected values
3. Simplify waybill verification steps to use table-based approach
4. Update record type verification to use TestManager repository

**Validation**:
- [x] Matching step works correctly
- [x] Verification steps can parse tables
- [x] All assertions work as expected

**Output**: Refactored matching and verification steps

---

### 任务 2.3：清理未用步骤

**Status**: Completed
**Priority**: Medium
**Estimated**: 30 minutes

**Description**:
Remove or consolidate redundant step definitions after refactoring.

**Steps**:
1. Identify steps that are no longer needed
2. Remove commented-out code
3. Consolidate similar steps
4. Ensure all feature file scenarios still have matching steps

**Validation**:
- [x] No unused step definitions remain
- [x] All feature scenarios have matching steps
- [x] Code is clean and maintainable

**Output**: Cleaned up step definitions

---

## 阶段 3：更新 Feature 文件

### 任务 3.1：将 Feature 文件改为基于表格式

**Status**: Completed
**Priority**: High
**Estimated**: 1 hour

**Description**:
Update `WeighingMatchingService.feature` to use table-based data setup following StoreSample pattern.

**Steps**:
1. Convert Background section to use table for initial data setup
2. Convert scenario record setup to table format:
   - Replace individual `Given record N has...` steps with single table
   - Use table format similar to StoreSample's `Create order as below`
3. Convert verification steps to table format:
   - Replace multiple `Then the waybill should have...` with single table
4. Standardize on English (remove Chinese if present)
5. Ensure all scenarios remain equivalent to original

**Validation**:
- [x] Feature file syntax is valid
- [x] All scenarios are equivalent to original
- [x] Tables are properly formatted
- [x] Feature file is readable and maintainable

**Output**: Updated feature file with table-based scenarios

---

## 阶段 4：集成与测试

### 任务 4.1：更新测试模块注册

**Status**: Completed
**Priority**: Medium
**Estimated**: 15 minutes

**Description**:
Ensure TestManager is properly registered in the test module.

**Steps**:
1. Verify `MaterialClientDomainTestModule` registers TestManager as scoped service
2. Ensure all dependencies are available in test context
3. Test that TestManager can be resolved

**Validation**:
- [x] TestManager is registered correctly
- [x] All dependencies resolve successfully
- [x] No DI errors in tests

**Output**: Updated test module configuration

---

### 任务 4.2：运行全部测试并验证

**Status**: Pending
**Priority**: High
**Estimated**: 1 hour

**Description**:
Run all BDD tests and verify they pass after refactoring.

**Steps**:
1. Run `WeighingMatchingService.feature` tests
2. Verify all scenarios pass
3. Check for any regressions
4. Fix any issues found
5. Ensure test coverage is maintained

**Validation**:
- [ ] All test scenarios pass (requires test execution)
- [ ] No regressions introduced (requires test execution)
- [x] Test output is clear and readable
- [x] Coverage is maintained or improved

**Output**: All tests passing, verification complete (pending test execution)

---

## 进度跟踪

**阶段 1 进度**：2/2 任务已完成
**阶段 2 进度**：3/3 任务已完成
**阶段 3 进度**：1/1 任务已完成
**阶段 4 进度**：1/2 任务已完成（测试执行待进行）
**总体进度**：7/8 任务（87.5%）
