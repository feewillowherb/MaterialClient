# Feature Specification: 有人值守功能实现

**Feature Branch**: `001-attended-weighing`  
**Created**: 2025-11-06  
**Status**: Draft  
**Input**: User description: "有人值守实现"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 查看和管理称重记录与运单 (Priority: P1)

操作员在有人值守页面查看所有未完成的称重记录（WeighingRecordType为Unmatch）和已完成的运单（Waybill），点击任意记录可以查看详细信息，包括毛重、进场时间、车辆照片、票据照片等。

**Why this priority**: 这是核心的用户界面功能，是操作员与系统交互的主要入口，必须首先实现才能支持其他功能。

**Independent Test**: 可以通过手动创建测试数据（称重记录和运单），验证页面能够正确显示列表和详情，无需依赖实际硬件设备。

**Acceptance Scenarios**:

1. **Given** 系统中有未完成的称重记录，**When** 操作员打开有人值守页面，**Then** 页面显示称重记录列表，状态为未完成
2. **Given** 系统中有已完成的运单，**When** 操作员打开有人值守页面，**Then** 页面显示运单列表，状态为已完成
3. **Given** 操作员在有人值守页面，**When** 点击一个未完成的称重记录，**Then** 显示详情页面，包含毛重、进场时间，其他字段为空
4. **Given** 操作员在有人值守页面，**When** 点击一个已完成的运单，**Then** 显示详情页面，包含完整的运单信息
5. **Given** 操作员在详情页面，**When** 查看车辆照片，**Then** 显示车辆拍照界面
6. **Given** 操作员在详情页面，**When** 查看票据照片，**Then** 显示票据照片界面

---

### User Story 2 - 自动称重检测和记录创建 (Priority: P1)

系统自动监测地磅重量变化，当车辆驶入并稳定称重后，自动创建称重记录，包括获取车牌号、拍照、记录重量和时间。

**Why this priority**: 这是核心业务逻辑，是整个有人值守功能的基础，必须与UI功能同时实现才能形成完整的MVP。

**Independent Test**: 可以通过模拟地磅重量接口返回测试值，验证系统能够正确检测重量变化、稳定状态，并创建称重记录，无需依赖实际硬件设备。

**Acceptance Scenarios**:

1. **Given** 地磅处于空载状态（重量在偏移范围内），**When** 车辆驶入导致重量超过偏移范围，**Then** 系统状态变为"已上称"
2. **Given** 地磅处于"已上称"状态，**When** 重量稳定超过偏移范围且持续时间达到稳定时间阈值，**Then** 系统状态变为"称重完成"，创建WeighingRecord并持久化
3. **Given** 系统创建WeighingRecord时，**When** 调用车牌抓拍接口，**Then** 获取车牌号并保存到WeighingRecord的PlateNumber字段（可为空）
4. **Given** 系统创建WeighingRecord时，**When** 调用车辆拍照接口，**Then** 获取照片并创建WeighingRecordAttachment关联
5. **Given** 地磅处于"已上称"状态，**When** 重量未稳定直接回到偏移范围内，**Then** 系统记录称重失败日志，不创建WeighingRecord，状态回到"已下称"
6. **Given** 地磅处于"称重完成"状态，**When** 车辆驶离导致重量回到偏移范围内，**Then** 系统状态变为"已下称"

---

### User Story 3 - 称重记录自动匹配和运单生成 (Priority: P2)

系统自动匹配符合条件的进场（Join）和出场（Out）称重记录，匹配成功后自动创建运单（Waybill）。

**Why this priority**: 虽然核心功能是称重记录创建，但自动匹配和运单生成是完整的业务流程，应该在MVP之后尽快实现以提供完整价值。

**Independent Test**: 可以通过创建两个符合匹配条件的WeighingRecord，验证系统能够正确匹配并生成Waybill，无需依赖实际硬件设备。

**Acceptance Scenarios**:

1. **Given** 系统中有两个WeighingRecord，车牌号相同，创建时间间隔在匹配时间窗口内，**When** 系统执行匹配检查，**Then** 系统识别这两个记录符合匹配规则1
2. **Given** 系统中有多个相同车牌号的WeighingRecord，创建时间间隔都在匹配时间窗口内，**When** 系统执行匹配检查，**Then** 系统选择时间间隔最短的一对记录进行匹配
3. **Given** 系统中有两个符合规则1的WeighingRecord，进场记录创建时间早于出场记录，**When** 页面设置DeliveryType为收料，**Then** 系统验证进场重量大于出场重量，满足匹配规则2
4. **Given** 系统中有两个符合规则1的WeighingRecord，进场记录创建时间早于出场记录，**When** 页面设置DeliveryType为发料，**Then** 系统验证进场重量小于出场重量，满足匹配规则2
5. **Given** 系统匹配成功两个WeighingRecord，**When** 创建Waybill，**Then** 使用Guid生成OrderNo，从Join和Out记录中提取Provider和Material（任意不为空），创建Waybill并持久化
6. **Given** 系统匹配成功两个WeighingRecord，**When** 创建Waybill，**Then** 将两个WeighingRecord的WeighingRecordType标记为Join和Out
7. **Given** 操作员在有人值守页面，**When** 匹配成功生成Waybill，**Then** 未完成列表中的两个WeighingRecord消失，已完成列表中出现新的Waybill

