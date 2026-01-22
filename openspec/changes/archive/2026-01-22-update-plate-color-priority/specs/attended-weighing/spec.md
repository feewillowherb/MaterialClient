## ADDED Requirements

### Requirement: Plate Color Priority Matching

The system SHALL implement a priority-based plate number selection mechanism where filtered plate colors are treated as lowest priority rather than being rejected.

#### Scenario: High-priority plate overrides low-priority plate

- **GIVEN** a vehicle with yellow plate (low-priority color) is on scale
- **AND** the plate has been recognized 10 times (cached with count=10)
- **WHEN** a vehicle with blue plate (high-priority color) is also detected once
- **THEN** the system SHALL select the blue plate as most frequent plate number
- **AND** the yellow plate SHALL remain in cache but not be selected

#### Scenario: Low-priority plate used when no high-priority exists

- **GIVEN** a vehicle with yellow plate (low-priority color) is on scale
- **AND** no other plates have been detected
- **WHEN** the system selects the most frequent plate number
- **THEN** the system SHALL return the yellow plate
- **AND** log a message indicating low-priority plate is being used

#### Scenario: Low-priority plate cannot override existing high-priority plate

- **GIVEN** a vehicle with blue plate (high-priority color) is cached with count=1
- **WHEN** a yellow plate (low-priority color) is recognized 100 times
- **THEN** the system SHALL continue to return the blue plate
- **AND** the yellow plate SHALL accumulate count in cache but not be selected

#### Scenario: Plate without color information treated as high-priority

- **GIVEN** a plate number is recognized without color information (colorType is null)
- **WHEN** the plate is cached
- **THEN** the system SHALL treat it as high-priority by default
- **AND** it SHALL be able to override low-priority plates

### Requirement: Plate Number Cache Color Tracking

The system SHALL store color information alongside plate numbers in the cache to support priority-based selection.

#### Scenario: Color information persisted in cache

- **GIVEN** a plate is recognized with color type YELLOW
- **WHEN** the plate is added to cache
- **THEN** the cache record SHALL include:
  - Count: number of recognitions
  - LastUpdateTime: timestamp of last recognition
  - ColorType: YELLOW (the detected color)

#### Scenario: Color information preserved when incrementing count

- **GIVEN** a plate "京A12345" with color BLUE is cached with count=1
- **WHEN** the same plate is recognized again with color BLUE
- **THEN** the cache record SHALL update to:
  - Count: 2 (incremented)
  - LastUpdateTime: (updated to current time)
  - ColorType: BLUE (preserved from first recognition)

#### Scenario: Cache handles missing color information

- **GIVEN** a plate is recognized without color information (colorType is null)
- **WHEN** the plate is added to cache
- **THEN** the cache record SHALL store ColorType as null
- **AND** the plate SHALL be treated as high-priority

### Requirement: Plate Color Priority Configuration

The system SHALL support configuring certain plate colors as low-priority, treating them as fallback options rather than rejecting them entirely.

#### Scenario: Low-priority color stored with flag

- **GIVEN** configuration specifies YELLOW in LowPriorityPlateColors array
- **WHEN** a yellow plate is recognized via OnPlateNumberRecognized
- **THEN** the system SHALL NOT reject the plate
- **AND** SHALL store it in cache with ColorType=YELLOW
- **AND** SHALL mark it as low-priority for selection purposes
- **AND** log message indicating low-priority color detected

#### Scenario: Normal color stored as high-priority

- **GIVEN** configuration specifies YELLOW in LowPriorityPlateColors array
- **WHEN** a blue plate is recognized via OnPlateNumberRecognized
- **THEN** the system SHALL store it in cache with ColorType=BLUE
- **AND** SHALL mark it as high-priority for selection purposes
- **AND** NOT log any priority-related message

#### Scenario: Configuration loading uses new key name

- **GIVEN** appsettings.json contains LowPriorityPlateColors array
- **WHEN** AttendedWeighingService starts
- **THEN** the system SHALL load the colors from LowPriorityPlateColors configuration key
- **AND** store them in _lowPriorityPlateColors HashSet
- **AND** use them to determine low-priority vs high-priority plates
- **AND** log the low-priority colors during initialization

### Requirement: Configuration Key Renaming

The system SHALL use LowPriorityPlateColors as the configuration key name to reflect priority-based semantics rather than rejection-based filtering.

#### Scenario: New configuration key used for loading

- **GIVEN** configuration file contains LowPriorityPlateColors key
- **WHEN** AttendedWeighingService initializes
- **THEN** the system SHALL read plate colors from LowPriorityPlateColors key
- **AND** SHALL NOT attempt to read from old FilteredPlateColors key
- **AND** log "Low-priority plate colors: [list]" during initialization

#### Scenario: Variable naming reflects priority semantics

- **GIVEN** the service code uses internal variables for plate color priority
- **THEN** the variable SHALL be named _lowPriorityPlateColors (not _filteredPlateColors)
- **AND** all log messages SHALL use "low-priority" terminology
- **AND** code comments SHALL reference priority-based behavior
