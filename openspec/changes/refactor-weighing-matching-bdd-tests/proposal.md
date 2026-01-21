# Change: Refactor WeighingMatchingService BDD Tests to Follow StoreSample Pattern

**Change ID**: `refactor-weighing-matching-bdd-tests`
**Status**: Draft
**Created**: 2026-01-22
**Type**: Refactoring

---

## Why

### Background

The MaterialClient project's BDD tests for `WeighingMatchingService` currently follow a verbose, ad-hoc pattern with:
- Separate step definition file (`WeighingMatchingServiceSteps.cs`) with direct repository access
- Verbose step definitions with complex parameter parsing
- Mixed Chinese and English in feature files
- Inconsistent patterns compared to the StoreSample reference implementation

The StoreSample project demonstrates a cleaner, more maintainable BDD test pattern:
- Unified `Steps.cs` with table-based data setup using DTOs
- `TestManager` class for centralized repository/service access via dependency injection
- Cleaner, more readable feature files with table-driven scenarios
- Consistent exception handling patterns

### Problems

1. **Maintainability**: Current step definitions are verbose and hard to maintain
2. **Consistency**: Different patterns across test files make it harder to understand and extend
3. **Readability**: Feature files mix languages and lack table-based data setup
4. **Testability**: Direct repository access in steps makes it harder to mock or extend
5. **Code Duplication**: Similar patterns repeated across different step definitions

---

## What Changes

### Overview

Refactor `WeighingMatchingService.feature` and `WeighingMatchingServiceSteps.cs` to follow the StoreSample BDD test pattern:
- Create `TestManager` class for centralized test dependencies
- Refactor step definitions to use table-based data setup with DTOs
- Consolidate common steps into main `Steps.cs` file
- Update feature file to use table-based scenarios
- Simplify step definitions by leveraging TestManager pattern

### Detailed Changes

1. **Create TestManager class** in `MaterialClient.Common.Tests`:
   - Inject repositories (`IRepository<WeighingRecord, long>`, `IRepository<Waybill, long>`)
   - Inject `WeighingMatchingService`
   - Provide centralized access to test dependencies

2. **Refactor WeighingMatchingServiceSteps.cs**:
   - Use `TestManager` instead of direct repository access
   - Create DTOs for table-based data setup (`WeighingRecordDto`, `WaybillVerifyDto`)
   - Simplify step definitions using table parsing
   - Remove verbose parameter parsing logic

3. **Update WeighingMatchingService.feature**:
   - Convert verbose step definitions to table-based format
   - Standardize on English for consistency
   - Use table format for record setup and verification

4. **Enhance main Steps.cs**:
   - Add common steps that can be reused (if applicable)
   - Ensure consistency with StoreSample pattern

---

## Impact

### Expected Benefits

- **Improved Maintainability**: Centralized test dependencies make tests easier to maintain
- **Better Readability**: Table-based scenarios are more readable and maintainable
- **Consistency**: Aligns with proven StoreSample pattern
- **Easier Extension**: TestManager pattern makes it easier to add new test dependencies
- **Reduced Duplication**: Common patterns consolidated in TestManager

### Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing tests | High | Run all tests after refactoring, ensure feature file scenarios remain equivalent |
| Learning curve for team | Medium | Document the pattern in test files, reference StoreSample as example |
| Over-engineering for simple tests | Low | Keep TestManager simple, only add what's needed |

---

## Success Criteria

- [ ] All existing test scenarios pass after refactoring
- [ ] Feature file uses table-based data setup
- [ ] TestManager pattern implemented and used consistently
- [ ] Step definitions simplified and more maintainable
- [ ] Code follows StoreSample pattern structure
- [ ] No regression in test coverage

---

## Next Steps

1. Review proposal and get approval
2. Implement TestManager class
3. Refactor step definitions
4. Update feature file
5. Run tests and verify all scenarios pass
6. Update documentation if needed

---

## References

- StoreSample reference: `StoreSample/test/StoreSample.Domain.Tests/Steps.cs`
- StoreSample feature: `StoreSample/test/StoreSample.Domain.Tests/Orders/OrderManagerTest.feature`
- Current implementation: `MaterialClient/MaterialClient.Common.Tests/Steps/WeighingMatchingServiceSteps.cs`
- Current feature: `MaterialClient/MaterialClient.Common.Tests/Features/WeighingMatchingService.feature`
