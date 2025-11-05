# Quick Start: 物料系统实体初始化

**Date**: 2025-01-30  
**Feature**: [spec.md](./spec.md) | [plan.md](./plan.md) | [data-model.md](./data-model.md)

## Overview

本快速开始指南帮助开发者快速理解和实现物料系统实体初始化功能。

## Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 或 Rider
- 已安装ABP框架包：
  - `Volo.Abp.Ddd.Domain` 9.3.6
  - `Volo.Abp.EntityFrameworkCore.Sqlite` 9.3.6
  - `Microsoft.EntityFrameworkCore.Sqlite` 9.0.10

## Implementation Steps

### Step 1: 更新DbContext

**File**: `MaterialClient.Common/EFCore/MaterialClientDbContext.cs`

1. 将基类从`DbContext`改为`AbpDbContext<MaterialClientDbContext>`
2. 添加构造函数接收`DbContextOptions<MaterialClientDbContext>`
3. 移除`OnConfiguring`方法
4. 添加所有实体的`DbSet`属性
5. 重写`OnModelCreating`方法配置关系

**参考**: [data-model.md](./data-model.md) 中的"EF Core Configuration"部分

### Step 2: 创建枚举类型

**Directory**: `MaterialClient.Common/Entities/Enums/`

创建以下枚举文件：
1. `OffsetResultType.cs`
2. `OrderSource.cs`
3. `AttachType.cs`

**参考**: [data-model.md](./data-model.md) 中的"Enums"部分

### Step 3: 创建实体类

**Directory**: `MaterialClient.Common/Entities/`

按以下顺序创建实体类（先创建被依赖的实体）：

1. **MaterialDefinition.cs** - 继承`Entity<int>`
2. **Provider.cs** - 继承`Entity<int>`
3. **MaterialUnit.cs** - 继承`Entity<int>`
4. **Waybill.cs** - 继承`FullAuditedEntity<long>`
5. **WeighingRecord.cs** - 继承`FullAuditedEntity<long>`
6. **AttachmentFile.cs** - 继承`FullAuditedEntity<int>`
7. **WaybillAttachment.cs** - 继承`Entity<int>`
8. **WeighingRecordAttachment.cs** - 继承`Entity<int>`

**参考**: [data-model.md](./data-model.md) 中的"Entities"部分

### Step 4: 配置DbContext关系

在`MaterialClientDbContext.OnModelCreating`方法中配置：
- 所有外键关系
- 唯一约束（关联表的复合唯一约束）
- 索引（如需要）

**参考**: [data-model.md](./data-model.md) 中的"EF Core Configuration"部分

### Step 5: 更新应用启动配置

在应用启动模块中配置DbContext：

```csharp
services.AddAbpDbContext<MaterialClientDbContext>(options =>
{
    options.UseSqlite(connectionString);
});
```

**注意**: 移除DbContext中的`OnConfiguring`方法，改为在应用启动时配置。

## Field Naming Conventions

所有字段名必须使用英文，遵循C#命名规范：

| 原始名称（中文） | 代码名称（英文） |
|----------------|----------------|
| 物料Id | MaterialId |
| ContectName | ContactName |
| ContectPhone | ContactPhone |
| weight | Weight |

## Enum Naming Conventions

枚举值使用英文，中文描述通过注释或`[Description]`特性提供：

| 中文值 | 英文值 |
|--------|--------|
| 默认 | Default |
| 超正差 | OverPositiveDeviation |
| 正常 | Normal |
| 超负差 | OverNegativeDeviation |
| 有人值守 | MannedStation |
| 补录 | ManualEntry |
| 移动验收 | MobileAcceptance |
| 无人值守 | UnmannedStation |
| 进场照片 | EntryPhoto |
| 出场照片 | ExitPhoto |
| 票据照片 | TicketPhoto |

## Entity Base Class Selection

- **业务实体**（Waybill, WeighingRecord, AttachmentFile）: 使用`FullAuditedEntity<TKey>`，需要审计追踪
- **配置实体**（MaterialDefinition, Provider, MaterialUnit）: 使用`Entity<TKey>`，不需要审计
- **关联实体**（WaybillAttachment, WeighingRecordAttachment）: 使用`Entity<TKey>`

## Repository Usage

所有实体通过ABP框架的`IRepository<TEntity, TKey>`接口访问，无需创建自定义Repository类：

```csharp
public class SomeService
{
    private readonly IRepository<MaterialDefinition, int> _materialRepository;
    
    public SomeService(IRepository<MaterialDefinition, int> materialRepository)
    {
        _materialRepository = materialRepository;
    }
}
```

## Testing

实体和关系可以通过以下方式测试：

1. **单元测试**: 测试实体属性、验证规则
2. **集成测试**: 测试EF Core配置、关系映射
3. **数据库测试**: 使用SQLite内存数据库测试完整的数据操作

**参考**: [data-model.md](./data-model.md) 中的"Validation Rules"部分

## Common Issues & Solutions

### Issue 1: DbContext配置错误

**症状**: 应用启动时DbContext配置失败

**解决**: 
- 确保DbContext继承自`AbpDbContext<MaterialClientDbContext>`
- 确保在应用启动时使用`AddAbpDbContext`配置
- 移除`OnConfiguring`方法

### Issue 2: 枚举类型转换错误

**症状**: 枚举值在数据库中存储或读取时出错

**解决**: 
- 确保枚举类型使用`short`作为底层类型
- 在EF Core配置中正确映射枚举类型

### Issue 3: 关联关系配置错误

**症状**: 查询关联数据时出错或数据不完整

**解决**: 
- 检查`OnModelCreating`中的关系配置
- 确保外键字段名称正确
- 检查导航属性的配置

## Next Steps

完成实体定义后，可以：

1. 创建数据库迁移（如果使用EF Core Migrations）
2. 编写单元测试验证实体定义
3. 编写集成测试验证关系映射
4. 在应用服务层使用Repository接口

## References

- [Feature Specification](./spec.md)
- [Implementation Plan](./plan.md)
- [Research Findings](./research.md)
- [Data Model Definition](./data-model.md)

