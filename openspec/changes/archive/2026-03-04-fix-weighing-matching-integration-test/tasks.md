## 1. 分析与规划
- [x] 1.1 Review current test implementation and identify all `Property()` usage
- [x] 1.2 Review feature file scenarios and identify which need table format conversion
- [x] 1.3 Review business logic in `WeighingMatchingService` and `AttendedWeighingService` to ensure coverage
- [x] 1.4 Document current test scenarios and their expected behavior

## 2. 修正实体属性访问
- [x] 2.1 Replace all `entry.Property("CreationTime").CurrentValue = ...` with `record.AddDate = ...`
- [x] 2.2 Remove all `dbContext.Entry(record)` and `Property()` calls
- [x] 2.3 Update `WeighingRecordTestDto` to include `AddDate` field if needed
- [x] 2.4 Test that entity creation with `AddDate` works correctly

## 3. 输入统一为表格式
- [x] 3.1 Ensure all scenarios use `Given Weighing records as below` with table
- [x] 3.2 Remove individual parameter-based `Given record (.*) has...` steps
- [x] 3.3 Update `WeighingRecordTestDto` to support all required fields in table format
- [x] 3.4 Verify table parsing works correctly for all scenarios

## 4. 验证统一为表格式
- [x] 4.1 Ensure all scenarios use `Then Waybills as below` with table
- [x] 4.2 Remove individual `Then the waybill should have...` steps
- [x] 4.3 Enhance `WaybillVerifyTestDto` to support all verification fields
- [x] 4.4 Update `ThenWaybillsAsBelow` method to handle all verification cases
- [x] 4.5 Add table-based verification for weighing record matched types

## 5. 增强业务逻辑覆盖
- [x] 5.1 Add scenarios for edge cases (multiple candidates, time window, weight validation)
- [x] 5.2 Ensure `AutoMatchAsync` logic is covered
- [x] 5.3 Ensure `CreateWaybillAsync` logic is covered
- [x] 5.4 Ensure `WeighingRecord.TryMatch` validation is covered
- [x] 5.5 Add scenarios for different delivery types (Receiving, Sending)

## 6. 更新 Feature 文件
- [x] 6.1 Convert all scenarios to use table format for input
- [x] 6.2 Convert all scenarios to use table format for verification
- [x] 6.3 Remove individual property verification steps
- [x] 6.4 Ensure feature file is consistent and readable
- [x] 6.5 Add any missing scenarios for business logic coverage

## 7. 测试与验证
- [ ] 7.1 Run all integration tests and verify they pass (requires manual test execution)
- [x] 7.2 Verify no `Property()` usage remains in test code
- [x] 7.3 Verify all scenarios use table format consistently
- [ ] 7.4 Review test coverage to ensure business logic is covered (requires test execution and analysis)
- [ ] 7.5 Document any remaining edge cases that need scenarios (requires test execution)
