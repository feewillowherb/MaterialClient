## ADDED Requirements

### Requirement: Manual match uses fixed weight diff threshold
The system SHALL use a fixed minimum weight difference threshold of 0.1 tons for manual match operations, independent of the configurable `_minWeightDiff` used by automatic matching.

#### Scenario: Get candidate records for manual match
- **WHEN** the system retrieves candidate weighing records for manual matching via `GetCandidateRecordsAsync` with a manual match override
- **THEN** the system SHALL use `minWeightDiff = 0.1` to filter candidates, allowing records with weight differences as small as 0.1 tons

#### Scenario: Execute manual match
- **WHEN** the user confirms a manual match via `ManualMatchAsync`
- **THEN** the system SHALL validate the match using `minWeightDiff = 0.1`

#### Scenario: Automatic match unaffected
- **WHEN** the system performs automatic matching via `TryMatchWithDeliveryTypeAsync`
- **THEN** the system SHALL continue using the configured `_minWeightDiff` value from settings
