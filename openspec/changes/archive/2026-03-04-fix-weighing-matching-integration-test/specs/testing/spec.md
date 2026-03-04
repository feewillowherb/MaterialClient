## ADDED Requirements

### Requirement: Integration Test Standards
Integration tests SHALL follow standard patterns for maintainability and consistency.

#### Scenario: Table-based input setup
- **GIVEN** integration test requires entity setup
- **WHEN** test data is provided
- **THEN** input data SHALL use table format with DTOs
- **AND** individual parameter-based steps SHALL be avoided when table format is applicable

#### Scenario: Table-based verification
- **GIVEN** integration test requires result verification
- **WHEN** test results are verified
- **THEN** verification SHALL use table format with DTOs
- **AND** individual property assertions SHALL be avoided when table format is applicable

#### Scenario: Entity property access
- **GIVEN** integration test requires setting entity properties
- **WHEN** entity properties are set
- **THEN** properties SHALL be accessed directly through entity properties
- **AND** EF Core `Property()` method SHALL NOT be used to manipulate entity properties

#### Scenario: Business logic coverage
- **GIVEN** integration test for service methods
- **WHEN** test scenarios are defined
- **THEN** tests SHALL cover core business logic in service methods
- **AND** tests SHALL cover edge cases and validation logic
- **NOTE** Query business code coverage is not strictly required
