# Data Model: 物料系统实体

**Date**: 2025-01-30  
**Feature**: [spec.md](./spec.md) | [plan.md](./plan.md) | [research.md](./research.md)

## Overview

本文档定义了物料管理系统的6个核心实体及其关联关系。所有实体遵循ABP框架的DDD原则，使用EF Core和SQLite进行持久化。

## Entity Relationships Diagram

```
MaterialDefinition (物料定义)
    ├── 1:N ──> MaterialUnit (物料单位)
    └── 1:N ──> WeighingRecord (称重记录)

Provider (供应商)
    ├── 1:N ──> Waybill (运单)
    ├── 1:N ──> WeighingRecord (称重记录)
    └── 1:N ──> MaterialUnit (物料单位)

Waybill (运单)
    └── N:M ──> AttachmentFile (附件文件) [via WaybillAttachment]

WeighingRecord (称重记录)
    └── N:M ──> AttachmentFile (附件文件) [via WeighingRecordAttachment]
```

## Entities

### 1. MaterialDefinition (物料定义)

**Base Class**: `Entity<int>`  
**Namespace**: `MaterialClient.Common.Entities`  
**Table Name**: `MaterialDefinitions`

**Fields**:

| Field Name | Type | Nullable | Description | Notes |
|------------|------|----------|-------------|-------|
| Id | int | No | 主键 | 自增 |
| Name | string | No | 物料名称 | |
| Brand | string | Yes | 品牌 | |
| Size | string | Yes | 规格尺寸 | |
| UpperLimit | decimal? | Yes | 上限 | |
| LowerLimit | decimal? | Yes | 下限 | |
| BasicUnit | string | Yes | 基本单位 | |
| Code | string | Yes | 物料编码 | |
| CoId | int | No | 公司ID | |
| Specifications | string | Yes | 规格说明 | |
| ProId | string | Yes | 产品ID | |
| UnitName | string | Yes | 单位名称 | |
| UnitRate | decimal | No | 单位换算率 | 默认值：1 |

**Relationships**:
- One-to-Many: `MaterialUnit` (via `MaterialId`)
- One-to-Many: `WeighingRecord` (via `MaterialId`)

**Validation Rules**:
- `Name` must not be empty
- `UnitRate` must be greater than 0

---

### 2. MaterialUnit (物料单位)

**Base Class**: `Entity<int>`  
**Namespace**: `MaterialClient.Common.Entities`  
**Table Name**: `MaterialUnits`

**Fields**:

| Field Name | Type | Nullable | Description | Notes |
|------------|------|----------|-------------|-------|
| Id | int | No | 主键 | 自增 |
| MaterialId | int | No | 物料ID | FK to MaterialDefinition |
| UnitName | string | No | 单位名称 | |
| Rate | decimal | No | 换算率 | |
| ProviderId | int | Yes | 供应商ID | FK to Provider |
| RateName | string | Yes | 换算率名称 | |

**Relationships**:
- Many-to-One: `MaterialDefinition` (via `MaterialId`)
- Many-to-One: `Provider` (via `ProviderId`, optional)

**Validation Rules**:
- `MaterialId` must reference an existing MaterialDefinition
- `Rate` must be greater than 0
- `UnitName` must not be empty

---

### 3. Provider (供应商)

**Base Class**: `Entity<int>`  
**Namespace**: `MaterialClient.Common.Entities`  
**Table Name**: `Providers`

**Fields**:

| Field Name | Type | Nullable | Description | Notes |
|------------|------|----------|-------------|-------|
| Id | int | No | 主键 | 自增 |
| ProviderType | int | No | 供应商类型 | |
| ProviderName | string | No | 供应商名称 | |
| ContactName | string | Yes | 联系人姓名 | 修正：原ContectName |
| ContactPhone | string | Yes | 联系人电话 | 修正：原ContectPhone |

**Relationships**:
- One-to-Many: `Waybill` (via `ProviderId`)
- One-to-Many: `WeighingRecord` (via `ProviderId`)
- One-to-Many: `MaterialUnit` (via `ProviderId`)

**Validation Rules**:
- `ProviderName` must not be empty

---

### 4. Waybill (运单)

**Base Class**: `FullAuditedEntity<long>`  
**Namespace**: `MaterialClient.Common.Entities`  
**Table Name**: `Waybills`

**Fields**:

