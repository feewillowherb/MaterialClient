# Implementation Tasks: 有人值守功能实现

**Branch**: `001-attended-weighing` | **Date**: 2025-11-06  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Task Summary

- **Total Tasks**: 47
- **Phase 1 (Setup)**: 3 tasks
- **Phase 2 (Foundational)**: 12 tasks
- **Phase 3 (User Story 1 - UI)**: 8 tasks
- **Phase 4 (User Story 2 - Weighing)**: 12 tasks
- **Phase 5 (User Story 3 - Matching)**: 7 tasks
- **Phase 6 (User Story 4 - Hardware APIs)**: 4 tasks
- **Phase 7 (Polish)**: 1 task

## Dependencies & Story Completion Order

```
Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1) + Phase 4 (US2) [parallel]
                ↓
                Phase 5 (US3) [depends on US1 + US2]
                ↓
                Phase 6 (US4) [can be parallel with US3]
                ↓
                Phase 7 (Polish)
```

**Parallel Execution Opportunities**:
- Phase 3 (US1 - UI) and Phase 4 (US2 - Weighing) can be developed in parallel after Phase 2
- Within each phase, tasks marked with [P] can be executed in parallel
- Phase 6 (US4 - Hardware APIs) can start after Phase 2 completes

## MVP Scope

**Recommended MVP**: Phase 3 (US1) + Phase 4 (US2)
- Provides complete UI for viewing records
- Provides automatic weighing detection and record creation
- Enables end-to-end testing of core workflow

## Implementation Strategy

1. **MVP First**: Implement US1 (UI) and US2 (Weighing) together for complete core functionality
2. **Incremental Delivery**: Add US3 (Matching) after MVP, then US4 (Hardware APIs)
3. **TDD Approach**: Write integration tests before implementation (constitution requirement)
4. **Independent Testing**: Each user story can be tested independently

---

## Phase 1: Setup & Project Initialization

**Goal**: Prepare project structure and add required dependencies

**Tasks**:

- [ ] T001 Add required NuGet packages to MaterialClient.Common.csproj (Volo.Abp.AspNetCore, Volo.Abp.Autofac, Swashbuckle.AspNetCore)
- [ ] T002 Verify existing dependencies (ABP Framework 9.3.6, Avalonia 11.3.6, EF Core 9.0.10)
- [ ] T003 Create Configuration folder structure in MaterialClient.Common/Configuration/

---

## Phase 2: Foundational Components (Blocking Prerequisites)

**Goal**: Create all foundational components required by user stories (enums, entities, configuration, database migration, ABP HTTP Host setup)

**Independent Test**: Can verify enum values, entity modifications, configuration loading, and HTTP Host startup without user story implementation

**Tasks**:

- [ ] T004 Create VehicleWeightStatus enum in MaterialClient.Common/Entities/Enums/VehicleWeightStatus.cs
- [ ] T005 Create DeliveryType enum in MaterialClient.Common/Entities/Enums/DeliveryType.cs
- [ ] T006 Create WeighingRecordType enum in MaterialClient.Common/Entities/Enums/WeighingRecordType.cs
- [ ] T007 [P] Modify WeighingRecord entity to add RecordType property in MaterialClient.Common/Entities/WeighingRecord.cs
- [ ] T008 [P] Create WeighingConfiguration class in MaterialClient.Common/Configuration/WeighingConfiguration.cs
- [ ] T009 Create EF Core migration to add RecordType column in MaterialClient.Common/EntityFrameworkCore/
- [ ] T010 Apply database migration using dotnet ef database update
- [ ] T011 [P] Create MaterialClientHttpHostModule in MaterialClient/HttpHost/MaterialClientHttpHostModule.cs
- [ ] T012 [P] Modify Program.cs to configure and start ABP HTTP Host in background thread in MaterialClient/Program.cs
- [ ] T013 [P] Configure Swagger in MaterialClientHttpHostModule for API documentation
- [ ] T014 [P] Add WeighingConfiguration to appsettings.json with default values
- [ ] T015 Update MaterialClientDbContext to configure WeighingRecord.RecordType in MaterialClient.Common/EntityFrameworkCore/MaterialClientDbContext.cs

---

## Phase 3: User Story 1 - 查看和管理称重记录与运单 (P1)

