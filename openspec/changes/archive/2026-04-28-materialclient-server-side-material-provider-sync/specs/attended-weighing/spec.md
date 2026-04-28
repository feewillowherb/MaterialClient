## ADDED Requirements

### Requirement: Material/provider edits in attended weighing MUST use remote write path
The system SHALL execute material/provider create-update actions in attended weighing through remote APIs and MUST NOT complete these edits through local-only persistence.

#### Scenario: Material save action from attended view uses remote API
- **WHEN** user saves a new or renamed material from attended weighing flow
- **THEN** the application service MUST call remote material write API
- **THEN** the UI update MUST be based on server-returned state

#### Scenario: Provider save action from attended view uses remote API
- **WHEN** user creates or updates provider information from attended weighing flow
- **THEN** the application service MUST call remote provider write API
- **THEN** attended view MUST refresh state from server write result before continuing navigation logic

### Requirement: Navigation logic MUST consume post-write server state
The system SHALL run post-operation navigation using entity state returned by server-authoritative writes.

#### Scenario: Navigation uses refreshed server state after material/provider write
- **WHEN** a material/provider write in attended weighing succeeds
- **THEN** navigation selection logic MUST use refreshed data that includes server-defaulted and server-normalized fields
- **THEN** the workflow MUST avoid decisions based on stale pre-submit local state
