# Tasks: 物料系统实体初始化

**Input**: Design documents from `/specs/001-entity-init/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md

**Tests**: Tests are NOT included as they are not explicitly requested in the feature specification. This feature only defines entities and Repository interfaces (provided by ABP framework).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `MaterialClient.Common/` at repository root
- Entities: `MaterialClient.Common/Entities/`
- Enums: `MaterialClient.Common/Entities/Enums/`
- DbContext: `MaterialClient.Common/EFCore/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and DbContext migration to ABP framework

- [x] T001 Update MaterialClientDbContext to inherit from AbpDbContext in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T002 [P] Add constructor to MaterialClientDbContext receiving DbContextOptions in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T003 Remove OnConfiguring method from MaterialClientDbContext in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T004 Create Entities directory structure in MaterialClient.Common/Entities/
- [x] T005 [P] Create Entities/Enums directory structure in MaterialClient.Common/Entities/Enums/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core enumerations that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 [P] Create OffsetResultType enum in MaterialClient.Common/Entities/Enums/OffsetResultType.cs
- [x] T007 [P] Create OrderSource enum in MaterialClient.Common/Entities/Enums/OrderSource.cs
- [x] T008 [P] Create AttachType enum in MaterialClient.Common/Entities/Enums/AttachType.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - 定义核心物料实体 (Priority: P1) 🎯 MVP

**Goal**: Define core material entities (MaterialDefinition, MaterialUnit, Provider) to enable storage and management of basic material information.

**Independent Test**: Create entity classes and verify all required fields exist. After completion, the system will have the capability to store basic material information.

### Implementation for User Story 1

- [x] T009 [P] [US1] Create MaterialDefinition entity inheriting Entity<int> in MaterialClient.Common/Entities/MaterialDefinition.cs
- [x] T010 [P] [US1] Create Provider entity inheriting Entity<int> in MaterialClient.Common/Entities/Provider.cs
- [x] T011 [US1] Create MaterialUnit entity inheriting Entity<int> in MaterialClient.Common/Entities/MaterialUnit.cs (depends on T009, T010)
- [x] T012 [US1] Add DbSet properties for MaterialDefinition, MaterialUnit, Provider in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T013 [US1] Configure MaterialDefinition relationship in OnModelCreating in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T014 [US1] Configure MaterialUnit relationships (to MaterialDefinition and Provider) in OnModelCreating in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T015 [US1] Configure Provider relationship in OnModelCreating in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T016 [US1] Verify all required fields are present in MaterialDefinition entity per spec.md acceptance scenario 1
- [x] T017 [US1] Verify all required fields are present in MaterialUnit entity per spec.md acceptance scenario 2
- [x] T018 [US1] Verify all required fields are present in Provider entity per spec.md acceptance scenario 3

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. The system can store and manage basic material information.

---

## Phase 4: User Story 2 - 定义业务实体及其关联 (Priority: P2)

**Goal**: Define waybill and weighing record entities to enable management of material transportation and weighing business.

**Independent Test**: Create entity classes, define enumeration types, and verify field completeness. After completion, the system will have the capability to record transportation and weighing information.

### Implementation for User Story 2

- [x] T019 [P] [US2] Create Waybill entity inheriting FullAuditedEntity<long> in MaterialClient.Common/Entities/Waybill.cs
- [x] T020 [P] [US2] Create WeighingRecord entity inheriting FullAuditedEntity<long> in MaterialClient.Common/Entities/WeighingRecord.cs
- [x] T021 [US2] Add DbSet properties for Waybill and WeighingRecord in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T022 [US2] Configure Waybill relationship to Provider in OnModelCreating in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T023 [US2] Configure WeighingRecord relationships (to Provider and MaterialDefinition) in OnModelCreating in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T024 [US2] Verify all required fields including OffsetResultType and OrderSource enums are present in Waybill entity per spec.md acceptance scenario 1
- [x] T025 [US2] Verify all required fields are present in WeighingRecord entity per spec.md acceptance scenario 2
- [x] T026 [US2] Verify foreign key associations from Waybill and WeighingRecord to Provider and MaterialDefinition per spec.md acceptance scenario 3

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently. The system can record transportation and weighing information.

---

## Phase 5: User Story 3 - 定义附件实体和关联关系 (Priority: P3)

**Goal**: Define attachment file entity and establish its relationships with waybill and weighing record to enable storage and management of business-related attachment files.

**Independent Test**: Create attachment entity, define association tables, and verify one-to-many relationships. After completion, the system will have the capability to store and manage business attachments.

### Implementation for User Story 3

- [x] T027 [P] [US3] Create AttachmentFile entity inheriting FullAuditedEntity<int> in MaterialClient.Common/Entities/AttachmentFile.cs
- [x] T028 [P] [US3] Create WaybillAttachment association entity inheriting Entity<int> in MaterialClient.Common/Entities/WaybillAttachment.cs
- [x] T029 [P] [US3] Create WeighingRecordAttachment association entity inheriting Entity<int> in MaterialClient.Common/Entities/WeighingRecordAttachment.cs
- [x] T030 [US3] Add DbSet properties for AttachmentFile, WaybillAttachment, WeighingRecordAttachment in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T031 [US3] Configure AttachmentFile relationship in OnModelCreating in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T032 [US3] Configure WaybillAttachment relationships (to Waybill and AttachmentFile) with composite unique constraint in OnModelCreating in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T033 [US3] Configure WeighingRecordAttachment relationships (to WeighingRecord and AttachmentFile) with composite unique constraint in OnModelCreating in MaterialClient.Common/EFCore/MaterialClientDbContext.cs
- [x] T034 [US3] Verify all required fields including AttachType enum are present in AttachmentFile entity per spec.md acceptance scenario 1
- [x] T035 [US3] Verify one-to-many relationship between WeighingRecord and AttachmentFile via WeighingRecordAttachment per spec.md acceptance scenario 2
- [x] T036 [US3] Verify one-to-many relationship between Waybill and AttachmentFile via WaybillAttachment per spec.md acceptance scenario 3