**Goal**: 操作员在有人值守页面查看所有未完成的称重记录（WeighingRecordType为Unmatch）和已完成的运单（Waybill），点击任意记录可以查看详细信息，包括毛重、进场时间、车辆照片、票据照片等。

**Why Priority P1**: 这是核心的用户界面功能，是操作员与系统交互的主要入口，必须首先实现才能支持其他功能。

**Independent Test**: 可以通过手动创建测试数据（称重记录和运单），验证页面能够正确显示列表和详情，无需依赖实际硬件设备。

**Acceptance Criteria**:
1. **Given** 系统中有未完成的称重记录，**When** 操作员打开有人值守页面，**Then** 页面显示称重记录列表，状态为未完成
2. **Given** 系统中有已完成的运单，**When** 操作员打开有人值守页面，**Then** 页面显示运单列表，状态为已完成
3. **Given** 操作员在有人值守页面，**When** 点击一个未完成的称重记录，**Then** 显示详情页面，包含毛重、进场时间，其他字段为空
4. **Given** 操作员在有人值守页面，**When** 点击一个已完成的运单，**Then** 显示详情页面，包含完整的运单信息
5. **Given** 操作员在详情页面，**When** 查看车辆照片，**Then** 显示车辆拍照界面
6. **Given** 操作员在详情页面，**When** 查看票据照片，**Then** 显示票据照片界面

**Tasks**:

- [ ] T016 [US1] Create AttendedWeighingWindow.axaml UI layout in MaterialClient/Views/AttendedWeighingWindow.axaml (参考assets/中的设计文件，不使用MainWindow.xaml的布局错误)
- [ ] T017 [US1] Create AttendedWeighingWindow.axaml.cs code-behind in MaterialClient/Views/AttendedWeighingWindow.axaml.cs
- [ ] T018 [US1] Create AttendedWeighingViewModel class in MaterialClient/ViewModels/AttendedWeighingViewModel.cs
- [ ] T019 [US1] [P] Implement data binding for unmatched records list in AttendedWeighingViewModel.cs using IRepository<WeighingRecord, long>
- [ ] T020 [US1] [P] Implement data binding for completed waybills list in AttendedWeighingViewModel.cs using IRepository<Waybill, long>
- [ ] T021 [US1] Implement detail view display logic in AttendedWeighingViewModel.cs (show weight, join time for WeighingRecord; full info for Waybill)
- [ ] T022 [US1] Implement vehicle photo display in AttendedWeighingWindow.axaml (reference join_out_camera.png design)
- [ ] T023 [US1] Implement bill photo display in AttendedWeighingWindow.axaml (reference bill.png design)

---

## Phase 4: User Story 2 - 自动称重检测和记录创建 (P1)

**Goal**: 系统自动监测地磅重量变化，当车辆驶入并稳定称重后，自动创建称重记录，包括获取车牌号、拍照、记录重量和时间。

**Why Priority P1**: 这是核心业务逻辑，是整个有人值守功能的基础，必须与UI功能同时实现才能形成完整的MVP。

**Independent Test**: 可以通过模拟地磅重量接口返回测试值，验证系统能够正确检测重量变化、稳定状态，并创建称重记录，无需依赖实际硬件设备。

**Acceptance Criteria**:
1. **Given** 地磅处于空载状态（重量在偏移范围内），**When** 车辆驶入导致重量超过偏移范围，**Then** 系统状态变为"已上称"
2. **Given** 地磅处于"已上称"状态，**When** 重量稳定超过偏移范围且持续时间达到稳定时间阈值，**Then** 系统状态变为"称重完成"，创建WeighingRecord并持久化
3. **Given** 系统创建WeighingRecord时，**When** 调用车牌抓拍接口，**Then** 获取车牌号并保存到WeighingRecord的PlateNumber字段（可为空）
4. **Given** 系统创建WeighingRecord时，**When** 调用车辆拍照接口，**Then** 获取照片并创建WeighingRecordAttachment关联
5. **Given** 地磅处于"已上称"状态，**When** 重量未稳定直接回到偏移范围内，**Then** 系统记录称重失败日志，不创建WeighingRecord，状态回到"已下称"
6. **Given** 地磅处于"称重完成"状态，**When** 车辆驶离导致重量回到偏移范围内，**Then** 系统状态变为"已下称"

