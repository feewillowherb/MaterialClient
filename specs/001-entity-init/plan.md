# Implementation Plan: 物料系统实体初始化

**Branch**: `001-entity-init` | **Date**: 2025-01-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-entity-init/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

本功能需要为物料管理系统定义6个核心实体（物料定义、物料单位、供应商、运单、称重记录、附件文件）及其关联关系。实体将使用ABP框架的DDD基础设施，遵循领域驱动设计原则，通过EF Core和SQLite进行数据持久化。所有实体字段名必须使用英文，枚举值的中文描述通过特性或注释处理。

## Technical Context

**Language/Version**: C# .NET 9.0  
**Primary Dependencies**: Volo.Abp.Ddd.Domain 9.3.6, Volo.Abp.EntityFrameworkCore.Sqlite 9.3.6, Microsoft.EntityFrameworkCore.Sqlite 9.0.10  
**Storage**: SQLite (加密数据库，使用SQLitePCLRaw.bundle_zetetic)  
**Testing**: xUnit (project uses .NET testing framework)  
**Target Platform**: Windows x64 (win-x64)  
**Project Type**: Single project (MaterialClient.Common库)  
**Performance Goals**: N/A (实体定义阶段，不涉及性能要求)  
**Constraints**: 
- 代码中变量名和字段必须是英文字符，禁止使用中文字符
- 代码中除了注释，不能出现中文字符
- 实体必须继承ABP基类（Entity<TKey>或FullAuditedEntity<TKey>）
- DbContext必须继承自AbpDbContext<TDbContext>
- 必须使用IRepository<TEntity, TKey>接口访问数据
**Scale/Scope**: 6个实体类，3个枚举类型，6个Repository接口，2个关联表

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ Core Principles Compliance

1. **ABP EntityFrameworkCore Sqlite包**: ✅ 已引用 `Volo.Abp.EntityFrameworkCore.Sqlite` 9.3.6
2. **DbContext基类**: ⚠️ 当前MaterialClientDbContext继承自DbContext，需要改为继承`AbpDbContext<MaterialClientDbContext>`
3. **仓储模式**: ✅ 将使用`IRepository<TEntity, TKey>`接口，符合ABP规范
4. **DDD实体基类**: ✅ 实体将继承`Volo.Abp.Domain.Entities.Entity<TKey>`或`FullAuditedEntity<TKey>`
5. **代码字符约束**: ✅ 所有字段名将使用英文，中文字符仅出现在注释中
6. **命名空间**: ✅ 使用`Volo.Abp.Domain.Entities`命名空间下的基类

### ⚠️ Required Changes

1. **MaterialClientDbContext迁移**: 需要将`MaterialClientDbContext`从继承`DbContext`改为继承`AbpDbContext<MaterialClientDbContext>`，并更新配置方式
2. **DbContext配置**: 需要使用`AddAbpDbContext<TDbContext>(options => options.UseSqlite(...))`进行配置，而不是直接在`OnConfiguring`中配置

### ✅ Post-Phase 1 Re-check

**Phase 1设计完成后重新评估**:

1. ✅ **DbContext基类迁移**: 已在data-model.md中定义，需要将`MaterialClientDbContext`改为继承`AbpDbContext<MaterialClientDbContext>`
2. ✅ **DbContext配置**: 已在quickstart.md中说明，需要在应用启动时使用`AddAbpDbContext`配置
3. ✅ **实体基类**: 已在data-model.md中明确指定（业务实体使用`FullAuditedEntity`，配置实体使用`Entity`）
4. ✅ **代码字符约束**: 已在data-model.md和quickstart.md中明确所有字段名使用英文
5. ✅ **仓储模式**: 已在data-model.md中明确使用`IRepository<TEntity, TKey>`接口

**所有宪法检查项均符合要求，可以进入实施阶段。**

## Project Structure

### Documentation (this feature)

```text
specs/001-entity-init/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
└── contracts/           # Phase 1 output (本功能不需要API contracts)
```

### Source Code (repository root)

```text
MaterialClient.Common/
├── Entities/                    # 实体类目录
│   ├── MaterialDefinition.cs   # 物料定义实体
│   ├── MaterialUnit.cs          # 物料单位实体
│   ├── Provider.cs              # 供应商实体
│   ├── Waybill.cs               # 运单实体
│   ├── WeighingRecord.cs        # 称重记录实体
│   ├── AttachmentFile.cs        # 附件文件实体
│   ├── WaybillAttachment.cs     # 运单-附件关联实体
│   └── WeighingRecordAttachment.cs  # 称重记录-附件关联实体
├── Entities/Enums/              # 枚举类型目录
│   ├── OffsetResultType.cs      # 偏移结果类型
│   ├── OrderSource.cs           # 订单来源
│   └── AttachType.cs            # 附件类型
├── EFCore/
│   └── MaterialClientDbContext.cs           # DbContext（需要更新为继承AbpDbContext）
└── Repositories/                # Repository接口目录（如需要自定义）
    └── [Repository接口，如需要扩展ABP默认仓储]
```

**Structure Decision**: 
- 使用MaterialClient.Common项目作为共享库
- 实体放在`Entities/`目录下
- 枚举类型放在`Entities/Enums/`目录下
- 关联实体也放在`Entities/`目录下
- 遵循ABP框架的Repository模式，使用`IRepository<TEntity, TKey>`接口，无需创建自定义Repository类（除非需要扩展功能）

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | 无违反 | 符合所有架构约束 |
