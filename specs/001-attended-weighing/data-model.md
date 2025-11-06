# Data Model: 有人值守功能

**Date**: 2025-11-06  
**Feature**: [spec.md](./spec.md)

## Entity Modifications

### WeighingRecord (修改)

**File**: `MaterialClient.Common/Entities/WeighingRecord.cs`

**Changes**:
- 新增字段：`WeighingRecordType` (enum, nullable)

**Complete Entity Structure**:
```csharp
public class WeighingRecord : FullAuditedEntity<long>
{
    public decimal Weight { get; set; }                    // 重量
    public string? PlateNumber { get; set; }               // 车牌号（可为空）
    public int? ProviderId { get; set; }                    // 供应商ID（可选）
    public int? MaterialId { get; set; }                    // 物料ID（可选）
    public WeighingRecordType RecordType { get; set; }     // 新增：记录类型（Unmatch/Join/Out）
    
    // Navigation properties
    public Provider? Provider { get; set; }
    public Material? Material { get; set; }
    public ICollection<WeighingRecordAttachment> Attachments { get; set; }
}
```

**Validation Rules**:
- Weight must be > 0
- PlateNumber can be null or empty string
- ProviderId and MaterialId are optional (can be null)
- RecordType defaults to Unmatch (0)

**State Transitions**:
- Unmatch → Join: When matched as join record
- Unmatch → Out: When matched as out record
- Join/Out: Final state (no further transitions)

---

## New Enums

### VehicleWeightStatus

**File**: `MaterialClient.Common/Entities/Enums/VehicleWeightStatus.cs`

```csharp
public enum VehicleWeightStatus
{
    /// <summary>
    /// 已下称
    /// </summary>
    OffScale = 0,

    /// <summary>
    /// 已上称
    /// </summary>
    OnScale = 1,

    /// <summary>
    /// 称重完成
    /// </summary>
    Weighing = 2
}
```

**Usage**: Used in WeighingService to track truck scale state

---

### DeliveryType

**File**: `MaterialClient.Common/Entities/Enums/DeliveryType.cs`

```csharp
public enum DeliveryType
{
    /// <summary>
    /// 发料
    /// </summary>
    Delivery = 0,

    /// <summary>
    /// 收料
    /// </summary>
    Receiving = 1
}
```

**Usage**: 
- Used in matching logic to determine weight comparison direction
- Delivery: Join.Weight < Out.Weight
- Receiving: Join.Weight > Out.Weight

---

### WeighingRecordType

**File**: `MaterialClient.Common/Entities/Enums/WeighingRecordType.cs`

```csharp
public enum WeighingRecordType
{
    /// <summary>
    /// 未匹配
    /// </summary>
    Unmatch = 0,

    /// <summary>
    /// 进场
    /// </summary>
    Join = 1,

    /// <summary>
    /// 出场
    /// </summary>
    Out = 2
}
```

**Usage**: 
- Tracks matching status of WeighingRecord
- Unmatch: Not yet matched
- Join: Matched as join (incoming) record
- Out: Matched as out (outgoing) record

---

## Existing Entities (No Changes)

### Waybill

**File**: `MaterialClient.Common/Entities/Waybill.cs`

**Note**: No modifications needed. Waybill is created from matched WeighingRecords.

**Key Fields**:
- OrderNo: Generated from Guid.ToString()
- ProviderId: Extracted from Join or Out record (can be null if both are null)
- MaterialId: Extracted from Join or Out record (can be null if both are null)
- JoinTime: From Join record CreationTime
- OutTime: From Out record CreationTime
- OrderTotalWeight: Calculated from Join and Out weights
- DeliveryType: Set from UI/page configuration

**Relationships**:
- One Provider (optional)
- Multiple WaybillAttachments (optional, for bill photos)

---

### WeighingRecordAttachment

**File**: `MaterialClient.Common/Entities/WeighingRecordAttachment.cs`

**Note**: No modifications needed. Used to store vehicle photos.

**Relationships**:
- One WeighingRecord
- One AttachmentFile

---

### WaybillAttachment

**File**: `MaterialClient.Common/Entities/WaybillAttachment.cs`

**Note**: No modifications needed. Used to store bill photos (optional).

**Relationships**:
- One Waybill
- One AttachmentFile

---

## Configuration

### WeighingConfiguration

**File**: `MaterialClient.Common/Configuration/WeighingConfiguration.cs` (New)

```csharp
public class WeighingConfiguration
{
    public decimal WeightOffsetRangeMin { get; set; } = -1m;      // 偏移范围下限
    public decimal WeightOffsetRangeMax { get; set; } = 1m;      // 偏移范围上限
    public int WeightStableDurationMs { get; set; } = 2000;       // 稳定时间（毫秒）
    public int WeighingMatchDurationHours { get; set; } = 3;    // 匹配时间窗口（小时）
}
```

**Storage**: JSON configuration file (appsettings.json)

**Default Values**:
- WeightOffsetRange: -1 to 1 (decimal)
- WeightStableDuration: 2000ms
- WeighingMatchDuration: 3 hours

---

## Database Schema Changes

### Migration Required

**WeighingRecord Table**:
- Add column: `RecordType` (INTEGER, NOT NULL, DEFAULT 0)
  - Maps to WeighingRecordType enum

**No new tables required** - all other entities already exist.

---

## Entity Relationships Diagram

```
WeighingRecord (modified)
├── Provider (optional FK)
├── Material (optional FK)
└── WeighingRecordAttachments (1:N)
    └── AttachmentFile

Waybill (unchanged)
├── Provider (optional FK)
└── WaybillAttachments (1:N, optional)
    └── AttachmentFile

VehicleWeightStatus (enum) - Used in service logic, not stored
DeliveryType (enum) - Stored in Waybill.DeliveryType
WeighingRecordType (enum) - Stored in WeighingRecord.RecordType
```

---

## Validation Rules Summary

1. **WeighingRecord**:
   - Weight > 0
   - PlateNumber: nullable, no validation (allows empty string)
   - ProviderId, MaterialId: nullable
   - RecordType: defaults to Unmatch

2. **Waybill**:
   - OrderNo: unique, generated from Guid
   - ProviderId, MaterialId: nullable (can be null if both Join and Out records have null)
   - JoinTime < OutTime (enforced in matching logic)
   - OrderTotalWeight: calculated from Join and Out weights

3. **Matching Rules** (enforced in WeighingMatchingService):
   - Rule 1: Same PlateNumber AND time difference <= WeighingMatchDuration
   - Rule 2: Join.CreationTime < Out.CreationTime AND weight relationship based on DeliveryType
   - If multiple candidates: select pair with shortest time interval

---

## Notes

- All entity names, properties, and enums use English names (constitution requirement)
- All entities inherit from ABP base classes (FullAuditedEntity)
- Use ABP repository pattern (IRepository<TEntity, TKey>) for data access
- Navigation properties follow ABP conventions