**Tasks**:

- [ ] T024 [US2] Create ITruckScaleWeightService interface in MaterialClient.Common/Services/Hardware/ITruckScaleWeightService.cs
- [ ] T025 [US2] [P] Create TruckScaleWeightService implementation in MaterialClient.Common/Services/Hardware/TruckScaleWeightService.cs (返回固定测试值，添加"待对接设备"备注)
- [ ] T026 [US2] [P] Create IPlateNumberCaptureService interface in MaterialClient.Common/Services/Hardware/IPlateNumberCaptureService.cs
- [ ] T027 [US2] [P] Create PlateNumberCaptureService implementation in MaterialClient.Common/Services/Hardware/PlateNumberCaptureService.cs (返回固定测试值，添加"待对接设备"备注)
- [ ] T028 [US2] [P] Create IVehiclePhotoService interface in MaterialClient.Common/Services/Hardware/IVehiclePhotoService.cs
- [ ] T029 [US2] [P] Create VehiclePhotoService implementation in MaterialClient.Common/Services/Hardware/VehiclePhotoService.cs (返回4张固定图片，添加"待对接设备"备注)
- [ ] T030 [US2] Create WeighingService class in MaterialClient.Common/Services/WeighingService.cs
- [ ] T031 [US2] Implement state machine logic in WeighingService.cs (OffScale → OnScale → Weighing transitions with boundary value handling)
- [ ] T032 [US2] Implement weight stability tracking in WeighingService.cs (timer-based stable duration check)
- [ ] T033 [US2] Implement WeighingRecord creation logic in WeighingService.cs (call hardware services, handle failures gracefully)
- [ ] T034 [US2] Register hardware services and WeighingService in MaterialClientCommonModule.cs
- [ ] T035 [US2] Write integration tests for WeighingService in MaterialClient.Common.Tests/WeighingServiceTests.cs (test state transitions, record creation, failure handling)

---

## Phase 5: User Story 3 - 称重记录自动匹配和运单生成 (P2)

**Goal**: 系统自动匹配符合条件的进场（Join）和出场（Out）称重记录，匹配成功后自动创建运单（Waybill）。

**Why Priority P2**: 虽然核心功能是称重记录创建，但自动匹配和运单生成是完整的业务流程，应该在MVP之后尽快实现以提供完整价值。

**Independent Test**: 可以通过创建两个符合匹配条件的WeighingRecord，验证系统能够正确匹配并生成Waybill，无需依赖实际硬件设备。

**Acceptance Criteria**:
1. **Given** 系统中有两个WeighingRecord，车牌号相同，创建时间间隔在匹配时间窗口内，**When** 系统执行匹配检查，**Then** 系统识别这两个记录符合匹配规则1
2. **Given** 系统中有多个相同车牌号的WeighingRecord，创建时间间隔都在匹配时间窗口内，**When** 系统执行匹配检查，**Then** 系统选择时间间隔最短的一对记录进行匹配
3. **Given** 系统中有两个符合规则1的WeighingRecord，进场记录创建时间早于出场记录，**When** 页面设置DeliveryType为收料，**Then** 系统验证进场重量大于出场重量，满足匹配规则2
4. **Given** 系统中有两个符合规则1的WeighingRecord，进场记录创建时间早于出场记录，**When** 页面设置DeliveryType为发料，**Then** 系统验证进场重量小于出场重量，满足匹配规则2
5. **Given** 系统匹配成功两个WeighingRecord，**When** 创建Waybill，**Then** 使用Guid生成OrderNo，从Join和Out记录中提取Provider和Material（任意不为空），创建Waybill并持久化
6. **Given** 系统匹配成功两个WeighingRecord，**When** 创建Waybill，**Then** 将两个WeighingRecord的WeighingRecordType标记为Join和Out
7. **Given** 操作员在有人值守页面，**When** 匹配成功生成Waybill，**Then** 未完成列表中的两个WeighingRecord消失，已完成列表中出现新的Waybill

**Tasks**:

