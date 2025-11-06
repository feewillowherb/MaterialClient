# Quick Start: 有人值守功能

**Date**: 2025-11-06  
**Feature**: [spec.md](./spec.md)

## Overview

This guide provides a quick start for implementing the attended weighing feature. Follow the steps in order.

## Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or Rider (recommended)
- ABP Framework 9.3.6 packages
- Avalonia UI 11.3.6 packages

## Implementation Steps

### Step 1: Add Required NuGet Packages

**MaterialClient.Common.csproj**:
```xml
<PackageReference Include="Volo.Abp.AspNetCore" Version="9.3.6" />
<PackageReference Include="Volo.Abp.Autofac" Version="9.3.6" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
```

**MaterialClient.csproj**:
```xml
<!-- Already has Avalonia packages -->
```

### Step 2: Create Enums

Create the following enum files in `MaterialClient.Common/Entities/Enums/`:

1. **VehicleWeightStatus.cs** - See [data-model.md](./data-model.md)
2. **DeliveryType.cs** - See [data-model.md](./data-model.md)
3. **WeighingRecordType.cs** - See [data-model.md](./data-model.md)

### Step 3: Modify WeighingRecord Entity

**File**: `MaterialClient.Common/Entities/WeighingRecord.cs`

Add property:
```csharp
public WeighingRecordType RecordType { get; set; } = WeighingRecordType.Unmatch;
```

### Step 4: Create Database Migration

Run EF Core migration to add `RecordType` column to `WeighingRecords` table:

```bash
dotnet ef migrations add AddWeighingRecordType --project MaterialClient.Common
dotnet ef database update --project MaterialClient.Common
```

### Step 5: Create Hardware Service Interfaces and Implementations

**Location**: `MaterialClient.Common/Services/Hardware/`

Create:
1. `ITruckScaleWeightService.cs` + `TruckScaleWeightService.cs`
2. `IPlateNumberCaptureService.cs` + `PlateNumberCaptureService.cs`
3. `IVehiclePhotoService.cs` + `VehiclePhotoService.cs`
4. `IBillPhotoService.cs` + `BillPhotoService.cs`

**Implementation Pattern**:
- Store test values in private fields
- Return fixed test data (see [contracts](./contracts/README.md))
- Add "待对接设备" comment in all implementations

### Step 6: Create WeighingService

**File**: `MaterialClient.Common/Services/WeighingService.cs`

**Responsibilities**:
- Monitor truck scale weight (poll every 100ms)
- Track state transitions (OffScale → OnScale → Weighing)
- Create WeighingRecord when weight is stable
- Call hardware services (plate number capture, vehicle photo)
- Handle failures gracefully (log and continue)

**Key Logic**:
- State machine for VehicleWeightStatus
- Timer for stable duration tracking
- Boundary value handling (>= for upper, <= for lower)

### Step 7: Create WeighingMatchingService

**File**: `MaterialClient.Common/Services/WeighingMatchingService.cs`

**Responsibilities**:
- Find matching WeighingRecords (Rule 1: same plate + time window)
- Validate matching rules (Rule 2: time order + weight relationship)
- Create Waybill when matched
- Update WeighingRecord.RecordType (Join/Out)

**Key Logic**:
- Query all Unmatch records
- Group by PlateNumber
- Find pairs within time window
- Select pair with shortest time interval if multiple candidates
- Validate weight relationship based on DeliveryType

### Step 8: Create HTTP API Controllers

**Location**: `MaterialClient.Common/Controllers/` (or create new Controllers folder)

Create:
1. `TruckScaleWeightController.cs` - GET/POST `/api/hardware/truck-scale/weight`
2. `PlateNumberController.cs` - GET/POST `/api/hardware/plate-number`
3. `VehiclePhotoController.cs` - GET `/api/hardware/vehicle-photos`
4. `BillPhotoController.cs` - GET `/api/hardware/bill-photo`

**Base Class**: Inherit from `AbpControllerBase`

### Step 9: Configure ABP HTTP Host

**File**: `MaterialClient/Program.cs`

**Steps**:
1. Create `WebApplicationBuilder` and `WebApplication`
2. Configure ABP modules
3. Configure Swagger
4. Start HTTP server in background thread
5. Keep Avalonia UI running in main thread

**Example Structure**:
```csharp
// Configure ABP HTTP Host
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication<MaterialClientHttpHostModule>();
var app = builder.Build();
app.InitializeApplication();

// Start HTTP server in background
Task.Run(() => app.Run());

// Start Avalonia UI
BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
```

### Step 10: Create ABP HTTP Host Module

**File**: `MaterialClient/HttpHost/MaterialClientHttpHostModule.cs`

