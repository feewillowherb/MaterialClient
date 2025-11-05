# Research: 物料系统实体初始化

**Date**: 2025-01-30  
**Feature**: [spec.md](./spec.md) | [plan.md](./plan.md)

## Research Questions & Decisions

### Q1: DbContext迁移到ABP框架

**Question**: 如何将现有的`MaterialClientDbContext`从继承`DbContext`迁移到继承`AbpDbContext<MaterialClientDbContext>`，并正确配置SQLite连接？

**Decision**: 
- 将`MaterialClientDbContext`改为继承`Volo.Abp.EntityFrameworkCore.AbpDbContext<MaterialClientDbContext>`
- 移除`OnConfiguring`方法，改为在应用启动时通过`AddAbpDbContext<TDbContext>(options => options.UseSqlite(...))`配置
- 保持SQLite加密配置（使用SQLitePCLRaw.bundle_zetetic）
- 在`MaterialClientDbContext`构造函数中接收`DbContextOptions<MaterialClientDbContext>`参数

**Rationale**: 
- ABP框架要求在应用启动时配置DbContext，而不是在DbContext内部配置
- 这样可以充分利用ABP的依赖注入和配置管理
- 保持与ABP框架其他组件的兼容性

**Alternatives considered**:
- 继续使用`OnConfiguring`方法：❌ 与ABP框架规范不符，无法使用ABP的DbContext配置扩展
- 使用`AddDbContext`而非`AddAbpDbContext`：❌ 无法获得ABP的审计、多租户等特性支持

**Implementation Notes**:
- 需要在应用启动模块中配置：`services.AddAbpDbContext<MaterialClientDbContext>(options => options.UseSqlite(connectionString))`
- DbContext构造函数签名：`public MaterialClientDbContext(DbContextOptions<MaterialClientDbContext> options) : base(options)`

---

### Q2: 实体基类选择

**Question**: 应该使用`Entity<TKey>`还是`FullAuditedEntity<TKey>`作为实体基类？

**Decision**: 
- 核心业务实体（运单、称重记录）使用`FullAuditedEntity<TKey>`，因为这些实体需要审计追踪（创建时间、修改时间等）
- 基础配置实体（物料定义、供应商）使用`Entity<TKey>`，因为这些是配置数据，不需要审计
- 关联实体（WaybillAttachment、WeighingRecordAttachment）使用`Entity<TKey>`

**Rationale**: 
- 审计字段对于业务实体（运单、称重记录）很重要，可以追踪数据变更历史
- 配置类实体（物料定义、供应商）变更频率低，不需要详细审计
- 关联实体通常是简单的关联关系，不需要审计

**Alternatives considered**:
- 所有实体都使用`FullAuditedEntity`：❌ 过度设计，配置类实体不需要审计
- 所有实体都使用`Entity`：❌ 业务实体缺少审计能力，不符合审计需求

**Implementation Notes**:
- 物料定义（MaterialDefinition）：`Entity<int>`
- 物料单位（MaterialUnit）：`Entity<int>`
- 供应商（Provider）：`Entity<int>`
- 运单（Waybill）：`FullAuditedEntity<long>`
- 称重记录（WeighingRecord）：`FullAuditedEntity<long>`
- 附件文件（AttachmentFile）：`FullAuditedEntity<int>`
- 关联实体：`Entity<int>`或使用复合主键

---

### Q3: 一对多关系的实现方式

**Question**: 称重记录/运单与附件文件的一对多关系，应该使用导航属性还是显式的关联表实体？

**Decision**: 
- 使用显式的关联实体（`WaybillAttachment`、`WeighingRecordAttachment`）而不是简单的导航属性集合
- 关联实体包含外键字段和可能的额外属性（如创建时间等）

**Rationale**: 
- 显式关联实体更清晰，便于未来扩展（如添加关联的元数据）
- 符合领域驱动设计原则，关联关系也是领域概念
- 便于查询和筛选特定的关联关系
- 如果需要审计，可以更容易地添加审计字段

**Alternatives considered**:
- 使用导航属性集合（`Waybill.Attachments`）：❌ 不够灵活，难以扩展，不利于查询特定关联
- 使用中间表但不创建实体类：❌ 不符合DDD原则，难以扩展和维护

**Implementation Notes**:
- `WaybillAttachment`实体包含：`WaybillId`（long）、`AttachmentFileId`（int）、可选的时间戳等
- `WeighingRecordAttachment`实体包含：`WeighingRecordId`（long）、`AttachmentFileId`（int）、可选的时间戳等
- 使用EF Core的Fluent API配置关联关系

---

### Q4: 枚举类型的中文值处理

**Question**: 枚举类型包含中文值（如"默认"、"超正差"等），如何在代码中使用英文，同时在数据库中存储或显示中文？

**Decision**: 
- 枚举值在代码中使用英文名称（如`Default`, `OverPositiveDeviation`, `Normal`, `OverNegativeDeviation`）
- 使用`[Description]`特性或常量字符串存储中文描述
- 在EF Core中，枚举存储为short类型（底层类型）
- 通过扩展方法或转换器在需要时转换为中文显示