- [ ] T036 [US3] Create WeighingMatchingService class in MaterialClient.Common/Services/WeighingMatchingService.cs
- [ ] T037 [US3] Implement matching rule 1 logic in WeighingMatchingService.cs (same plate number + time window, select shortest interval if multiple candidates)
- [ ] T038 [US3] Implement matching rule 2 logic in WeighingMatchingService.cs (time order + weight relationship based on DeliveryType)
- [ ] T039 [US3] Implement Waybill creation logic in WeighingMatchingService.cs (generate OrderNo from Guid, extract Provider/Material, handle null values)
- [ ] T040 [US3] Implement WeighingRecord.RecordType update logic in WeighingMatchingService.cs (mark as Join/Out after matching)
- [ ] T041 [US3] Register WeighingMatchingService in MaterialClientCommonModule.cs
- [ ] T042 [US3] Write integration tests for WeighingMatchingService in MaterialClient.Common.Tests/WeighingMatchingServiceTests.cs (test matching rules, Waybill creation, multiple candidates selection)

---

## Phase 6: User Story 4 - 硬件接口测试和配置 (P3)

**Goal**: 系统提供硬件接口的模拟实现，返回固定值用于测试，并提供HTTP接口允许手动修改测试值。

**Why Priority P3**: 虽然对核心业务流程很重要，但在设备未对接阶段，这是测试和开发支持功能，可以在核心功能稳定后实现。

**Independent Test**: 可以通过调用HTTP接口修改测试值，然后调用相应的硬件接口，验证返回期望的固定值，无需依赖实际硬件设备。

**Acceptance Criteria**:
1. **Given** 系统已启动，**When** 调用获取地磅重量接口，**Then** 返回一个decimal值（可通过HTTP接口修改）
2. **Given** 系统已启动，**When** 调用车牌抓拍接口，**Then** 返回车辆号码字符串（可通过HTTP接口修改）
3. **Given** 系统已启动，**When** 调用车辆拍照接口，**Then** 返回4张相同的JPG格式照片
4. **Given** 系统已启动，**When** 调用票据拍照接口，**Then** 返回1张JPG格式照片
5. **Given** 操作员需要测试不同重量值，**When** 通过HTTP接口修改地磅重量返回值，**Then** 后续调用获取地磅重量接口返回新值
6. **Given** 操作员需要测试不同车牌号，**When** 通过HTTP接口修改车牌抓拍返回值，**Then** 后续调用车牌抓拍接口返回新值

**Tasks**:

- [ ] T043 [US4] Create TruckScaleWeightController in MaterialClient.Common/Controllers/TruckScaleWeightController.cs (GET/POST /api/hardware/truck-scale/weight)
- [ ] T044 [US4] [P] Create PlateNumberController in MaterialClient.Common/Controllers/PlateNumberController.cs (GET/POST /api/hardware/plate-number)
- [ ] T045 [US4] [P] Create VehiclePhotoController in MaterialClient.Common/Controllers/VehiclePhotoController.cs (GET /api/hardware/vehicle-photos)
- [ ] T046 [US4] [P] Create BillPhotoController in MaterialClient.Common/Controllers/BillPhotoController.cs (GET /api/hardware/bill-photo)

---

## Phase 7: Polish & Cross-Cutting Concerns

**Goal**: Final integration, testing, and polish

**Tasks**:

- [ ] T047 Verify all integration tests pass and update UI to reflect matching results in real-time in AttendedWeighingViewModel.cs

---

## Parallel Execution Examples

### After Phase 2 (Foundational), can parallelize:

**Team A (UI Focus)**:
- T016-T023 (US1 - UI implementation)

**Team B (Business Logic Focus)**:
- T024-T035 (US2 - Weighing service implementation)

### Within Phase 4 (US2), can parallelize:

- T025, T027, T029 (Hardware service implementations) - [P] marked
- All can be implemented simultaneously

### Within Phase 6 (US4), can parallelize:

- T044, T045, T046 (API controllers) - [P] marked
- All can be implemented simultaneously

---

## Notes

- All code must use English names (constitution requirement)
- All classes must use MaterialClient prefix (constitution requirement)
- Integration tests are required for weighing logic (spec requirement)
- Follow TDD approach: write tests before implementation (constitution requirement)
- Reference assets/ folder for UI design (main_ui1.png, main_ui2.png, order_list.png, order_detail.png, join_out_camera.png, bill.png)
- DO NOT copy layout from MainWindow.xaml (contains layout errors)
- Use standard Avalonia UI layout principles