| Field Name | Type | Nullable | Description | Notes |
|------------|------|----------|-------------|-------|
| Id | long | No | 主键 | 自增 |
| ProviderId | int | No | 供应商ID | FK to Provider |
| OrderNo | string | No | 订单号 | |
| OrderType | int? | Yes | 订单类型 | |
| DeliveryType | int? | Yes | 配送类型 | |
| PlateNumber | string | Yes | 车牌号 | |
| JoinTime | DateTime? | Yes | 进场时间 | |
| OutTime | DateTime? | Yes | 出场时间 | |
| Remark | string | Yes | 备注 | |
| OrderPlanOnWeight | decimal | No | 计划重量 | |
| OrderPlanOnPcs | decimal | No | 计划件数 | |
| OrderPcs | decimal | No | 实际件数 | |
| OrderTotalWeight | decimal? | Yes | 总重量 | |
| OrderTruckWeight | decimal? | Yes | 车辆重量 | |
| OrderGoodsWeight | decimal? | Yes | 货物重量 | |
| LastSyncTime | DateTime? | Yes | 最后同步时间 | |
| IsEarlyWarn | bool | No | 是否预警 | 默认值：false |
| PrintCount | int | No | 打印次数 | 默认值：0 |
| AbortReason | string | Yes | 中止原因 | |
| OffsetResult | OffsetResultType | No | 偏移结果 | 枚举类型，默认：Default |
| EarlyWarnType | string | Yes | 预警类型 | |
| OrderSource | OrderSource | No | 订单来源 | 枚举类型 |

**Audit Fields** (from `FullAuditedEntity<long>`):
- CreationTime
- CreatorId
- LastModificationTime
- LastModifierId
- IsDeleted
- DeletionTime
- DeleterId

**Relationships**:
- Many-to-One: `Provider` (via `ProviderId`)
- Many-to-Many: `AttachmentFile` (via `WaybillAttachment`)

**Validation Rules**:
- `ProviderId` must reference an existing Provider
- `OrderNo` must not be empty
- `OrderPlanOnWeight` must be >= 0
- `OrderPlanOnPcs` must be >= 0
- `OrderPcs` must be >= 0

---

### 5. WeighingRecord (称重记录)

**Base Class**: `FullAuditedEntity<long>`  
**Namespace**: `MaterialClient.Common.Entities`  
**Table Name**: `WeighingRecords`

**Fields**:

| Field Name | Type | Nullable | Description | Notes |
|------------|------|----------|-------------|-------|
| Id | long | No | 主键 | 自增 |
| Weight | decimal | No | 重量 | 修正：原weight |
| PlateNumber | string | Yes | 车牌号 | |
| ProviderId | int? | Yes | 供应商ID | FK to Provider, optional |
| MaterialId | int? | Yes | 物料ID | FK to MaterialDefinition, optional |

**Audit Fields** (from `FullAuditedEntity<long>`):
- CreationTime
- CreatorId
- LastModificationTime
- LastModifierId
- IsDeleted
- DeletionTime
- DeleterId

**Relationships**:
- Many-to-One: `Provider` (via `ProviderId`, optional)
- Many-to-One: `MaterialDefinition` (via `MaterialId`, optional)
- Many-to-Many: `AttachmentFile` (via `WeighingRecordAttachment`)

**Validation Rules**:
- `Weight` must be >= 0
- If `ProviderId` is provided, it must reference an existing Provider
- If `MaterialId` is provided, it must reference an existing MaterialDefinition

---

### 6. AttachmentFile (附件文件)

**Base Class**: `FullAuditedEntity<int>`  
**Namespace**: `MaterialClient.Common.Entities`  
**Table Name**: `AttachmentFiles`

**Fields**:

| Field Name | Type | Nullable | Description | Notes |
|------------|------|----------|-------------|-------|
| Id | int | No | 主键 | 自增 |
| FileName | string | No | 文件名 | |
| LocalPath | string | No | 本地路径 | |
| OssFullPath | string? | Yes | OSS完整路径 | |
| AttachType | AttachType | No | 附件类型 | 枚举类型 |

**Audit Fields** (from `FullAuditedEntity<int>`):
- CreationTime
- CreatorId
- LastModificationTime
- LastModifierId
- IsDeleted
- DeletionTime
- DeleterId

**Relationships**:
- Many-to-Many: `Waybill` (via `WaybillAttachment`)
- Many-to-Many: `WeighingRecord` (via `WeighingRecordAttachment`)

**Validation Rules**:
- `FileName` must not be empty
- `LocalPath` must not be empty

---

### 7. WaybillAttachment (运单-附件关联)

**Base Class**: `Entity<int>`  
**Namespace**: `MaterialClient.Common.Entities`  
**Table Name**: `WaybillAttachments`

**Fields**:

| Field Name | Type | Nullable | Description | Notes |
|------------|------|----------|-------------|-------|
| Id | int | No | 主键 | 自增 |
| WaybillId | long | No | 运单ID | FK to Waybill |
| AttachmentFileId | int | No | 附件文件ID | FK to AttachmentFile |

**Relationships**:
- Many-to-One: `Waybill` (via `WaybillId`)
- Many-to-One: `AttachmentFile` (via `AttachmentFileId`)

