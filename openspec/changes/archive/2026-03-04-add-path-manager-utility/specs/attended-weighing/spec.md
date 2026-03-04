# Spec Delta: attended-weighing

## ADDED Requirements

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

## MODIFIED Requirements

None. This change adds new capabilities without modifying existing requirements.

---

## REMOVED Requirements

None. This change is purely additive and maintains backward compatibility.
