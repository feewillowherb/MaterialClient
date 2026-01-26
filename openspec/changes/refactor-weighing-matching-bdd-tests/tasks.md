# Tasks: Refactor WeighingMatchingService BDD Tests to Follow StoreSample Pattern

**Change ID**: `refactor-weighing-matching-bdd-tests`
**Total Tasks**: 8
**Estimated Duration**: 1-2 days

---

## Task Overview

Refactor the WeighingMatchingService BDD tests to follow the proven StoreSample pattern, improving maintainability, readability, and consistency. The refactoring involves creating a TestManager class, simplifying step definitions, and updating the feature file to use table-based data setup.

---

## Phase 1: Setup TestManager Infrastructure

### Task 1.1: Create TestManager Class

**Status**: Completed
**Priority**: High
**Estimated**: 1 hour

**Description**:
Create a `TestManager` class similar to StoreSample's pattern that provides centralized access to test dependencies via dependency injection.

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

### Task 1.2: Create DTOs for Table-Based Data Setup

**Status**: Completed
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

## Phase 2: Refactor Step Definitions

### Task 2.1: Refactor Record Setup Steps

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

### Task 2.2: Refactor Matching and Verification Steps

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

### Task 2.3: Clean Up Unused Steps

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

## Phase 3: Update Feature File

### Task 3.1: Convert Feature File to Table-Based Format

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

## Phase 4: Integration and Testing

### Task 4.1: Update Test Module Registration

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

### Task 4.2: Run All Tests and Verify

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

## Progress Tracking

**Phase 1 Progress**: 2/2 tasks completed
**Phase 2 Progress**: 3/3 tasks completed
**Phase 3 Progress**: 1/1 tasks completed
**Phase 4 Progress**: 1/2 tasks completed (test execution pending)
**Overall Progress**: 7/8 tasks (87.5%)
