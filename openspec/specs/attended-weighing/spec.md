# attended-weighing Specification

## Purpose
TBD - created by archiving change refactor-weighing-item-navigation. Update Purpose after archive.
## Requirements
### Requirement: Item Navigation After Operations

The system SHALL provide consistent navigation to WeighingListItemDto objects after operations (Save, Complete, Match, Abolish) are performed.

#### Scenario: Complete operation navigation
- **WHEN** user completes a waybill (FirstWeight → Completed) in AttendedWeighingDetailView
- **THEN** the system SHALL:
  - Refresh the list data to reflect the completed state
  - Select the newly completed waybill in the list
  - Display the completed waybill in AttendedWeighingMainView (not DetailView)
  - Navigate to the correct page if the item is not on the current page
  - Switch to the appropriate tab only if necessary (respect IsShowAllRecords flag)

#### Scenario: Save operation navigation
- **WHEN** user saves changes to a weighing record or waybill in AttendedWeighingDetailView
- **THEN** the system SHALL:
  - Refresh the list data to reflect the saved changes
  - Keep the saved item selected in the list
  - Remain in AttendedWeighingDetailView (allow continued editing)
  - Stay on the current tab (item state hasn't changed)
  - Navigate to the correct page if the item moved due to sorting

#### Scenario: Match operation navigation
- **WHEN** user manually matches a weighing record with another record
- **THEN** the system SHALL:
  - Refresh the list data to show the newly created waybill
  - Select the next unmatched item in the list
  - Display the next unmatched item in AttendedWeighingDetailView
  - Switch to unmatched tab if currently on completed tab and not showing all records
  - Navigate to the correct page for the next item

#### Scenario: Abolish operation navigation
- **WHEN** user abolishes (deletes) a weighing record
- **THEN** the system SHALL:
  - Refresh the list data to remove the abolished record
  - Select the next unmatched item in the list
  - Display the next unmatched item in AttendedWeighingDetailView
  - Stay on the current tab (item was removed, not moved)
  - Navigate to the correct page for the next item

### Requirement: Tab Switching Rules

The system SHALL implement intelligent tab switching that respects user context and only switches when necessary.

#### Scenario: Tab switching respects "All Records" mode
- **WHEN** IsShowAllRecords flag is true (user selected "All Records" tab)
- **THEN** the system SHALL NOT automatically switch tabs after any operation
- **BECAUSE** all items are visible on this tab regardless of completion status

#### Scenario: Tab switching when item moves to completed state
- **WHEN** an item becomes completed (OrderType changes to Completed)
- **AND** IsShowUnmatched is true (user is on "Unmatched" tab)
- **AND** IsShowAllRecords is false
- **THEN** the system SHALL switch to IsShowCompleted = true ("Completed" tab)

#### Scenario: Tab switching when item moves to unmatched state
- **WHEN** an item becomes unmatched (OrderType changes to FirstWeight or Unmatch)
- **AND** IsShowCompleted is true (user is on "Completed" tab)
- **AND** IsShowAllRecords is false
- **THEN** the system SHALL switch to IsShowUnmatched = true ("Unmatched" tab)

#### Scenario: No tab switching when current tab contains target item
- **WHEN** an operation completes and the resulting item's state matches the current tab filter
- **THEN** the system SHALL NOT switch tabs
- **EXAMPLE**: User on "Completed" tab saves a completed waybill → stay on "Completed" tab

### Requirement: Cross-Page Item Navigation

The system SHALL find and navigate to items across pagination boundaries.

#### Scenario: Item found on current page
- **WHEN** navigating to a target item after an operation
- **AND** the target item is present on the current page
- **THEN** the system SHALL:
  - Select the item immediately without changing pages
  - Complete navigation in O(1) time

#### Scenario: Item found on different page
- **WHEN** navigating to a target item after an operation
- **AND** the target item is NOT present on the current page
- **THEN** the system SHALL:
  - Search across pages starting from page 1
  - Navigate to the page containing the target item
  - Select the item once found
  - Limit search to a maximum of 10 pages to prevent excessive loading

#### Scenario: Item not found after search
- **WHEN** navigating to a target item after an operation
- **AND** the target item cannot be found after searching available pages
- **THEN** the system SHALL:
  - Fall back to selecting the first item in the current list
  - Log a warning about the missing item
  - Not display an error to the user (graceful degradation)

### Requirement: View Selection Based on Item State

The system SHALL automatically select the appropriate view (MainView or DetailView) based on the item's type and completion status.

#### Scenario: Completed waybill displays in MainView
- **WHEN** navigating to an item that is a Waybill
- **AND** the waybill's OrderType is Completed
- **THEN** the system SHALL display AttendedWeighingMainView (read-only summary view)

#### Scenario: Editable items display in DetailView
- **WHEN** navigating to an item that is NOT a completed waybill
- **EXAMPLES**: WeighingRecord (unmatched), Waybill with OrderType = FirstWeight
- **THEN** the system SHALL display AttendedWeighingDetailView (editable form view)

#### Scenario: View selection after Complete operation
- **WHEN** user completes a waybill (changes OrderType from FirstWeight to Completed)
- **THEN** the system SHALL switch from AttendedWeighingDetailView to AttendedWeighingMainView
- **BECAUSE** the item is now read-only and optimized for viewing in MainView

### Requirement: Operation Event Context

The system SHALL provide complete context information in operation events to enable proper navigation.

#### Scenario: Event arguments include operation context
- **WHEN** an operation (Save, Complete, Match, Abolish) completes in AttendedWeighingDetailView
- **THEN** the raised event SHALL include:
  - ItemId: The ID of the resulting item
  - ItemType: Whether the item is a WeighingRecord or Waybill
  - OrderType: The current order type (Unmatch, FirstWeight, Completed)
  - IsCompleted: Boolean flag for quick completion status check
  - OperationType: String identifying which operation was performed

#### Scenario: Complete operation event
- **WHEN** user completes a waybill successfully
- **THEN** CompleteCompleted event SHALL be raised with:
  - ItemId = the waybill ID
  - ItemType = Waybill
  - OrderType = Completed
  - IsCompleted = true
  - OperationType = "Complete"

#### Scenario: Save operation event
- **WHEN** user saves changes to a record or waybill
- **THEN** SaveCompleted event SHALL be raised with:
  - ItemId = the saved item's ID
  - ItemType = the item's current type
  - OrderType = the item's current order type
  - IsCompleted = based on OrderType
  - OperationType = "Save"

### Requirement: Unified Navigation Logic

The system SHALL use a single unified method for all post-operation navigation to ensure consistency.

#### Scenario: All operations use NavigateToItemAsync
- **WHEN** any operation event handler is triggered (Save, Complete, Match, Abolish, ManualMatch)
- **THEN** the handler SHALL call the unified NavigateToItemAsync method
- **AND** NavigateToItemAsync SHALL handle:
  - Data refresh
  - Tab switching decision
  - Page navigation
  - Item selection
  - View selection

#### Scenario: Navigation logic is predictable and testable
- **WHEN** testing navigation behavior
- **THEN** all navigation paths SHALL go through NavigateToItemAsync
- **ALLOWING** single point of testing and maintenance
- **ENSURING** consistent behavior across all operations

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

### Requirement: System MUST Store File Paths as Relative Paths for Database Portability

The system SHALL store file paths in the database using relative paths (relative to application base directory) to enable database migration between different servers or directories without breaking file references.

**Context**: When database files are migrated to a new server or directory (e.g., from `D:\MaterialClient\` to `E:\Apps\MaterialClient\`), absolute paths like `D:\MaterialClient\Photos\car.jpg` would break. Relative paths like `Photos/car.jpg` remain valid after migration.

**Implementation Constraint**: This applies to all `AttachmentFile.LocalPath` values stored in the database.

#### Scenario: Photo path stored as relative path

**Given** the application is running from `D:\MaterialClient\`  
**When** a photo is captured and saved to `D:\MaterialClient\Photos\2026\01\23\bill.jpg`  
**Then** the database `AttachmentFile.LocalPath` must store `"Photos/2026/01/23/bill.jpg"` (relative path)  
**And** not store `"D:\MaterialClient\Photos\2026\01\23\bill.jpg"` (absolute path)

#### Scenario: Database migrated to new location

**Given** a database contains `AttachmentFile` with `LocalPath = "Photos/2026/01/23/car.jpg"`  
**And** the database file is copied from `D:\MaterialClient\` to `E:\NewLocation\`  
**And** the `Photos` folder is also copied to `E:\NewLocation\Photos\`  
**When** the application runs from `E:\NewLocation\`  
**Then** the photo at `E:\NewLocation\Photos\2026/01/23/car.jpg` must load successfully  
**And** no path updates are required in the database

#### Scenario: Existing absolute paths remain functional

**Given** a database contains legacy `AttachmentFile` with `LocalPath = "D:\MaterialClient\Photos\car.jpg"` (absolute path from old version)  
**When** the application runs from `D:\MaterialClient\`  
**Then** the photo must still load successfully  
**And** new photo captures must use relative paths going forward

---

### Requirement: Image Converters MUST Normalize Paths Before Loading

Image converters SHALL normalize relative paths to absolute paths before file existence checks and image loading operations, ensuring images render correctly regardless of the application's working directory at launch.

**Context**: When the application is launched from `C:\Windows\System32` (e.g., via Task Scheduler), relative paths from the database would incorrectly resolve to `C:\Windows\System32\Photos\...` without normalization.

**Implementation Constraint**: This applies to `CarNullOrEmptyImageConverter` and `NullOrEmptyImageConverter` used throughout the UI.

#### Scenario: Image loads from relative path when launched from System32

**Given** the application is launched from `C:\Windows\System32\`  
**And** the database contains `AttachmentFile` with `LocalPath = "Photos/2026/01/23/car.jpg"`  
**When** the UI attempts to display the image using `CarNullOrEmptyImageConverter`  
**Then** the converter must normalize the path to `{AppContext.BaseDirectory}\Photos\2026\01\23\car.jpg`  
**And** the image must render successfully  
**And** not attempt to load from `C:\Windows\System32\Photos\2026\01\23\car.jpg`

#### Scenario: Image loads from absolute path (backward compatibility)

**Given** the database contains legacy `AttachmentFile` with `LocalPath = "D:\MaterialClient\Photos\car.jpg"` (absolute path)  
**When** the UI attempts to display the image  
**Then** the converter must detect the path is already absolute  
**And** use it directly without modification  
**And** the image must render successfully

#### Scenario: Default image shown for missing files

**Given** the database contains `AttachmentFile` with `LocalPath = "Photos/missing.jpg"`  
**And** the file does not exist at `{AppContext.BaseDirectory}\Photos\missing.jpg`  
**When** the UI attempts to display the image  
**Then** the converter must show the default car image placeholder  
**And** not throw an exception

#### Scenario: Asset paths handled separately

**Given** a ViewModel provides an asset path `"avares://MaterialClient/Assets/Car_Default.png"`  
**When** the UI attempts to display the image using `CarNullOrEmptyImageConverter`  
**Then** the converter must recognize it as an asset path  
**And** load it from embedded resources  
**And** not apply file path normalization

---

### Requirement: System MUST Provide Unified PathManager Utility

The system SHALL provide a centralized `PathManager` utility with bidirectional path conversion methods (`ToAbsolutePath`, `ToRelativePath`) and file operation helpers, ensuring consistent path handling across all services.

**Context**: Path conversion logic was previously scattered across `DatabaseConnectionStringFactory`, `AttachmentPathUtils`, and service code. Centralizing this logic reduces duplication and ensures consistency.

**Implementation Constraint**: `PathManager` must be a static utility class in `MaterialClient.Common/Utils/` namespace, following project conventions for configuration-unrelated utility logic.

#### Scenario: Convert relative path to absolute for file operations

**Given** the application base directory is `D:\MaterialClient\`  
**When** `PathManager.ToAbsolutePath("Photos/2026/01/23/car.jpg")` is called  
**Then** it must return `"D:\MaterialClient\Photos\2026\01\23\car.jpg"`  
**And** the path must be fully normalized (no `..` or extra slashes)

#### Scenario: Convert absolute path to relative for database storage

**Given** the application base directory is `D:\MaterialClient\`  
**When** `PathManager.ToRelativePath("D:\MaterialClient\Photos\2026\01\23\car.jpg")` is called  
**Then** it must return `"Photos\2026\01\23\car.jpg"`

#### Scenario: Idempotent conversion (already absolute)

**Given** an absolute path `"D:\MaterialClient\Photos\car.jpg"`  
**When** `PathManager.ToAbsolutePath("D:\MaterialClient\Photos\car.jpg")` is called  
**Then** it must return the input unchanged: `"D:\MaterialClient\Photos\car.jpg"`

#### Scenario: Idempotent conversion (already relative)

**Given** a relative path `"Photos/car.jpg"`  
**When** `PathManager.ToRelativePath("Photos/car.jpg")` is called  
**Then** it must return the input unchanged: `"Photos/car.jpg"`

#### Scenario: Path outside application directory remains absolute

**Given** an absolute path `"C:\Users\Admin\Desktop\export.pdf"` (outside app directory)  
**When** `PathManager.ToRelativePath("C:\Users\Admin\Desktop\export.pdf")` is called  
**Then** it must return the input unchanged (cannot be made relative)  
**And** return `"C:\Users\Admin\Desktop\export.pdf"`

#### Scenario: File existence check with path normalization

**Given** the application base directory is `D:\MaterialClient\`  
**And** a file exists at `D:\MaterialClient\Photos\car.jpg`  
**When** `PathManager.FileExists("Photos/car.jpg")` is called  
**Then** it must normalize to absolute path internally  
**And** return `true`

#### Scenario: Directory creation with path normalization

**Given** the application base directory is `D:\MaterialClient\`  
**When** `PathManager.EnsureDirectoryExists("Photos/2026/01/23")` is called  
**Then** it must create the directory at `D:\MaterialClient\Photos\2026\01\23\`  
**And** return the absolute path `"D:\MaterialClient\Photos\2026\01\23"`  
**And** create any missing parent directories

---

