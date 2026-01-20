---
name: Add Plate Color Filtering
overview: Add configuration to filter license plates by color during recognition, with the filtered colors list stored in appsettings.json and loaded only at initialization.
todos:
  - id: add_config_json
    content: Add FilteredPlateColors array to appsettings.json
    status: completed
  - id: create_config_class
    content: Create PlateColorFilterConfig class in Configuration folder
    status: completed
  - id: update_service_field
    content: Add _filteredPlateColors field and IConfiguration dependency to AttendedWeighingService
    status: completed
  - id: load_config
    content: Load filtered colors configuration in StartAsync method
    status: completed
  - id: update_interface
    content: Update IAttendedWeighingService interface signature
    status: completed
  - id: update_method
    content: Update OnPlateNumberRecognized method signature and add filtering logic
    status: completed
  - id: update_callsite
    content: Update MinimalWebHostService to pass colorType from AlarmInfoPlate
    status: completed
  - id: update_tests
    content: Update test method calls if needed
    status: completed
---

# Add License Plate Color Filtering Configuration

## Overview

Implement a feature to filter out specific license plate colors during recognition. The configuration is loaded from `appsettings.json` at service initialization and used to skip processing plates with filtered colors.

## Implementation Steps

### 1. Add Configuration to appsettings.json

Add a new top-level configuration array in [`MaterialClient/appsettings.json`](D:\CodeUp\MaterialClient\MaterialClient\appsettings.json):

```json
"FilteredPlateColors": [1, 2]
```

- Values are integers matching `LprAllInOneColorType` enum (0=Unknown, 1=Blue, 2=Yellow, etc.)
- Can be empty array `[]` or omitted entirely (defaults to no filtering)
- Example: `[1, 2]` means filter out Blue and Yellow plates

### 2. Create Configuration Class

Create a new configuration class to load from appsettings:

```csharp
public class PlateColorFilterConfig
{
    public List<int> FilteredPlateColors { get; set; } = new();
}
```

Location: Create in `MaterialClient.Common/Configuration/` folder alongside other config classes.

### 3. Update AttendedWeighingService

**Add field to store filtered colors:**

```csharp
private HashSet<LprAllInOneColorType> _filteredPlateColors = new();
```

**Load configuration during initialization:**

In the `StartAsync()` method (around line 175-187), after loading `WeighingConfiguration`, add:

```csharp
// Load filtered plate colors from appsettings
var filteredColorsConfig = _configuration.GetSection("FilteredPlateColors").Get<List<int>>();
if (filteredColorsConfig != null)
{
    _filteredPlateColors = filteredColorsConfig
        .Select(c => (LprAllInOneColorType)c)
        .ToHashSet();
    _logger?.LogInformation($"Loaded {_filteredPlateColors.Count} filtered plate colors");
}
```

Note: Need to inject `IConfiguration` into the constructor.

**Update method signature:**

Change `OnPlateNumberRecognized` from:

```csharp
public void OnPlateNumberRecognized(string plateNumber)
```

To:

```csharp
public void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null)
```

**Add filtering logic:**

At the beginning of `OnPlateNumberRecognized` (after line 372), add:

```csharp
// Filter by color if specified and in filtered list
if (colorType.HasValue && _filteredPlateColors.Contains(colorType.Value))
{
    _logger?.LogDebug($"Filtered out plate {plateNumber} with color {colorType.Value}");
    return;
}
```

### 4. Update Interface

Update [`IAttendedWeighingService`](D:\CodeUp\MaterialClient\MaterialClient.Common\Services\AttendedWeighingService.cs) interface (line 105):

```csharp
void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null);
```

### 5. Update MinimalWebHostService Call Site

In [`MinimalWebHostService.cs`](D:\CodeUp\MaterialClient\MaterialClient\Services\MinimalWebHostService.cs), update the AlarmInfoPlate callback handler (around line 190-194):

**Current code:**

```csharp
var license = callback?.AlarmInfoPlate?.Result?.PlateResult?.License;
if (!string.IsNullOrWhiteSpace(license))
{
    weighingService.OnPlateNumberRecognized(license);
```

**Updated code:**

```csharp
var plateResult = callback?.AlarmInfoPlate?.Result?.PlateResult;
var license = plateResult?.License;
if (!string.IsNullOrWhiteSpace(license))
{
    var colorType = plateResult?.ColorType.HasValue == true 
        ? (LprAllInOneColorType?)plateResult.ColorType.Value 
        : null;
    weighingService.OnPlateNumberRecognized(license, colorType);
```

### 6. Update Tests

Update test calls in [`AttendedWeighingServiceTests.cs`](D:\CodeUp\MaterialClient\MaterialClient.Common.Tests\Tests\AttendedWeighingServiceTests.cs) to include the new optional parameter (can pass `null` or omit since it's optional).

## Key Design Decisions

- **HashSet for filtering**: O(1) lookup performance for checking filtered colors
- **Optional parameter**: Backward compatible - existing calls without color still work
- **Load at initialization**: Configuration is immutable after service starts (no runtime changes)
- **Default behavior**: If config is missing or empty, no filtering occurs (safe default)
- **Enum values**: Using `LprAllInOneColorType` enum for type safety

## Files to Modify

- `MaterialClient/appsettings.json` - Add FilteredPlateColors array
- `MaterialClient.Common/Configuration/PlateColorFilterConfig.cs` - New file
- `MaterialClient.Common/Services/AttendedWeighingService.cs` - Add field, load config, update method
- `MaterialClient/Services/MinimalWebHostService.cs` - Pass colorType parameter
- `MaterialClient.Common.Tests/Tests/AttendedWeighingServiceTests.cs` - Update test calls (if needed)