**Validation Rules**:
- `WaybillId` must reference an existing Waybill
- `AttachmentFileId` must reference an existing AttachmentFile
- Unique constraint on (`WaybillId`, `AttachmentFileId`) to prevent duplicates

---

### 8. WeighingRecordAttachment (称重记录-附件关联)

**Base Class**: `Entity<int>`  
**Namespace**: `MaterialClient.Common.Entities`  
**Table Name**: `WeighingRecordAttachments`

**Fields**:

| Field Name | Type | Nullable | Description | Notes |
|------------|------|----------|-------------|-------|
| Id | int | No | 主键 | 自增 |
| WeighingRecordId | long | No | 称重记录ID | FK to WeighingRecord |
| AttachmentFileId | int | No | 附件文件ID | FK to AttachmentFile |

**Relationships**:
- Many-to-One: `WeighingRecord` (via `WeighingRecordId`)
- Many-to-One: `AttachmentFile` (via `AttachmentFileId`)

**Validation Rules**:
- `WeighingRecordId` must reference an existing WeighingRecord
- `AttachmentFileId` must reference an existing AttachmentFile
- Unique constraint on (`WeighingRecordId`, `AttachmentFileId`) to prevent duplicates

---

## Enums

### OffsetResultType

**Namespace**: `MaterialClient.Common.Entities.Enums`  
**Underlying Type**: `short`

| Value | Name | Description (Chinese) |
|-------|------|----------------------|
| 0 | Default | 默认 |
| 1 | OverPositiveDeviation | 超正差 |
| 2 | Normal | 正常 |
| 3 | OverNegativeDeviation | 超负差 |

---

### OrderSource

**Namespace**: `MaterialClient.Common.Entities.Enums`  
**Underlying Type**: `short`

| Value | Name | Description (Chinese) |
|-------|------|----------------------|
| 1 | MannedStation | 有人值守 |
| 2 | ManualEntry | 补录 |
| 3 | MobileAcceptance | 移动验收 |
| 4 | UnmannedStation | 无人值守 |

---

### AttachType

**Namespace**: `MaterialClient.Common.Entities.Enums`  
**Underlying Type**: `short`

| Value | Name | Description (Chinese) |
|-------|------|----------------------|
| 0 | EntryPhoto | 进场照片 |
| 1 | ExitPhoto | 出场照片 |
| 2 | TicketPhoto | 票据照片 |

---

## EF Core Configuration

### DbContext Updates

**File**: `MaterialClient.Common/EFCore/MaterialClientDbContext.cs`

```csharp
public class MaterialClientDbContext : AbpDbContext<MaterialClientDbContext>
{
    public MaterialClientDbContext(DbContextOptions<MaterialClientDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<MaterialDefinition> MaterialDefinitions { get; set; }
    public DbSet<MaterialUnit> MaterialUnits { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Waybill> Waybills { get; set; }
    public DbSet<WeighingRecord> WeighingRecords { get; set; }
    public DbSet<AttachmentFile> AttachmentFiles { get; set; }
    public DbSet<WaybillAttachment> WaybillAttachments { get; set; }
    public DbSet<WeighingRecordAttachment> WeighingRecordAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure relationships
        ConfigureMaterialDefinition(modelBuilder);
        ConfigureMaterialUnit(modelBuilder);
        ConfigureProvider(modelBuilder);
        ConfigureWaybill(modelBuilder);
        ConfigureWeighingRecord(modelBuilder);
        ConfigureAttachmentFile(modelBuilder);
        ConfigureWaybillAttachment(modelBuilder);
        ConfigureWeighingRecordAttachment(modelBuilder);
    }
}
```

### Relationship Configuration

- **MaterialUnit → MaterialDefinition**: Required foreign key
- **MaterialUnit → Provider**: Optional foreign key
- **Waybill → Provider**: Required foreign key
- **WeighingRecord → Provider**: Optional foreign key
- **WeighingRecord → MaterialDefinition**: Optional foreign key
- **WaybillAttachment**: Composite unique constraint on (`WaybillId`, `AttachmentFileId`)
- **WeighingRecordAttachment**: Composite unique constraint on (`WeighingRecordId`, `AttachmentFileId`)

---

## Repository Interfaces

所有实体使用ABP框架提供的`IRepository<TEntity, TKey>`接口：

- `IRepository<MaterialDefinition, int>`
- `IRepository<MaterialUnit, int>`
- `IRepository<Provider, int>`
- `IRepository<Waybill, long>`
- `IRepository<WeighingRecord, long>`
- `IRepository<AttachmentFile, int>`
- `IRepository<WaybillAttachment, int>`
- `IRepository<WeighingRecordAttachment, int>`

无需创建自定义Repository类，ABP框架自动提供实现。