**Checkpoint**: All user stories should now be independently functional. The system can store and manage business attachments with proper relationships.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and documentation updates

- [x] T037 [P] Verify all 6 core entities are correctly defined with all required fields per SC-001
- [x] T038 [P] Verify all 3 enum types are correctly defined with all enum values per SC-002
- [x] T039 [P] Verify WeighingRecord to AttachmentFile one-to-many relationship is correctly established per SC-003
- [x] T040 [P] Verify Waybill to AttachmentFile one-to-many relationship is correctly established per SC-004
- [x] T041 Verify all entities can use IRepository<TEntity, TKey> interface per SC-005 (ABP framework provides implementation automatically)
- [x] T042 Verify all necessary association tables are created to support all entity relationships per SC-006
- [x] T043 [P] Add XML documentation comments to all entity classes in MaterialClient.Common/Entities/
- [x] T044 [P] Add XML documentation comments to all enum types in MaterialClient.Common/Entities/Enums/
- [x] T045 Run quickstart.md validation to ensure implementation matches documentation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Depends on US1 entities (Provider, MaterialDefinition) for foreign key relationships
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Depends on US2 entities (Waybill, WeighingRecord) for association relationships

### Within Each User Story

- Entities before DbContext configuration
- DbContext configuration before relationship validation
- Core entities before association entities (for US3)
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes:
  - User Story 1 entities (MaterialDefinition, Provider) can be created in parallel
  - User Story 2 entities (Waybill, WeighingRecord) can be created in parallel after US1
  - User Story 3 entities (AttachmentFile, WaybillAttachment, WeighingRecordAttachment) can be created in parallel after US2
- Different user stories can be worked on sequentially by the same developer (due to dependencies)
- Polish phase validation tasks marked [P] can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all core entities for User Story 1 together:
Task: "Create MaterialDefinition entity inheriting Entity<int> in MaterialClient.Common/Entities/MaterialDefinition.cs"
Task: "Create Provider entity inheriting Entity<int> in MaterialClient.Common/Entities/Provider.cs"

# After MaterialDefinition and Provider are created, create MaterialUnit:
Task: "Create MaterialUnit entity inheriting Entity<int> in MaterialClient.Common/Entities/MaterialUnit.cs"
```

---

## Parallel Example: User Story 2

```bash
# Launch all business entities for User Story 2 together (after US1):
Task: "Create Waybill entity inheriting FullAuditedEntity<long> in MaterialClient.Common/Entities/Waybill.cs"
Task: "Create WeighingRecord entity inheriting FullAuditedEntity<long> in MaterialClient.Common/Entities/WeighingRecord.cs"
```

---

## Parallel Example: User Story 3

```bash
# Launch all attachment entities for User Story 3 together (after US2):
Task: "Create AttachmentFile entity inheriting FullAuditedEntity<int> in MaterialClient.Common/Entities/AttachmentFile.cs"
Task: "Create WaybillAttachment association entity inheriting Entity<int> in MaterialClient.Common/Entities/WaybillAttachment.cs"
Task: "Create WeighingRecordAttachment association entity inheriting Entity<int> in MaterialClient.Common/Entities/WeighingRecordAttachment.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently by verifying entity classes and fields
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Verify independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Verify independently → Deploy/Demo
4. Add User Story 3 → Verify independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Sequential Implementation (Recommended)

Since this feature involves entity definitions with dependencies:

1. Developer completes Setup + Foundational together
2. Once Foundational is done:
   - Complete User Story 1 (core entities)
   - Then complete User Story 2 (depends on US1 entities)
   - Then complete User Story 3 (depends on US2 entities)
3. Each story completes and validates independently

**Note**: While stories have dependencies, each story can still be independently tested and validated once its required dependencies are complete.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
- **Repository Interfaces**: ABP framework automatically provides IRepository<TEntity, TKey> implementations - no custom repository classes needed
- **DbContext Configuration**: Application startup configuration must use AddAbpDbContext<MaterialClientDbContext> - see research.md Q1
- **Entity Base Classes**: Follow research.md Q2 - use FullAuditedEntity for business entities, Entity for config entities
- **Enum Values**: Use English names in code, Chinese descriptions in comments - see research.md Q4
- **Field Naming**: All field names must use English - see research.md Q5 for mapping rules

---

## Task Summary

- **Total Tasks**: 45
- **Phase 1 (Setup)**: 5 tasks
- **Phase 2 (Foundational)**: 3 tasks
- **Phase 3 (User Story 1)**: 10 tasks
- **Phase 4 (User Story 2)**: 8 tasks
- **Phase 5 (User Story 3)**: 10 tasks
- **Phase 6 (Polish)**: 9 tasks

- **Parallel Opportunities**: 18 tasks marked [P]
- **Suggested MVP Scope**: Phase 1 + Phase 2 + Phase 3 (User Story 1 only)
- **Independent Test Criteria**: Each user story has clear acceptance scenarios in spec.md

