## Context

本变更围绕有人值守固废称重场景：当前 `AttendedWeighingMainView` 仅在固废模式下提供“打印”按钮，缺少在界面内通过受控方式更新固废运单 `OrderType`（例如在首磅状态和完成状态之间切换）的能力。运单匹配与领域行为由 `IWeighingMatchingService` 及其实现负责，运单本身在领域层提供 `OrderType` 相关方法。目标是在不破坏既有导航与视图选择规则的前提下，新增“修改”入口并通过应用服务调用领域方法，仅修改运单的 `OrderType` 状态。

## Goals / Non-Goals

**Goals:**
- 在固废模式下于“打印”按钮左侧新增“修改”按钮，并通过命令与 ViewModel 打通。
- 在 ViewModel 层新增用于编辑固废运单状态的命令（例如 `EditSolidWasteCommand`），调用 `IWeighingMatchingService` 暴露的新应用服务方法以执行领域层对 `OrderType` 的更新。
- 确保按钮的可见性/可用性与当前选中条目、用户权限及运单状态（可编辑性）一致。
- 修改成功后刷新界面数据，保证“打印”按钮输出的内容基于最新的运单状态与已有重量数据。

**Non-Goals:**
- 不调整现有的列表分页与导航算法（例如 `NavigateToItemAsync` 等），只在需要时复用现有导航能力。
- 不在本次变更中引入新的外部依赖或跨界面弹窗编辑器，修改入口限制在当前称重上下文。
- 不改变已有的非固废称重流程与界面行为。

## Decisions

- **UI 按钮布局**：在 `AttendedWeighingMainView.axaml` 中，沿用现有工具栏布局规则，在“打印”按钮左侧插入“修改”按钮，通过新的绑定属性（例如 `EditSolidWasteCommand`、`CanEditSolidWaste`）控制行为与显隐。
- **命令绑定与 ViewModel 扩展**：在对应的 ViewModel（例如 `AttendedWeighingViewModel` 或聚合上下文 ViewModel）中新增 `ReactiveCommand` 或等价命令属性，封装调用 `IWeighingMatchingService` 的逻辑，并在执行前校验当前选中条目是否为可编辑的固废运单。
- **应用服务接口设计**：在 `IWeighingMatchingService` 中新增一个仅负责更新运单 `OrderType` 的接口，例如 `Task SetWaybillOrderTypeAsync(long waybillId, OrderTypeEnum newOrderType, CancellationToken ct)`，实际实现内部加载运单聚合并调用领域方法更新 `OrderType`；接口本身不修改重量等其他字段。
- **领域调用约束**：`IWeighingMatchingService` 负责协调：根据当前选中运单 ID 加载聚合根 → 校验领域不变式（状态是否允许该 `OrderType` 变更、是否固废运单等）→ 调用领域方法更新 `OrderType` → 持久化并生成必要的领域事件或日志（例如记录状态由 FirstWeight 变为 Completed）。
- **错误处理与反馈**：若领域层因业务规则拒绝修改（例如非首磅状态不允许编辑），服务应抛出受检异常或返回失败结果；ViewModel 捕获后通过现有对话框或消息提示机制向用户显示清晰错误信息。

## Risks / Trade-offs

- **风险：界面状态与领域状态不一致**  
  - 若在修改过程中列表数据未及时刷新，可能导致用户看到的 `FirstWeight` 与实际领域值不一致。  
  - **缓解**：修改完成后统一走现有数据刷新与导航通路（例如重新加载当前页或通过已有导航方法刷新选中项）。

- **风险：固废与非固废逻辑耦合过深**  
  - 在 `IWeighingMatchingService` 中直接分支处理固废特定行为，可能与其他称重场景耦合。  
  - **缓解**：通过清晰的方法命名与参数（如专门的 `EditSolidWasteFirstWeightAsync`），将固废相关逻辑封装在限定边界内，必要时在后续提炼为子服务。

- **风险：领域方法 `OrderType` 语义混淆**  
  - 若 `OrderType` 现有含义偏向状态枚举而非领域行为，直接通过它承载修改 `FirstWeight` 逻辑可能增加理解成本。  
  - **缓解**：在实现阶段遵循领域模型既有约定，如已有通过 `OrderType` 切换首磅/完成状态的行为则沿用；如发现语义不合，可在后续变更中重构领域方法命名与职责。

- **风险：权限与审计覆盖不足**  
  - 修改 `FirstWeight` 属于关键操作，若未纳入权限与审计体系，将有潜在合规风险。  
  - **缓解**：在调用服务前复用现有权限检查机制（如基于角色或操作权限的校验），并在服务实现中记录审计日志（操作者、时间、原值/新值）。

## Migration Plan

- 以特性开关或配置项控制“修改”按钮是否启用（如项目已有类似配置机制，则复用），便于逐步开放该能力。
- 部署后在测试环境验证典型固废场景：首磅录入 → 修改 `FirstWeight` → 再次打印 → 数据管理/报表中检查数据一致性。
- 若发现与现有流程冲突，可通过关闭配置项临时禁用“修改”按钮，而无需回滚数据库或领域模型。

## Open Questions

- 界面上修改 `FirstWeight` 的交互形式（直接在当前视图中编辑、弹出对话框或跳转到详情编辑）以何种方式呈现？当前规范仅要求提供“修改”入口与服务调用，具体 UI 交互可在后续变更中细化。
- 领域方法 `OrderType` 在现有模型中是否已经承担修改首磅数据的职责，还是需要补充新的领域行为方法以避免职责过载？
- 固废运单 `FirstWeight` 的修改是否需要额外的审批流（如双人复核），若有要求，是否应拆分为单独的变更以扩展流程能力？