---

### User Story 4 - 硬件接口测试和配置 (Priority: P3)

系统提供硬件接口的模拟实现，返回固定值用于测试，并提供HTTP接口允许手动修改测试值。

**Why this priority**: 虽然对核心业务流程很重要，但在设备未对接阶段，这是测试和开发支持功能，可以在核心功能稳定后实现。

**Independent Test**: 可以通过调用HTTP接口修改测试值，然后调用相应的硬件接口，验证返回期望的固定值，无需依赖实际硬件设备。

**Acceptance Scenarios**:

1. **Given** 系统已启动，**When** 调用获取地磅重量接口，**Then** 返回一个decimal值（可通过HTTP接口修改）
2. **Given** 系统已启动，**When** 调用车牌抓拍接口，**Then** 返回车辆号码字符串（可通过HTTP接口修改）
3. **Given** 系统已启动，**When** 调用车辆拍照接口，**Then** 返回4张相同的JPG格式照片
4. **Given** 系统已启动，**When** 调用票据拍照接口，**Then** 返回1张JPG格式照片
5. **Given** 操作员需要测试不同重量值，**When** 通过HTTP接口修改地磅重量返回值，**Then** 后续调用获取地磅重量接口返回新值
6. **Given** 操作员需要测试不同车牌号，**When** 通过HTTP接口修改车牌抓拍返回值，**Then** 后续调用车牌抓拍接口返回新值

---

### Edge Cases

- 当车牌抓拍接口返回空字符串时，系统如何处理？
- 当称重记录创建后，在匹配时间窗口内没有找到匹配记录时，系统如何处理？
- 当系统状态为"称重完成"但车辆未驶离，又有新车辆驶入时，系统如何处理？

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系统必须支持HTTP服务集成，提供最小功能实现，支持API文档查看
- **FR-002**: 系统必须支持依赖注入容器用于服务管理
- **FR-003**: 系统必须提供有人值守页面UI，遵循UI设计规范，修复现有布局问题
- **FR-004**: 系统必须在有人值守页面显示未完成的称重记录（WeighingRecordType为Unmatch）和已完成的运单（Waybill）
- **FR-005**: 系统必须支持点击列表项查看详细信息，包括称重记录详情和运单详情
- **FR-006**: 系统必须支持在详情页面查看车辆照片和票据照片
- **FR-007**: 系统必须支持配置WeightOffsetRange（默认-1到1），WeightStableDuration（默认2000ms），WeighingMatchDuration（默认3小时）
- **FR-008**: 系统必须监测地磅重量变化，识别三种状态：已下称（OffScale）、已上称（OnScale）、称重完成（Weighing）。当重量等于或超过偏移范围上限时视为"已上称"，等于或低于下限时视为"已下称"
- **FR-009**: 系统必须在车辆驶入并稳定称重后（重量超过偏移范围且稳定时间达到阈值），自动创建WeighingRecord并持久化
- **FR-010**: 系统必须在创建WeighingRecord时调用车牌抓拍接口获取车牌号（可为空）
- **FR-011**: 系统必须在创建WeighingRecord时调用车辆拍照接口获取照片并创建WeighingRecordAttachment关联。如果拍照接口调用失败，系统继续创建WeighingRecord，记录拍照失败日志，但不创建WeighingRecordAttachment
- **FR-012**: 系统必须在重量未稳定直接回到偏移范围内时，记录称重失败日志，不创建WeighingRecord
- **FR-013**: 系统必须为WeighingRecord添加WeighingRecordType字段，用于表示记录状态（Unmatch、Join、Out）
- **FR-014**: 系统必须自动匹配两个WeighingRecord：匹配规则1（车牌相同且时间间隔在匹配窗口内）。当存在多个匹配候选时，选择时间间隔最短的一对记录
- **FR-015**: 系统必须自动匹配两个WeighingRecord：匹配规则2（进场记录早于出场记录，且根据DeliveryType验证重量关系）。如果规则1满足但规则2不满足，系统不创建Waybill，两个记录保持未匹配状态
- **FR-016**: 系统必须在匹配成功时自动创建Waybill，OrderNo为唯一标识符，Provider和Material从Join或Out记录中提取（任意不为空）。如果Provider和Material都为空，系统创建Waybill时ProviderId和MaterialId保持为null
- **FR-017**: 系统必须在匹配成功时将两个WeighingRecord的WeighingRecordType标记为Join和Out
- **FR-018**: 系统必须支持DeliveryType设置（发料、收料），影响匹配规则2的重量验证逻辑
- **FR-019**: 系统必须提供获取地磅重量接口，返回decimal值，支持通过HTTP接口修改返回值（用于测试）
- **FR-020**: 系统必须提供车牌抓拍接口，返回车辆号码字符串，支持通过HTTP接口修改返回值（用于测试）
- **FR-021**: 系统必须提供车辆拍照接口，返回4张相同的JPG格式照片
- **FR-022**: 系统必须提供票据拍照接口，返回1张JPG格式图片
- **FR-023**: 系统必须在硬件接口实现中添加"待对接设备"备注
- **FR-024**: 系统必须在创建Waybill时，对于无法获取数据源的字段提供默认值并添加TODO注释
- **FR-025**: 系统必须支持票据拍照为空，用户可以不拍票据

