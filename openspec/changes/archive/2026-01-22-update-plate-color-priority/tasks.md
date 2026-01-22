# Implementation Tasks

## 1. Variable Renaming
- [x] 1.1 Rename `_filteredPlateColors` to `_lowPriorityPlateColors` in `AttendedWeighingService.cs`
- [x] 1.2 Update all references to the renamed variable throughout the service
- [x] 1.3 Rename configuration key from `FilteredPlateColors` to `LowPriorityPlateColors` in configuration reading code
- [x] 1.4 Update `PlateColorFilterConfig.cs` property name from `FilteredPlateColors` to `LowPriorityPlateColors`
- [x] 1.5 Update log messages to use "low-priority" terminology instead of "filtered"

## 2. Data Structure Updates
- [x] 2.1 Add `ColorType` property to `PlateNumberCacheRecord` (nullable `LprAllInOneColorType?`)
- [x] 2.2 ~~Add `IsLowPriority` computed property to `PlateNumberCacheRecord` based on color~~ (Not needed - computed at selection time)

## 3. Caching Logic Updates
- [x] 3.1 Update `OnPlateNumberRecognized()` to store color information in cache
- [x] 3.2 Remove early return for low-priority colors (lines 400-406) - store them as low-priority instead
- [x] 3.3 Update cache AddOrUpdate logic to preserve color information when incrementing count

## 4. Selection Logic Updates
- [x] 4.1 Update `GetMostFrequentPlateNumber()` to implement priority-based selection
- [x] 4.2 First attempt to find most frequent high-priority plate
- [x] 4.3 Fall back to most frequent low-priority plate only if no high-priority plates exist
- [x] 4.4 Add logging to indicate when low-priority plates are selected

## 5. Configuration Migration
- [x] 5.1 Update appsettings.json to use `LowPriorityPlateColors` key
- [x] 5.2 ~~Add backward compatibility check for old `FilteredPlateColors` key (optional)~~ (Decided not to support backward compat)
- [x] 5.3 Document migration steps in release notes (documented in design.md)

## 6. Testing
- [x] 6.1 Add test for high-priority plate overrides low-priority plate
- [x] 6.2 Add test for low-priority plate used when no high-priority exists
- [x] 6.3 Add test for low-priority plate cannot override existing high-priority plate
- [x] 6.4 ~~Update existing plate filtering tests to reflect priority behavior~~ (Existing tests pass with new behavior)
- [x] 6.5 Add test for color information persistence in cache
- [x] 6.6 Test configuration loading with new key name

## 7. Validation
- [x] 7.1 Run existing tests to ensure no regression (36/38 tests pass; 2 pre-existing failures unrelated to this change)
- [ ] 7.2 Test with real hardware configuration (if available) - **Requires physical hardware testing**
- [x] 7.3 Verify logging provides clear indication of priority selection
- [x] 7.4 Verify configuration migration works correctly
