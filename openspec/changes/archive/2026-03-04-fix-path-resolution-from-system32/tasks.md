# Implementation Tasks

## 1. Fix Database Connection String Resolution
- [x] 1.1 Update `MaterialClientCommonModule.ConfigureServices()` to call `DatabaseConnectionStringFactory.FixConnectionString()`
- [ ] 1.2 Verify database path resolution in logs when started from System32
- [ ] 1.3 Test database migrations work correctly

## 2. Fix Attachment Path Resolution
- [x] 2.1 Add `GetLocalStorageAbsolutePath()` method to `AttachmentPathUtils` that returns absolute paths
- [x] 2.2 Update `GetBillPhotoFullPath()` to use absolute paths
- [x] 2.3 Update `GetMonitoringPhotoFullPath()` to use absolute paths
- [x] 2.4 Ensure directory creation uses absolute paths

## 3. Fix Ticket Printing Service Path Resolution
- [x] 3.1 Add path normalization in `TicketPrintingService.PrintToPdf()` entry point
- [x] 3.2 Add path normalization in `TicketPrintingService.PrintImageToPdf()` entry point
- [x] 3.3 Add path normalization in `TicketPrintingService.RenderTicketToImage()` entry point
- [ ] 3.4 Test ticket PDF generation with relative paths when started from System32
- [ ] 3.5 Test ticket PDF generation with absolute paths (regression)

## 4. Integration Testing
- [ ] 4.1 Test application startup from System32 directory
- [ ] 4.2 Verify database access works correctly
- [ ] 4.3 Verify photo capture and storage works (USB camera and bill photos)
- [ ] 4.4 Verify Hikvision camera photo capture works correctly
- [ ] 4.5 Verify photo loading from historical records works
- [ ] 4.6 Verify OSS upload service can find local files
- [ ] 4.7 Verify photo display in all ViewModels (AttendedWeighing, ManualMatch, PhotoGrid)
- [ ] 4.8 Verify ticket printing to PDF works correctly (relative and absolute paths)
- [ ] 4.9 Test normal startup from application directory (regression)

## 5. Documentation
- [x] 5.1 Add code comments explaining path resolution strategy
- [x] 5.2 Document `AppContext.BaseDirectory` usage pattern