### Key Entities *(include if feature involves data)*

- **WeighingRecord（称重记录）**: 记录车辆在地磅上的称重信息，包括重量、车牌号、进场时间、供应商、物料等。新增WeighingRecordType字段用于表示匹配状态（Unmatch、Join、Out）
- **Waybill（运单）**: 由匹配成功的进场和出场称重记录生成，包含完整的运输订单信息，包括订单号、供应商、物料、重量、时间等
- **WeighingRecordAttachment（称重记录附件）**: 关联WeighingRecord和附件文件，用于存储车辆照片等附件
- **WaybillAttachment（运单附件）**: 关联Waybill和附件文件，用于存储票据照片等附件（可选）
- **VehicleWeightStatus（车辆重量状态）**: 枚举类型，表示地磅的三种状态：已下称（OffScale）、已上称（OnScale）、称重完成（Weighing）
- **DeliveryType（配送类型）**: 枚举类型，表示配送类型：发料（0）、收料（1），影响匹配规则2的重量验证逻辑
- **WeighingRecordType（称重记录类型）**: 枚举类型，表示称重记录的匹配状态：未匹配（Unmatch）、进场（Join）、出场（Out）

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 系统能够自动检测车辆驶入并稳定称重，在2秒内完成称重记录创建和持久化
- **SC-002**: 系统能够正确匹配符合条件的称重记录对，匹配准确率达到95%以上（基于测试数据）
- **SC-003**: 操作员能够在3秒内完成查看称重记录列表和详情的基本操作
- **SC-004**: 系统能够处理连续车辆称重场景，在车辆驶离后5秒内恢复到可接受下一车辆的状态
- **SC-005**: 系统能够正确区分称重成功和失败场景，失败场景下不创建无效记录，成功率达到100%
- **SC-006**: 硬件接口模拟实现能够稳定返回测试数据，支持通过HTTP接口修改测试值，用户操作响应及时
- **SC-007**: 系统能够支持至少100条未匹配称重记录和100条已完成运单的列表显示，用户能够快速浏览和查看

## Assumptions

- 系统已存在基础的实体定义（WeighingRecord、Waybill、Provider、Material等）
- 系统已配置数据持久化支持
- 硬件设备当前未对接，所有硬件接口返回固定测试值
- 车牌号验证在当前阶段不需要，允许任意字符串
- 票据拍照为可选功能，用户可以不拍票据
- 配置项可以持久化存储
- 系统日志用于记录称重失败等关键事件

## Dependencies

- HTTP服务支持（用于API文档和硬件接口测试）
- 数据持久化支持
- UI框架支持
- 图片处理和存储支持
- 配置文件读取支持

## Clarifications

### Session 2025-11-06

- Q: 当匹配时间窗口内存在多个相同车牌号的称重记录时，系统如何选择匹配？ → A: 时间最近优先 - 自动选择时间间隔最短的一对记录进行匹配
- Q: 当地磅重量正好在偏移范围边界值（如WeightOffsetRange的上限或下限）时，系统如何处理状态转换？ → A: 边界值视为"超过" - 当重量等于或超过偏移范围上限时视为"已上称"，等于或低于下限时视为"已下称"
- Q: 当车辆拍照接口调用失败时，系统是否继续创建WeighingRecord？ → A: 继续创建但记录失败 - 系统继续创建WeighingRecord，记录拍照失败日志，但不创建WeighingRecordAttachment
- Q: 当匹配规则1满足但匹配规则2不满足时，系统如何处理？ → A: 保持未匹配状态 - 系统不创建Waybill，两个WeighingRecord保持WeighingRecordType为Unmatch，等待后续可能匹配的记录
- Q: 当创建Waybill时，如果Join和Out记录中的Provider和Material都为空，系统如何处理？ → A: 允许为空 - 系统创建Waybill，ProviderId和MaterialId保持为null，依赖业务逻辑处理空值情况

## Out of Scope

- 实际硬件设备对接（当前阶段仅返回固定值）
- 车牌号有效性验证
- 用户认证和授权（当前阶段仅支持API文档，不需要认证）
- 数据导出和报表功能
- 历史数据查询和统计
- 多用户并发控制
- 数据同步和备份
- 移动端支持
