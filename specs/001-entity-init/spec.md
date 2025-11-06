# Feature Specification: 物料系统实体初始化

**Feature Branch**: `001-entity-init`  
**Created**: 2025-01-30  
**Status**: Draft  
**Input**: User description: "Entity init"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 定义核心物料实体 (Priority: P1)

作为系统架构师，我需要定义物料系统的核心实体（物料定义、物料单位、供应商），以便系统能够存储和管理物料的基本信息。

**Why this priority**: 这些是物料系统的基础实体，其他实体和业务功能都依赖于这些核心定义。没有这些实体，系统无法进行物料管理。

**Independent Test**: 可以通过创建实体类和验证所有必需字段都存在来独立测试。完成此功能后，系统将具备存储物料基本信息的能力。

**Acceptance Scenarios**:

1. **Given** 系统需要定义物料信息，**When** 创建物料定义实体，**Then** 实体包含所有必需字段（Id, Name, Brand, Size, UpperLimit, LowerLimit, BasicUnit, Code, CoId, Specifications, ProId, UnitName, UnitRate）
2. **Given** 系统需要定义物料单位，**When** 创建物料单位实体，**Then** 实体包含所有必需字段（Id, 物料Id, UnitName, Rate, ProviderId, RateName）并能关联到物料定义
3. **Given** 系统需要管理供应商信息，**When** 创建供应商实体，**Then** 实体包含所有必需字段（Id, ProviderType, ProviderName, ContectName, ContectPhone）

---

### User Story 2 - 定义业务实体及其关联 (Priority: P2)

作为系统架构师，我需要定义运单和称重记录实体，以便系统能够管理物料运输和称重业务。

**Why this priority**: 这些实体支持核心业务流程（运输和称重），是物料管理系统的关键功能组件。

**Independent Test**: 可以通过创建实体类、定义枚举类型和验证字段完整性来独立测试。完成此功能后，系统将具备记录运输和称重信息的能力。

**Acceptance Scenarios**:

1. **Given** 系统需要管理运单信息，**When** 创建运单实体，**Then** 实体包含所有必需字段（包括所有业务字段和枚举类型 OffsetResultType、OrderSource）
2. **Given** 系统需要记录称重数据，**When** 创建称重记录实体，**Then** 实体包含所有必需字段（Id, weight, PlateNumber, ProviderId, 物料Id）
3. **Given** 运单和称重记录需要关联供应商和物料，**When** 创建实体，**Then** 实体通过外键正确关联到供应商和物料定义实体

---

### User Story 3 - 定义附件实体和关联关系 (Priority: P3)

作为系统架构师，我需要定义附件文件实体，并建立其与运单和称重记录的关联关系，以便系统能够存储和管理业务相关的附件文件。

**Why this priority**: 附件功能支持业务合规性和审计需求，但相比核心业务功能优先级较低。

**Independent Test**: 可以通过创建附件实体、定义关联表并验证一对多关系来独立测试。完成此功能后，系统将具备存储和管理业务附件的能力。

**Acceptance Scenarios**:

1. **Given** 系统需要存储附件文件，**When** 创建附件文件实体，**Then** 实体包含所有必需字段（Id, FileName, LocalPath, OssFullPath, AttachType）和正确的枚举类型定义
2. **Given** 称重记录需要关联多个附件，**When** 创建关联关系，**Then** 系统能够建立称重记录与附件文件的一对多关系
3. **Given** 运单需要关联多个附件，**When** 创建关联关系，**Then** 系统能够建立运单与附件文件的一对多关系

---

### Edge Cases

- 如何处理实体字段的可空性（nullable fields）？
- 如何处理枚举类型的中文值映射？
- 如何处理实体之间的级联删除关系？
- 如何处理关联表中的重复关联？

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系统必须定义物料定义实体，包含所有指定字段（Id, Name, Brand, Size, UpperLimit, LowerLimit, BasicUnit, Code, CoId, Specifications, ProId, UnitName, UnitRate）
- **FR-002**: 系统必须定义物料单位实体，包含所有指定字段（Id, 物料Id, UnitName, Rate, ProviderId, RateName）
- **FR-003**: 系统必须定义供应商实体，包含所有指定字段（Id, ProviderType, ProviderName, ContectName, ContectPhone）
- **FR-004**: 系统必须定义运单实体，包含所有指定字段和枚举类型（OffsetResultType, OrderSource）
- **FR-005**: 系统必须定义称重记录实体，包含所有指定字段（Id, weight, PlateNumber, ProviderId, 物料Id）
- **FR-006**: 系统必须定义附件文件实体，包含所有指定字段和枚举类型（AttachType）
- **FR-007**: 系统必须建立称重记录与附件文件的一对多关联关系
- **FR-008**: 系统必须建立运单与附件文件的一对多关联关系
- **FR-009**: 系统必须为每个实体创建对应的Repository类
- **FR-010**: 系统必须创建必要的关联表以支持实体间的关系

### Key Entities *(include if feature involves data)*

- **物料定义实体 (Material Definition Entity)**: 表示物料的基本定义信息，包含物料名称、品牌、规格、上下限、单位等属性。这是物料系统的核心实体。
- **物料单位实体 (Material Unit Entity)**: 表示物料的单位信息，包含单位名称、换算率等。通过物料Id关联到物料定义实体。
- **供应商实体 (Provider Entity)**: 表示供应商信息，包含供应商类型、名称、联系人等。与其他业务实体（运单、称重记录）关联。
- **运单实体 (Waybill Entity)**: 表示运输订单信息，包含订单号、车牌号、时间、重量等业务字段。通过ProviderId关联到供应商实体，并通过关联表与附件文件实体建立一对多关系。
- **称重记录实体 (Weighing Record Entity)**: 表示称重记录信息，包含重量、车牌号等。通过ProviderId关联到供应商实体，通过物料Id关联到物料定义实体，并通过关联表与附件文件实体建立一对多关系。
- **附件文件实体 (Attachment File Entity)**: 表示附件文件信息，包含文件名、路径、类型等。通过关联表与运单和称重记录建立多对一关系。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 所有6个核心实体（物料定义、物料单位、供应商、运单、称重记录、附件文件）均已正确定义，包含所有必需字段
- **SC-002**: 所有枚举类型（OffsetResultType, OrderSource, AttachType）均已正确定义，包含所有枚举值
- **SC-003**: 称重记录与附件文件的一对多关联关系已正确建立，支持一个称重记录关联多个附件文件
- **SC-004**: 运单与附件文件的一对多关联关系已正确建立，支持一个运单关联多个附件文件
- **SC-005**: 所有实体的Repository类已创建，符合系统架构规范
- **SC-006**: 所有必要的关联表已创建，支持实体间的所有关系定义

## Assumptions

- 实体字段的数据类型按照需求描述中的定义（int, long, string, decimal?, DateTime?, bool, enum等）
- 枚举类型使用short作为底层类型
- 实体之间的外键关系通过导航属性或外键字段实现
- Repository类遵循系统现有的仓储模式约定
- 关联表命名遵循系统约定（如：WaybillAttachments, WeighingRecordAttachments）
- 此功能仅包含实体和Repository定义，不包含CRUD操作或Service层的实现

## Scope

### In Scope

- 定义所有6个实体类的结构和字段
- 定义所有枚举类型
- 创建实体的Repository接口/类
- 创建实体间关联所需的关联表结构

### Out of Scope

- CRUD操作的实现
- Service层的业务逻辑
- 数据验证规则的具体实现
- 数据库迁移脚本
- API接口定义
- 用户界面相关功能
