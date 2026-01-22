## ADDED Requirements

### Requirement: Test Module API Mock Registration
The test infrastructure SHALL provide mock implementations for all external API dependencies to enable BDD scenario execution without external service dependencies.

#### Scenario: Register MaterialPlatformApi mock for authentication tests
- **GIVEN** the test module is being configured
- **WHEN** `ConfigureServices` method is invoked
- **THEN** `IMaterialPlatformApi` SHALL be registered as a singleton mock
- **AND** the mock SHALL provide a default successful login response
- **AND** the login response SHALL include a valid `LoginUserDto` with UserId, UserName, Token, and AuthEndTime

#### Scenario: Register SoundDeviceApi mock for integration tests
- **GIVEN** the test module is being configured
- **WHEN** `ConfigureServices` method is invoked
- **THEN** `ISoundDeviceApi` SHALL be registered as a singleton mock
- **AND** the mock SHALL provide a stub implementation for `PlayAudioAsync`
- **AND** the stub SHALL return a successful JSON response without requiring external HTTP service

#### Scenario: Authentication steps can resolve IMaterialPlatformApi
- **GIVEN** `MaterialClientEntityFrameworkCoreTestModule` has been initialized
- **WHEN** `AuthenticationSteps` constructor attempts to resolve `IMaterialPlatformApi`
- **THEN** the DI container SHALL successfully resolve the mock instance
- **AND** no `InvalidOperationException` SHALL be thrown

#### Scenario: BDD scenarios execute without external service dependencies
- **GIVEN** all API mocks are registered in the test module
- **WHEN** BDD scenarios in `Authentication.feature`, `Authorization.feature`, `WeighingService.feature`, and `WeighingMatchingService.feature` are executed
- **THEN** all scenarios SHALL initialize without DI resolution errors
- **AND** scenarios SHALL be able to configure mock behavior per test case
- **AND** no external HTTP services SHALL be required for test execution

### Requirement: Test Isolation and Mock Reset
The test infrastructure SHALL ensure proper isolation between test scenarios by resetting mock state before each scenario execution.

#### Scenario: Mock state is reset before each BDD scenario
- **GIVEN** a BDD test step class has a `[BeforeScenario]` method
- **WHEN** the scenario starts execution
- **THEN** all registered mocks SHALL have their received calls cleared
- **AND** mock behavior SHALL be reset to default configuration
- **AND** subsequent scenarios SHALL not be affected by previous scenario mock interactions

#### Scenario: Test-specific mock behavior can be configured
- **GIVEN** the default mock behavior is registered in test module
- **WHEN** a test step configures specific mock behavior (e.g., login failure response)
- **THEN** the test-specific behavior SHALL override the default behavior for that test
- **AND** the mock SHALL return the configured response
- **AND** other tests SHALL not be affected by the test-specific configuration
