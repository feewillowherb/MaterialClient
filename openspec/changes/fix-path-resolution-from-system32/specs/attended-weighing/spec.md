# Spec Delta: Attended Weighing - Path Resolution Fix

## MODIFIED Requirements

### Requirement: Vehicle Photo Storage
The system SHALL capture and store vehicle photos during weighing operations using absolute file paths resolved from the application executable directory.

**Context**: When the application starts from any working directory (including `C:\Windows\System32` via Task Scheduler or Registry auto-start), photo paths must resolve correctly to the application's storage directories, not the current working directory.

#### Scenario: Photo capture when started from System32
- **GIVEN** MaterialClient is launched via Task Scheduler with working directory `C:\Windows\System32`
- **AND** a vehicle enters the scale
- **WHEN** the system captures a vehicle photo
- **THEN** the photo SHALL be saved to `{AppContext.BaseDirectory}\PhotoJianKong\{year}\{MM}\{dd}\{filename}.jpg`
- **AND** the database SHALL store the absolute path to the photo
- **AND** the photo file SHALL be accessible for viewing

#### Scenario: Bill photo capture when started from System32
- **GIVEN** MaterialClient is launched via Task Scheduler with working directory `C:\Windows\System32`
- **AND** a user manually captures a bill photo
- **WHEN** the system saves the bill photo
- **THEN** the photo SHALL be saved to `{AppContext.BaseDirectory}\PhotoPiaoJu\{year}\{MM}\{dd}\bill_{timestamp}.jpg`
- **AND** the database SHALL store the absolute path to the photo
- **AND** the photo file SHALL be accessible for printing and viewing

#### Scenario: Loading historical photos when started from System32
- **GIVEN** MaterialClient is launched via Task Scheduler with working directory `C:\Windows\System32`
- **AND** photos were previously captured and stored with absolute paths
- **WHEN** the user views historical weighing records
- **THEN** the system SHALL successfully load and display all associated photos
- **AND** no photo access errors SHALL occur

#### Scenario: Photo storage during normal startup
- **GIVEN** MaterialClient is launched normally from its installation directory
- **WHEN** the system captures any photo type
- **THEN** photos SHALL be stored using absolute paths based on `AppContext.BaseDirectory`
- **AND** behavior SHALL be identical to Task Scheduler launch scenario
- **AND** no regression in existing functionality SHALL occur

## ADDED Requirements

### Requirement: Database Access from Any Working Directory
The system SHALL access the SQLite database file using an absolute path resolved from the application executable directory, regardless of the process working directory.

**Context**: When launched via Windows Task Scheduler or Registry auto-start, the application may start with `C:\Windows\System32` as the working directory. The database file must be accessed from the application directory, not the working directory.

#### Scenario: Database initialization when started from System32
- **GIVEN** MaterialClient is launched via Task Scheduler with working directory `C:\Windows\System32`
- **AND** the connection string in `appsettings.json` is `"Data Source=MaterialClient.db"`
- **WHEN** the application initializes the database connection
- **THEN** the connection string SHALL be converted to `"Data Source={AppContext.BaseDirectory}\MaterialClient.db"`
- **AND** the database file SHALL be successfully opened
- **AND** database migrations SHALL complete successfully

#### Scenario: Database access with pre-existing absolute path
- **GIVEN** the connection string in `appsettings.json` contains an absolute path like `"Data Source=C:\CustomPath\MaterialClient.db"`
- **WHEN** the application initializes the database connection
- **THEN** the absolute path SHALL be preserved unchanged
- **AND** the database file SHALL be accessed at the specified absolute path
- **AND** no path conversion SHALL occur

#### Scenario: Settings service initialization when started from System32
- **GIVEN** MaterialClient is launched via Task Scheduler with working directory `C:\Windows\System32`
- **WHEN** the settings service initializes its cache
- **THEN** the database SHALL be accessible
- **AND** settings SHALL be loaded successfully
- **AND** no `SQLite Error 14: 'unable to open database file'` SHALL occur