**Responsibilities**:
- Register ABP modules
- Configure Swagger
- Register API controllers
- Minimal configuration (no Auth, no CORS)

### Step 11: Register Services in MaterialClientCommonModule

**File**: `MaterialClient.Common/MaterialClientCommonModule.cs`

**Add registrations**:
```csharp
// Hardware services
services.AddTransient<ITruckScaleWeightService, TruckScaleWeightService>();
services.AddTransient<IPlateNumberCaptureService, PlateNumberCaptureService>();
services.AddTransient<IVehiclePhotoService, VehiclePhotoService>();
services.AddTransient<IBillPhotoService, BillPhotoService>();

// Business services
services.AddTransient<WeighingService>();
services.AddTransient<WeighingMatchingService>();
```

### Step 12: Create Avalonia UI

**Design Reference Files** (located in `assets/` folder):
- `main_ui1.png` / `main_ui2.png` - Main UI design reference
- `order_list.png` - Order list layout (shows WeighingRecord and Waybill list)
- `order_detail.png` - Order detail view (shows WeighingRecord or Waybill details)
- `join_out_camera.png` - Vehicle photo display interface
- `bill.png` - Bill photo display interface
- `MainWindow.xaml` - **DO NOT USE** - Contains layout errors, use only as reference structure

**Important**: 
- **DO NOT** copy layout from `MainWindow.xaml` directly - it contains layout errors
- Use standard Avalonia UI layout principles
- Reference the PNG images for visual design and layout structure
- Follow Avalonia UI best practices for proper layout

**Files to Create**:
- `MaterialClient/Views/AttendedWeighingWindow.axaml`
- `MaterialClient/Views/AttendedWeighingWindow.axaml.cs`
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`

**UI Layout** (based on reference images):
- **Order List Area** (`order_list.png`): 
  - Shows WeighingRecord (WeighingRecordType == Unmatch) as "未完成" (Unmatched)
  - Shows Waybill as "已完成" (Completed)
  - Click to show detail view
- **Detail View** (`order_detail.png`):
  - For WeighingRecord: Display weight, join time, other fields empty
  - For Waybill: Display complete waybill information
- **Vehicle Photo View** (`join_out_camera.png`): Display vehicle photos
- **Bill Photo View** (`bill.png`): Display bill photo (optional)

**ViewModel**:
- Use `IRepository<WeighingRecord, long>` for data access
- Use `IRepository<Waybill, long>` for waybill data
- Use `ObservableCollection` for list binding
- Implement commands for item selection

### Step 13: Add Configuration

**File**: `appsettings.json`

Add:
```json
{
  "Weighing": {
    "WeightOffsetRangeMin": -1,
    "WeightOffsetRangeMax": 1,
    "WeightStableDurationMs": 2000,
    "WeighingMatchDurationHours": 3
  }
}
```

### Step 14: Write Integration Tests

**Location**: `MaterialClient.Common.Tests/`

Create:
1. `WeighingServiceTests.cs` - Test state transitions and record creation
2. `WeighingMatchingServiceTests.cs` - Test matching logic
3. `HardwareServiceTests.cs` - Test hardware service interfaces

**Test Base**: Use `MaterialClientEntityFrameworkCoreTestBase` or `MaterialClientDomainTestBase`

**Key Test Scenarios**:
- Vehicle enters and weight stabilizes → WeighingRecord created
- Vehicle enters but weight doesn't stabilize → No record, log failure
- Two records match → Waybill created
- Multiple candidates → Select shortest time interval pair

## Testing

### Manual Testing

1. **Start Application**: Run `MaterialClient` project
2. **Access Swagger**: Open `http://localhost:5000/swagger`
3. **Test Hardware APIs**: Use Swagger UI to set test values
4. **Monitor Weighing**: Watch console logs for state transitions
5. **Verify UI**: Check AttendedWeighingWindow for records and waybills

### Integration Testing

Run tests:
```bash
dotnet test MaterialClient.Common.Tests
```

## Common Issues

### Issue: HTTP Server not starting

**Solution**: Check that HTTP server is running in background thread, not blocking Avalonia UI

### Issue: WeighingRecord not created

**Solution**: 
- Check weight value is outside offset range
- Verify stable duration is reached
- Check logs for errors

### Issue: Matching not working

**Solution**:
- Verify time window (default 3 hours)
- Check DeliveryType is set correctly
- Verify weight relationship matches DeliveryType

## Next Steps

After implementation:
1. Run integration tests
2. Manual testing with Swagger
3. Verify UI displays correctly
4. Check logs for errors

See [tasks.md](./tasks.md) (generated by `/speckit.tasks`) for detailed task breakdown.