**Rationale**: 
- 符合代码字符约束（代码中不能使用中文字符）
- 枚举值在代码中使用英文更符合编程规范
- 中文描述通过特性或注释提供，不影响代码逻辑
- EF Core支持枚举类型直接映射到数据库

**Alternatives considered**:
- 在代码中使用中文枚举值：❌ 违反代码字符约束
- 使用字符串存储枚举：❌ 性能较差，类型安全性低
- 使用常量类替代枚举：❌ 失去类型安全性和IDE支持

**Implementation Notes**:
```csharp
public enum OffsetResultType : short
{
    Default = 0,              // 默认
    OverPositiveDeviation = 1, // 超正差
    Normal = 2,                // 正常
    OverNegativeDeviation = 3   // 超负差
}
```
- 使用`[Description("中文描述")]`特性标记中文描述
- 创建扩展方法`GetDescription()`用于获取中文描述

---

### Q5: 字段名的中英文转换

**Question**: 需求文档中的字段名包含中文（如"物料Id"），如何在代码中转换为英文？

**Decision**: 
- 所有字段名使用英文命名，遵循C#命名规范
- 中文字段名映射关系：
  - `物料Id` → `MaterialId`
  - `ProviderId` → `ProviderId`（已为英文）
  - `ContectName` → `ContactName`（修正拼写错误）
  - `ContectPhone` → `ContactPhone`（修正拼写错误）
  - `weight` → `Weight`（遵循Pascal命名规范）

**Rationale**: 
- 符合代码字符约束和C#命名规范
- 提高代码可读性和可维护性
- 修正需求文档中的拼写错误（Contect → Contact）

**Alternatives considered**:
- 保留中文字段名：❌ 违反代码字符约束和C#规范
- 使用拼音：❌ 可读性差，不符合国际化标准

**Implementation Notes**:
- 所有属性名使用Pascal命名法
- 在注释中说明原始的中文字段名（如需要）
- 在实体类上添加XML注释说明字段含义

---

### Q6: Repository模式实现

**Question**: 是否需要创建自定义Repository接口，还是直接使用ABP的`IRepository<TEntity, TKey>`？

**Decision**: 
- 直接使用ABP框架提供的`IRepository<TEntity, TKey>`接口
- 不创建自定义Repository类，除非有特殊需求
- 通过依赖注入使用仓储：`IRepository<MaterialDefinition, int>`

**Rationale**: 
- ABP框架已经提供了完整的Repository实现，包括基本的CRUD操作
- 符合规范要求（FR-009仅要求创建Repository类，ABP自动提供实现）
- 减少代码重复，提高开发效率
- 需要扩展功能时，可以通过继承或扩展方法实现

**Alternatives considered**:
- 创建自定义Repository接口和实现：❌ 不符合规范要求，增加不必要的复杂性
- 直接使用DbContext：❌ 违反ABP架构原则，不利于测试和维护

**Implementation Notes**:
- 在服务层或应用层通过构造函数注入使用：`IRepository<MaterialDefinition, int>`
- 如果需要扩展功能（如特定查询），可以创建扩展方法或使用LINQ
- 不需要创建Repository文件夹或类（除非有特殊需求）

---

### Q7: 枚举类型命名规范

**Question**: 需求文档中枚举类型名称不一致（`OffsetResultType` vs `eOrderSource`），如何统一命名？

**Decision**: 
- 统一使用`PascalCase`命名，不带前缀
- `OffsetResultType` → `OffsetResultType`（保持不变）
- `eOrderSource` → `OrderSource`（移除`e`前缀）
- `AttachType` → `AttachType`（保持不变）

**Rationale**: 
- 符合C#命名规范和ABP框架约定
- 移除匈牙利命名法前缀（`e`），提高代码可读性
- 保持命名一致性

**Alternatives considered**:
- 保留`eOrderSource`：❌ 不符合C#命名规范
- 使用其他前缀：❌ 不符合ABP框架约定

**Implementation Notes**:
- 所有枚举类型使用`PascalCase`命名
- 枚举值也使用`PascalCase`命名
- 在注释中说明原始枚举名称（如需要）

---

## Summary

所有技术疑问已解决，主要决策包括：

1. ✅ DbContext迁移到ABP框架，使用`AddAbpDbContext`配置
2. ✅ 业务实体使用`FullAuditedEntity`，配置实体使用`Entity`
3. ✅ 使用显式关联实体实现一对多关系
4. ✅ 枚举值使用英文，中文通过特性或注释提供
5. ✅ 所有字段名使用英文，遵循C#命名规范
6. ✅ 直接使用ABP的`IRepository<TEntity, TKey>`接口
7. ✅ 统一枚举类型命名规范

所有[NEEDS CLARIFICATION]标记已解决，可以进入Phase 1设计阶段。

