## ADDED Requirements

### Requirement: Material writes MUST be server-authoritative
The system SHALL persist material create/rename operations through server APIs and MUST NOT treat local persistence as the source of truth for these operations.

#### Scenario: Create material by name through server API
- **WHEN** user triggers material creation with a material name
- **THEN** client MUST call the configured remote material create endpoint
- **THEN** client MUST use server response as persisted result
- **THEN** client MUST NOT write a local-only material record as final state

#### Scenario: Rename material through server API
- **WHEN** user triggers material rename from attended workflows
- **THEN** client MUST call the configured remote material rename/update endpoint
- **THEN** client MUST apply returned server state (including version) to UI state
- **THEN** client MUST NOT finalize rename via local-only update

### Requirement: Provider writes MUST be server-authoritative
The system SHALL persist provider create/update operations through server APIs and MUST NOT finalize provider modifications through local persistence.

#### Scenario: Create provider through server API
- **WHEN** user triggers provider creation with `providerName` and `deliveryType`
- **THEN** client MUST call remote provider create API
- **THEN** client MUST use server-returned provider payload as source state
- **THEN** client MUST NOT persist provider create as local-only write

#### Scenario: Update provider through server API
- **WHEN** user triggers provider update for `providerName`, `contactName`, or `contactPhone`
- **THEN** client MUST call remote provider update API
- **THEN** client MUST reflect server response as authoritative updated state
- **THEN** client MUST NOT execute local-only provider overwrite behavior

### Requirement: Client write flow MUST handle remote failures predictably
The system SHALL provide stable handling for remote write failures to keep UI behavior deterministic.

#### Scenario: Business validation failure from server
- **WHEN** remote material/provider write returns business validation error
- **THEN** client MUST present mapped user-facing error information
- **THEN** client MUST keep local edit state without committing local persistence

#### Scenario: Transient network failure during write
- **WHEN** remote write fails due to timeout or transient network issue
- **THEN** client MUST apply bounded retry policy
- **THEN** client MUST surface final failure when retries are exhausted
- **THEN** client MUST NOT fallback to local persistence as a substitute write path
