# Change: Fix Path Resolution When Started from System32

## Why

When MaterialClient is launched via Windows Task Scheduler or Registry auto-start (e.g., from `C:\Windows\System32\`), the application fails to access critical resources:

1. **Database Access Failure**: SQLite cannot open `MaterialClient.db` because the relative path resolves to `C:\Windows\System32\MaterialClient.db` instead of the application directory
2. **Attachment Access Failure**: Photo attachments stored in `PhotoPiaoJu/` and `PhotoJianKong/` directories cannot be found because relative paths resolve to System32

**Error Evidence** (from logs):
```
SQLite Error 14: 'unable to open database file'
Server: 'MaterialClient.db'
```

This breaks core functionality:
- Database migrations fail on startup
- Settings service cannot initialize
- Vehicle photos cannot be saved or loaded
- Ticket photos cannot be accessed
- Ticket PDF files may be generated in System32 directory if relative paths are provided

## What Changes

- **Fix database connection string resolution** by using existing `DatabaseConnectionStringFactory.FixConnectionString()` in `MaterialClientCommonModule.cs`
- **Fix attachment path resolution** by extending `AttachmentPathUtils` to return absolute paths based on `AppContext.BaseDirectory`
- **Fix ticket printing output paths** by adding path resolution at `TicketPrintingService` entry points (`PrintToPdf`, `PrintImageToPdf`, `RenderTicketToImage`)
- Ensure all file system paths are resolved relative to the application executable directory, not the current working directory
- No changes to global working directory (maintains existing behavior for all other operations)

## Impact

### Affected Specs
- `attended-weighing` - Photo capture and storage behavior

### Affected Code
- `MaterialClient.Common/MaterialClientCommonModule.cs` - Database configuration
- `MaterialClient.Common/Utils/AttachmentPathUtils.cs` - Photo path resolution
- `MaterialClient.Common/Utils/DatabaseConnectionStringFactory.cs` - Already exists, will be used
- `MaterialClient.Common/Services/Hardware/TicketPrintingService.cs` - Ticket PDF/image output path resolution
- Any code that saves/loads attachments (no code changes needed, utility handles it)

### Components Indirectly Fixed
The following components will automatically work correctly after the fix, with no code changes required:
- `MaterialClient.Common/Services/Hikvision/HikvisionService.cs` - Camera photo capture (receives absolute paths from `AttachmentPathUtils`)
- `MaterialClient.Common/Services/AttendedWeighingService.cs` - Photo attachment creation (uses `AttachmentPathUtils`)
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs` - Bill photo capture (uses `AttachmentPathUtils`)
- `MaterialClient.Common/Services/AttachmentService.cs` - Photo loading and OSS upload (reads from database paths)
- All ViewModels that display photos (read paths from database)

### Breaking Changes
None - this is a bug fix that restores intended behavior.

### Migration
Automatic - paths will resolve correctly on next application start.
