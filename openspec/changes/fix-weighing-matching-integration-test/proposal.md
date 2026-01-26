# Change: Fix WeighingMatchingService Integration Test Issues

**Change ID**: `fix-weighing-matching-integration-test`
**Status**: Draft
**Created**: 2026-01-22
**Type**: Refactoring

---

## Why

### Problems

The current `WeighingMatchingServiceSteps.cs` integration test has several critical issues:

1. **Prohibited EF Core Property Manipulation**: The test uses `entry.Property("CreationTime").CurrentValue = creationTimeValue;` which is strictly prohibited. This approach bypasses entity encapsulation and can lead to data inconsistencies.

2. **Inconsistent Table Format**: While the test partially uses table format for input (`Given Weighing records as below`), verification steps use individual assertions instead of table-based verification, making tests verbose and harder to maintain.

3. **Incomplete Business Code Coverage**: The integration test may not properly cover business logic in `AttendedWeighingService.cs` and `WeighingMatchingService.cs`, particularly around matching logic and waybill creation.

4. **Mixed Test Patterns**: The test mixes table-based setup with individual parameter-based steps, creating inconsistency and reducing readability.

### Impact

- **Code Quality**: Prohibited patterns violate coding standards
- **Maintainability**: Inconsistent patterns make tests harder to understand and extend
- **Test Coverage**: Potential gaps in business logic coverage
- **Readability**: Mixed patterns reduce test clarity

---

## What Changes

### Overview

Refactor `WeighingMatchingServiceSteps.cs` and `WeighingMatchingService.feature` to:
1. Remove all `Property()` usage and use proper entity properties (`AddDate` instead of manipulating `CreationTime`)
2. Standardize on table format for both input setup and verification
3. Ensure comprehensive coverage of business logic
4. Follow integration test standard template with table-based patterns

### Detailed Changes

#### 1. Fix Entity Property Access

**Current (Prohibited)**:
```csharp
entry.Property("CreationTime").CurrentValue = creationTimeValue;
```

**Fixed**:
```csharp
record.AddDate = creationTimeValue;
```

Replace all instances where `Property("CreationTime")` is used with direct assignment to `AddDate` property, which is the correct property on `WeighingRecord` entity.

#### 2. Standardize Table Format for Verification

**Current**: Individual assertions for each waybill property
```csharp
[Then(@"the waybill should have OrderTruckWeight (.*) kg")]
public void ThenTheWaybillShouldHaveOrderTruckWeight(decimal expectedWeight)
{
    // Individual assertion
}
```

**Fixed**: Table-based verification
```gherkin
Then Waybills as below
  | PlateNumber | OrderTruckWeight | OrderTotalWeight | OrderGoodsWeight |
  | 京A12345    | 10.0             | 15.0             | 5.0              |
```

The `ThenWaybillsAsBelow` method already exists but needs to be used consistently across all scenarios.

#### 3. Consolidate Step Definitions

Remove individual parameter-based steps in favor of table-based steps:
- Remove `Given record (.*) has plate number...` steps
- Use only `Given Weighing records as below` with table format
- Remove individual `Then the waybill should have...` steps
- Use only `Then Waybills as below` with table format

#### 4. Enhance Business Logic Coverage

Ensure integration tests cover:
- `WeighingMatchingService.AutoMatchAsync()` - automatic matching logic
- `WeighingMatchingService.CreateWaybillAsync()` - waybill creation
- `WeighingRecord.TryMatch()` - matching validation logic
- Edge cases: multiple candidates, time window validation, weight relationship validation

Note: Integration tests do not strictly require covering query business code (e.g., `GetListItemsAsync`).

#### 5. Update Feature File

Convert all scenarios to use table format consistently:
- Input: Always use `Given Weighing records as below` with table
- Verification: Always use `Then Waybills as below` with table
- Remove individual property verification steps

---

## Impact

### Affected Files

- `MaterialClient.Common.Tests/Steps/WeighingMatchingServiceSteps.cs` - Refactor step definitions
- `MaterialClient.Common.Tests/Features/WeighingMatchingService.feature` - Update scenarios to use table format

### Expected Benefits

- **Code Quality**: Removes prohibited patterns, follows coding standards
- **Maintainability**: Consistent table-based patterns make tests easier to maintain
- **Readability**: Table format is more readable than individual assertions
- **Coverage**: Better coverage of business logic through comprehensive scenarios

### Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing tests | High | Ensure all scenarios are converted correctly, run full test suite |
| Missing edge cases | Medium | Review business logic coverage, add scenarios for edge cases |
| Test data complexity | Low | Use clear table structure, document DTOs |

---

## Success Criteria

- [ ] All `Property()` usage removed from test code
- [ ] All scenarios use table format for both input and verification
- [ ] All existing test scenarios pass after refactoring
- [ ] Business logic in `WeighingMatchingService` and `AttendedWeighingService` is properly covered
- [ ] No individual parameter-based steps remain (except where table format is not applicable)
- [ ] Feature file is consistent and readable

---

## References

- Current implementation: `MaterialClient.Common.Tests/Steps/WeighingMatchingServiceSteps.cs`
- Current feature: `MaterialClient.Common.Tests/Features/WeighingMatchingService.feature`
- Business code: 
  - `MaterialClient.Common/Services/WeighingMatchingService.cs`
  - `MaterialClient.Common/Services/AttendedWeighingService.cs`
- Entity: `MaterialClient.Common/Entities/WeighingRecord.cs`
