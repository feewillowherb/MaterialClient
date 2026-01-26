# weighing-matching-service Specification Delta

**Change ID**: `refactor-weighing-matching-bdd-tests`
**Capability**: `weighing-matching-service`
**Operation**: MODIFIED

---

## MODIFIED Requirements

### Requirement: WeighingMatchingService BDD Test Structure

The BDD tests for WeighingMatchingService SHALL follow the StoreSample test pattern for consistency and maintainability.

#### Scenario: TestManager pattern for test dependencies
- **GIVEN** a BDD test for WeighingMatchingService
- **WHEN** accessing repositories or services in step definitions
- **THEN** the test SHALL use a `TestManager` class that provides centralized access via dependency injection
- **AND** the TestManager SHALL be registered as a scoped service in the test module
- **AND** step definitions SHALL access TestManager via `GetRequiredService<TestManager>()`

#### Scenario: Table-based data setup
- **GIVEN** a BDD test scenario that needs to create test data
- **WHEN** setting up weighing records or waybills
- **THEN** the test SHALL use table-based data setup with DTOs
- **AND** the feature file SHALL use Reqnroll table syntax for data input
- **AND** step definitions SHALL parse tables into DTO objects using `table.CreateSet<DtoType>()`

#### Scenario: Simplified step definitions
- **GIVEN** a BDD test step definition
- **WHEN** the step needs to access repositories or services
- **THEN** the step SHALL use TestManager instead of direct repository access
- **AND** the step SHALL avoid verbose parameter parsing in favor of table-based or DTO-based approaches
- **AND** common patterns SHALL be consolidated in reusable steps

#### Scenario: Feature file consistency
- **GIVEN** a BDD feature file for WeighingMatchingService
- **WHEN** defining test scenarios
- **THEN** the feature file SHALL use table-based data setup
- **AND** the feature file SHALL use English consistently
- **AND** the feature file SHALL follow StoreSample's table format patterns

---

## Notes

- This change refactors the test structure without changing the actual test scenarios or business logic being tested
- The TestManager pattern improves maintainability by centralizing test dependencies
- Table-based data setup improves readability and makes it easier to add new test cases
- All existing test scenarios must remain equivalent after refactoring
