## 1. UI 按钮与命令绑定

- [x] 1.1 在 `AttendedWeighingMainView.axaml` 中于“打印”按钮左侧新增“修改”按钮，并绑定新的命令与可见性属性（例如 `EditSolidWasteCommand`、`CanEditSolidWaste`）
- [x] 1.2 更新对应 ViewModel（例如 `AttendedWeighingMainViewModel` 或聚合 ViewModel），实现 `EditSolidWasteCommand`，从当前选中条目中解析固废运单并调用应用服务

## 2. 应用服务与领域调用

- [x] 2.1 在 `IWeighingMatchingService` 中新增仅用于更新运单 `OrderType` 的接口签名（例如 `SetWaybillOrderTypeAsync`），并在实现类中落地
- [x] 2.2 在服务实现中加载当前固废运单聚合根，基于领域方法更新 `OrderType`（如在 FirstWeight 与 Completed 之间切换），并持久化变更
- [ ] 2.3 为状态变更操作补充必要的权限校验与审计日志记录（如已有基础设施可复用则接入）

## 3. 刷新与视图一致性

- [ ] 3.1 在修改成功后复用现有数据刷新/导航逻辑（例如重新加载列表或调用统一导航方法），确保 MainView 中展示的固废运单数据与领域一致
- [ ] 3.2 检查“打印”按钮逻辑，确保在修改后打印内容基于最新 `FirstWeight` 与相关字段，必要时补充测试覆盖

## 4. 验证与回归测试

- [ ] 4.1 为固废运单“修改 + 打印”主路径编写或更新自动化/集成测试（如项目已有测试基础设施）
- [ ] 4.2 在测试环境手工验证典型场景：可编辑与不可编辑条目、“修改”按钮显隐、领域规则校验失败时的错误提示等
