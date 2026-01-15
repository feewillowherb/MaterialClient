<!--
DOCUMENT_STATUS: SUPERSEDED
LAST_REVIEWED: 2026-01-15
REVIEWER: Claude (OpenSpec Migration)
NOTES: Legacy specification format. Superseded by OpenSpec workflow adopted 2026-01-15. 
       Content preserved for historical reference. For current specifications, see openspec/specs/.
-->

# Tasks: 登录和授权

**Feature**: 002-login-auth  
**Input**: Design documents from `/specs/002-login-auth/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: ✅ **REQUIRED** - TDD is mandated by project constitution. All tests MUST be written FIRST and FAIL before implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Path Conventions

Based on plan.md structure:
- **UI层**: `MaterialClient/` (Avalonia项目)
- **业务逻辑层**: `MaterialClient.Common/` (共享库)
- **测试**: `MaterialClient.Common.Tests/` (集成测试项目)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Install System.Management NuGet package (9.0.1) in MaterialClient.Common project for machine code generation
- [x] T002 [P] Generate AES-256 encryption key and add to appsettings.json under "Encryption:AesKey"
- [x] T003 [P] Add BasePlatform configuration section to MaterialClient/appsettings.json with BaseUrl and ProductCode
- [x] T004 [P] Create Api/Dtos directory in MaterialClient.Common project

**Checkpoint**: ✅ Project dependencies and configuration ready

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### 2.1: API Contract DTOs

- [x] T005 [P] Create HttpResult<T> generic wrapper class in MaterialClient.Common/Api/Dtos/HttpResult.cs
- [x] T006 [P] Create LicenseRequestDto in MaterialClient.Common/Api/Dtos/LicenseRequestDto.cs
- [x] T007 [P] Create LicenseInfoDto in MaterialClient.Common/Api/Dtos/LicenseInfoDto.cs
- [x] T008 [P] Create LoginRequestDto in MaterialClient.Common/Api/Dtos/LoginRequestDto.cs
- [x] T009 [P] Create LoginUserDto in MaterialClient.Common/Api/Dtos/LoginUserDto.cs

### 2.2: Refit API Interface

- [x] T010 Define IBasePlatformApi Refit interface in MaterialClient.Common/Api/IBasePlatformApi.cs with GetAuthClientLicense and UserLogin methods
- [x] T011 Register Refit client in MaterialClientCommonModule.ConfigureServices with retry policy and timeout configuration

### 2.3: Shared Services

- [x] T012 [P] Create IMachineCodeService interface in MaterialClient.Common/Services/Authentication/IMachineCodeService.cs
- [x] T013 [P] Implement MachineCodeService (CPU+Board+MAC hash) in MaterialClient.Common/Services/Authentication/MachineCodeService.cs
- [x] T014 [P] Create IPasswordEncryptionService interface in MaterialClient.Common/Services/Authentication/IPasswordEncryptionService.cs
- [x] T015 [P] Implement PasswordEncryptionService (AES-256-CBC) in MaterialClient.Common/Services/Authentication/PasswordEncryptionService.cs
- [x] T016 Register MachineCodeService and PasswordEncryptionService in MaterialClientCommonModule

### 2.4: Test Infrastructure

- [x] T017 Create Features directory in MaterialClient.Common.Tests for BDD feature files
- [x] T018 [P] Setup test configuration in MaterialClient.Common.Tests/appsettings.json for test database connection

**Checkpoint**: ✅ Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - 首次使用授权激活 (Priority: P1) 🎯 MVP

**Goal**: 用户首次启动时输入授权码激活软件，验证成功后保存授权信息到数据库

**Independent Test**: 启动未授权的应用程序，输入有效授权码，系统验证成功并保存授权信息，然后进入登录页面

### 3.1: Tests for User Story 1 (TDD - RED Phase)

> **TDD流程**: 先写测试 → 测试失败(RED) → 实现代码 → 测试通过(GREEN) → 重构

- [x] T019 [P] [US1] Write BDD feature file in MaterialClient.Common.Tests/Features/Authorization.feature with scenarios for valid/invalid auth codes
- [x] T020 [P] [US1] Create unit test for MachineCodeService in MaterialClient.Common.Tests/MachineCodeServiceTests.cs (verify hash generation)
- [x] T021 [P] [US1] Create integration test for LicenseService in MaterialClient.Common.Tests/LicenseServiceIntegrationTests.cs
- [x] T022 [P] [US1] Create unit test for PasswordEncryptionService in MaterialClient.Common.Tests/PasswordEncryptionServiceTests.cs

**⚠️ Verify all tests FAIL before proceeding to implementation**

### 3.2: Data Model for User Story 1 (GREEN Phase)

- [x] T023 [US1] Create LicenseInfo entity in MaterialClient.Common/Entities/LicenseInfo.cs inheriting Entity<Guid>
- [x] T024 [US1] Add LicenseInfos DbSet to MaterialClientDbContext in MaterialClient.Common/EntityFrameworkCore/MaterialClientDbContext.cs
- [x] T025 [US1] Configure LicenseInfo entity mapping in MaterialClientDbContext.OnModelCreating with indexes and constraints
- [x] T026 [US1] Configure database auto-migration in HttpHost module OnApplicationInitialization

### 3.3: Services for User Story 1 (GREEN Phase)

- [x] T027 [P] [US1] Create ILicenseService interface in MaterialClient.Common/Services/Authentication/ILicenseService.cs
- [x] T028 [US1] Implement LicenseService in MaterialClient.Common/Services/Authentication/LicenseService.cs
- [x] T029 [US1] Implement MachineCodeService in MaterialClient.Common/Services/Authentication/MachineCodeService.cs
- [x] T030 [US1] Register authentication services in MaterialClientCommonModule.ConfigureServices

### 3.4: UI for User Story 1 (GREEN Phase)

- [x] T031 [P] [US1] Create AuthCodeWindow.axaml in MaterialClient/Views/AuthCodeWindow.axaml with authorization code input UI
- [x] T032 [P] [US1] Create AuthCodeWindow.axaml.cs code-behind in MaterialClient/Views/AuthCodeWindow.axaml.cs
- [x] T033 [US1] Create AuthCodeWindowViewModel in MaterialClient/ViewModels/AuthCodeWindowViewModel.cs with ReactiveUI
- [x] T034 [US1] Implement VerifyCommand in AuthCodeWindowViewModel to call LicenseService
- [x] T035 [US1] Add error handling and user feedback in AuthCodeWindowViewModel (ErrorMessage, StatusMessage properties)

### 3.5: Startup Integration for User Story 1 (GREEN Phase)

- [x] T036 [US1] Create StartupService in MaterialClient/Services/StartupService.cs (no interface needed)
- [x] T037 [US1] Implement StartupService.StartupAsync to check license status and show appropriate window
- [x] T038 [US1] Modify App.axaml.cs OnFrameworkInitializationCompleted to use StartupService for window determination
- [x] T039 [US1] Wait for ABP ServiceLocator initialization in App.axaml.cs
- [x] T040 [US1] Services auto-registered via ABP dependency injection (ITransientDependency)

### 3.6: Verification & Refactoring

- [x] T041 [US1] Build verification completed (MaterialClient.Common builds successfully)
- [ ] T042 [US1] Run tests: cd MaterialClient.Common.Tests && dotnet test
- [ ] T043 [US1] Manual test: Launch app, verify authorization window appears, test valid/invalid codes
- [ ] T044 [US1] Refactor: Review code for duplication, improve naming, add XML comments

**Checkpoint**: User Story 1 完全功能 - 授权激活流程可独立测试

---

## Phase 4: User Story 2 - 用户账号密码登录 (Priority: P2)

**Goal**: 用户使用账号和密码登录系统，支持"记住密码"功能

**Independent Test**: 在授权验证通过后，使用正确账号密码登录成功进入主界面，勾选"记住密码"后下次启动自动填充

### 4.1: Tests for User Story 2 (TDD - RED Phase)

- [x] T045 [P] [US2] Write BDD feature file in MaterialClient.Common.Tests/Features/Authentication.feature with login scenarios
- [x] T046 [P] [US2] Create unit test for PasswordEncryptionService in MaterialClient.Common.Tests/PasswordEncryptionServiceTests.cs (encrypt/decrypt)
- [ ] T047 [P] [US2] Create integration test for AuthenticationService.LoginAsync in MaterialClient.Common.Tests/AuthenticationServiceTests.cs
- [ ] T048 [P] [US2] Create integration test for credential storage in MaterialClient.Common.Tests/CredentialStorageTests.cs

**⚠️ Verify all tests FAIL before proceeding to implementation**

### 4.2: Data Model for User Story 2 (GREEN Phase)

- [x] T049 [P] [US2] Create UserCredential entity in MaterialClient.Common/Entities/UserCredential.cs
- [x] T050 [P] [US2] Create UserSession entity in MaterialClient.Common/Entities/UserSession.cs
- [x] T051 [US2] Add UserCredentials and UserSessions DbSets to MaterialClientDbContext
- [x] T052 [US2] Configure UserCredential and UserSession entity mappings in MaterialClientDbContext.OnModelCreating with unique constraints
- [x] T053 [US2] Auto-migration configured (same as T026)

### 4.3: Services for User Story 2 (GREEN Phase)

- [x] T054 [P] [US2] Create IAuthenticationService interface in MaterialClient.Common/Services/Authentication/IAuthenticationService.cs
- [x] T055 [US2] Implement AuthenticationService.LoginAsync in MaterialClient.Common/Services/Authentication/AuthenticationService.cs
- [x] T056 [US2] Implement credential save logic in AuthenticationService.LoginAsync for "remember password"
- [x] T057 [US2] Implement AuthenticationService.GetSavedCredentialAsync to auto-fill login form
- [x] T058 [US2] Implement AuthenticationService.ClearSavedCredentialAsync for login failure handling
- [x] T059 [US2] IAuthenticationService auto-registered via ABP (ITransientDependency)

### 4.4: UI for User Story 2 (GREEN Phase)

- [x] T060 [P] [US2] Create LoginWindow.axaml in MaterialClient/Views/LoginWindow.axaml with login form UI (username, password, remember checkbox)
- [x] T061 [P] [US2] Create LoginWindow.axaml.cs code-behind in MaterialClient/Views/LoginWindow.axaml.cs
- [x] T062 [US2] Create LoginWindowViewModel in MaterialClient/ViewModels/LoginWindowViewModel.cs with login properties
- [x] T063 [US2] Implement LoginCommand in LoginWindowViewModel to call AuthenticationService.LoginAsync
- [x] T064 [US2] Implement auto-fill logic in LoginWindowViewModel constructor using GetSavedCredentialAsync
- [x] T065 [US2] Add error handling and credential clearing on login failure in LoginWindowViewModel
- [x] T066 [US2] Implement window auto-close on success using ReactiveUI property observation

### 4.5: Integration with User Story 1

- [x] T067 [US2] StartupService.StartupAsync shows LoginWindow when license is valid
- [x] T068 [US2] AuthCodeWindow closes and StartupService shows LoginWindow after successful authorization
- [x] T069 [US2] Windows created directly in StartupService (no DI registration needed)

### 4.6: Verification & Refactoring

- [ ] T070 [US2] Run all User Story 2 tests and verify they pass (GREEN phase complete)
- [ ] T071 [US2] Manual test: Complete authorization, login with valid credentials, verify main window opens
- [ ] T072 [US2] Manual test: Verify "remember password" saves and auto-fills on next launch
- [ ] T073 [US2] Manual test: Verify failed login clears saved credentials
- [ ] T074 [US2] Refactor: Extract common ViewModel logic, improve error messages

**Checkpoint**: User Stories 1 AND 2 完全功能 - 授权和登录流程独立测试通过

---

## Phase 5: User Story 3 - 授权过期处理 (Priority: P3)

**Goal**: 当软件授权到期后，系统提示用户重新进行授权验证

**Independent Test**: 修改数据库中的授权到期时间为过去，启动应用程序，验证系统检测到过期并显示授权码窗口

### 5.1: Tests for User Story 3 (TDD - RED Phase)

- [ ] T075 [P] [US3] Add authorization expiry scenarios to MaterialClient.Common.Tests/Features/Authorization.feature
- [ ] T076 [P] [US3] Create integration test for expired license detection in MaterialClient.Common.Tests/AuthorizationServiceTests.cs
- [ ] T077 [P] [US3] Create integration test for project ID change handling in MaterialClient.Common.Tests/AuthorizationServiceTests.cs

**⚠️ Verify all tests FAIL before proceeding to implementation**

### 5.2: Implementation for User Story 3 (GREEN Phase)

- [ ] T078 [US3] Enhance AuthorizationService.CheckLicenseStatusAsync to check AuthEndTime against current time
- [ ] T079 [US3] Implement AuthorizationService.SwitchProjectAsync to clear old project data when ProjectId changes
- [ ] T080 [US3] Update StartupService.DetermineStartupWindowAsync to show AuthCodeWindow for expired licenses
- [ ] T081 [US3] Add logging for authorization expiry events in AuthorizationService

### 5.3: Integration with Existing Stories

- [ ] T082 [US3] Update AuthorizationService.VerifyAuthCodeAsync to call SwitchProjectAsync when ProjectId differs
- [ ] T083 [US3] Update AuthenticationService to participate in project switching (clear old credentials)
- [ ] T084 [US3] Add user-friendly expiry message to AuthCodeWindow UI

### 5.4: Verification & Refactoring

- [ ] T085 [US3] Run all User Story 3 tests and verify they pass (GREEN phase complete)
- [ ] T086 [US3] Manual test: Set AuthEndTime to past date, verify authorization window appears
- [ ] T087 [US3] Manual test: Input new auth code with different ProjectId, verify old data cleared
- [ ] T088 [US3] Refactor: Consolidate project switching logic, add defensive checks

**Checkpoint**: All 3 User Stories 完全功能 - 完整的授权生命周期管理

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

### 6.1: Error Handling & Resilience

- [ ] T089 [P] Implement network error handling with "Retry" button for all API calls across AuthorizationService and AuthenticationService
- [ ] T090 [P] Add user-friendly error messages for all failure scenarios (network, invalid input, server errors)
- [ ] T091 [P] Implement window close handling: AuthCodeWindow closes → app exits (FR-003)

### 6.2: Logging & Observability

- [ ] T092 [P] Add structured logging for all authorization attempts in AuthorizationService (success, failure, expiry)
- [ ] T093 [P] Add structured logging for all login attempts in AuthenticationService (success, failure, credential clear)
- [ ] T094 [P] Add performance logging for API calls (measure response time)

### 6.3: BDD Test Steps Implementation

- [ ] T095 Create Steps.cs in MaterialClient.Common.Tests/ with Reqnroll step definitions for Authorization.feature
- [ ] T096 Create AuthenticationSteps.cs in MaterialClient.Common.Tests/ with step definitions for Authentication.feature
- [ ] T097 Run all BDD scenarios: dotnet test --filter "Category=BDD"

### 6.4: Documentation & Final Validation

- [ ] T098 [P] Update quickstart.md with actual implementation notes and troubleshooting tips
- [ ] T099 [P] Remove or hide original MainWindow.axaml per FR-014
- [ ] T100 Run full test suite: cd MaterialClient.Common.Tests && dotnet test
- [ ] T101 Perform end-to-end manual test following all acceptance scenarios from spec.md
- [ ] T102 Validate against all 6 Success Criteria from spec.md (SC-001 to SC-006)

**Final Checkpoint**: 所有功能完成，测试通过，ready for deployment

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) completion - **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2) completion
- **User Story 2 (Phase 4)**: Depends on Foundational (Phase 2) completion
  - Optional soft dependency on US1 for navigation integration (T067-T068)
- **User Story 3 (Phase 5)**: Depends on US1 and US2 completion (uses their services)
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - **NO** dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational - **SOFT** dependency on US1 for startup flow, but independently testable
- **User Story 3 (P3)**: Depends on US1 and US2 - reuses their services and entities

### Within Each User Story (TDD Flow)

1. **RED**: Write tests → All tests MUST FAIL
2. **GREEN**: Implement minimal code → All tests MUST PASS
3. **REFACTOR**: Improve code → Tests still PASS

**Strict order within each phase**:
- Tests → Data Model → Services → UI → Integration → Verification

### Parallel Opportunities

#### Phase 1: Setup
- T002, T003, T004 can all run in parallel

#### Phase 2: Foundational
- **2.1**: T005-T009 (all DTOs) can run in parallel
- **2.2**: T010-T011 must be sequential (define then register)
- **2.3**: T012-T013 parallel, T014-T015 parallel, T016 depends on all
- **2.4**: T017-T018 can run in parallel

#### User Story 1
- **Tests**: T019-T022 can run in parallel
- **Data Model**: T023-T025 sequential, T026 waits for all
- **Services**: T027-T028 parallel, T029-T030 sequential
- **UI**: T031-T032 parallel, T033-T035 sequential
- **Startup**: T036-T040 mostly sequential (dependencies)

#### User Story 2
- **Tests**: T045-T048 can run in parallel
- **Data Model**: T049-T050 parallel, T051-T053 sequential
- **Services**: T054-T058 sequential (dependencies), T059 after all
- **UI**: T060-T061 parallel, T062-T066 sequential

#### User Story 3
- **Tests**: T075-T077 can run in parallel
- **Implementation**: T078-T084 mostly sequential (tight integration)

#### Phase 6: Polish
- T089-T091 (error handling) can run in parallel
- T092-T094 (logging) can run in parallel
- T098-T099 (documentation) can run in parallel

---

## Parallel Example: User Story 1

**Simultaneous Test Creation** (T019-T022):
```bash
# Task T019: BDD feature file
# Task T020: Unit test for MachineCodeService  
# Task T021: Integration test for VerifyAuthCodeAsync
# Task T022: Integration test for CheckLicenseStatusAsync
```

**Simultaneous Model Setup** (T023-T025):
```bash
# Task T023: Create LicenseInfo entity class
# Task T024: Add DbSet to DbContext
# Task T025: Configure entity mapping
# Wait for all → T026: Create migration
```

**Simultaneous UI Files** (T031-T032):
```bash
# Task T031: Create AuthCodeWindow.axaml
# Task T032: Create AuthCodeWindow.axaml.cs
# Wait for both → T033-T035: ViewModel implementation
```

---

## Implementation Strategy

### MVP First (User Story 1 Only) 🎯

**Fastest path to working software:**

1. ✅ Complete **Phase 1**: Setup (T001-T004) → ~30 mins
2. ✅ Complete **Phase 2**: Foundational (T005-T018) → ~3 hours
3. ✅ Complete **Phase 3**: User Story 1 (T019-T044) → ~1 day
4. **STOP and VALIDATE**: 
   - Run tests: `dotnet test --filter "Category=US1"`
   - Manual test: Launch app → Enter auth code → Verify success
   - Demo the MVP!

**MVP Delivers**: Software authorization activation - the highest priority feature

### Incremental Delivery

**Add value incrementally without breaking existing features:**

1. Foundation (Phase 1-2) → ~4 hours
2. ➕ User Story 1 (Phase 3) → Test independently → **Release v0.1** (MVP)
3. ➕ User Story 2 (Phase 4) → Test independently → **Release v0.2** (+ Login)
4. ➕ User Story 3 (Phase 5) → Test independently → **Release v0.3** (+ Expiry)
5. ➕ Polish (Phase 6) → Final validation → **Release v1.0** (Production)

**Each release is independently deployable and testable**

### Parallel Team Strategy

**With 3 developers:**

1. **Week 1**: All 3 developers complete Foundation together (Phase 1-2)
2. **Week 2+**: After Foundation ready:
   - **Developer A**: User Story 1 (T019-T044)
   - **Developer B**: User Story 2 (T045-T074) - can start in parallel!
   - **Developer C**: Prepare test infrastructure, help with integration
3. **Week 3**: Developer C works on User Story 3 (T075-T088)
4. **Week 4**: All developers work on Polish (Phase 6)

**Critical**: Foundation (Phase 2) MUST be complete before any user story work begins

---

## Task Summary

| Phase | Task Count | Can Parallelize | Estimated Time |
|-------|------------|-----------------|----------------|
| Phase 1: Setup | 4 | 3 tasks | 30 mins |
| Phase 2: Foundational | 14 | 11 tasks | 3 hours |
| Phase 3: User Story 1 (P1) | 26 | 8 tasks | 1 day |
| Phase 4: User Story 2 (P2) | 30 | 9 tasks | 1.5 days |
| Phase 5: User Story 3 (P3) | 14 | 3 tasks | 0.5 day |
| Phase 6: Polish | 14 | 8 tasks | 0.5 day |
| **TOTAL** | **102 tasks** | **42 parallel** | **~4-5 days** |

### Breakdown by Type

- 🧪 **Tests**: 16 tasks (TDD required)
- 📊 **Data Model**: 10 tasks (entities + migrations)
- 🔧 **Services**: 20 tasks (business logic)
- 🎨 **UI**: 15 tasks (windows + viewmodels)
- 🔗 **Integration**: 12 tasks (wiring components)
- 📝 **Documentation**: 3 tasks
- ✨ **Polish**: 14 tasks (error handling, logging)
- 🛠️ **Infrastructure**: 12 tasks (setup + foundational)

### Independent Test Criteria

**User Story 1**: ✅ Launch app without license → Enter valid auth code → Verify AuthEndTime saved → Login window appears  
**User Story 2**: ✅ Launch app with valid license → Login with credentials → Check "Remember" → Restart → Verify auto-fill → Main window opens  
**User Story 3**: ✅ Set AuthEndTime to past → Launch app → Verify auth window appears → Enter new code → Verify old data cleared

---

## Notes

- **[P] tasks**: Different files, no dependencies - can run in parallel
- **[Story] labels**: Map tasks to user stories for traceability and independent testing
- **TDD enforcement**: Constitution requires Test-First approach - NEVER skip RED phase
- **Checkpoint validation**: Stop at each checkpoint to verify story works independently
- **Commit frequency**: Commit after each task or logical group (T-ID is your commit reference)
- **Avoid**: Same-file conflicts, cross-story dependencies that break independence, skipping tests

---

## Validation Checklist

Before marking feature complete:

- [ ] All 102 tasks completed
- [ ] All tests pass: `dotnet test`
- [ ] All BDD scenarios pass: `dotnet test --filter "Category=BDD"`
- [ ] Manual test following all acceptance scenarios in spec.md
- [ ] All 6 Success Criteria from spec.md validated (SC-001 to SC-006)
- [ ] Code review completed (check: English naming, ABP conventions, security)
- [ ] Constitution check passed (TDD followed, proper entity inheritance, Refit usage)
- [ ] Documentation updated (quickstart.md reflects actual implementation)

---

**Created**: 2025-11-07  
**Status**: Ready for implementation  
**Recommended Start**: Phase 1 → Phase 2 → Phase 3 (MVP) → Validate → Continue

