# 道闸功能评估报告（Vzvision LPR I/O 与称重状态门控）

> **文档性质**：评估报告，非实施说明。  
> **日期**：2026-03-25  
> **修订**：2026-03-26 增补 §12（道闸与 LRP 方向枚举统一为 A/B、与 LPR 解耦、两种 I/O 控制方式、启动时 A/B 成对校验）；明确 **方案 B 为默认方案**、方案 A 为可选增强。

---

## 1. 背景与目标

本评估聚焦“道闸功能模式”（双道闸 A/B + 两路 LPR/LRP 触发）与现有系统称重状态机之间的协同，核心目标包括：

- 满足你给出的道闸时序与安全约束（见 §3）
- 尽量不影响原“无道闸模式”（即不启用道闸/联动时，现有业务流程不变）
- 明确是否需要在 `AttendedWeighingStatus` 扩展一个“待上磅/待上榜”状态，并评估代价与风险

---

## 2. 现状分析（当前代码如何工作）

### 2.1 称重状态机（`AttendedWeighingStatus`）

系统内置称重状态枚举：

- `OffScale`（称重已结束）
- `WaitingForStability`（等待稳定）
- `WeightStabilized`（重量已稳定）
- `WaitingForDeparture`（等待下磅）

状态转换由 `AttendedWeighingService.CreateStatusStream(...)` 基于：

- 重量是否超过最小阈值
- 稳定性窗口 `stability.IsStable`
- 称重记录创建标记（`_lastCreatedWeighingRecordIdSubject`）

完成，并在转换时触发副作用（抓拍、创建称重记录、状态通知等）。

系统还会通过 `MessageBus` 发送 `StatusChangedMessage(AttendedWeighingStatus)`，供其他模块订阅。

### 2.2 道闸 I/O 控制（`LprGateIoControlService`）

当前道闸 I/O 控制服务的行为可概括为：

- 订阅 `MessageBus` 的 `LicensePlateRecognizedMessage`
- 根据车牌识别设备配置 `LicensePlateRecognitionConfig` 判断：
  - `EnableGateIo == true` 才继续
  - `message.DeviceType == LprDeviceType.Vzvision` 才继续（非 Vzvision 记录仅日志后跳过）
- 调用 `IVzvisionLprService.SetIoOutputAutoRespAsync(config, ioChannel, 500)`

也就是说：**当前实现只“识别到车牌就开闸脉冲”，不读取 `AttendedWeighingStatus`，也不区分入口/出口阶段。**

---

## 3. 你的需求约束拆解（逐条映射）

### 3.1 道闸 I/O 定义

- `1`：请求/保持打开道闸（打开优先级持续到 `IO=1` 期间；在 `VzLPRClient_SetIOOutputAutoResp` 自动复位释放之前，雷达关闭信号会被忽略。雷达必须在 `durationMs` 这段时间结束后“再次触发一次”才允许关闭生效）
- `0`：关闭许可位（雷达在边沿/条件满足时执行关闭；关闭判定与 `AttendedWeighingStatus` 不关联）

同时你提到“车辆上磅后，由外部激光雷达自动关闭道闸”。

> 现状与风险点：当前 Vzvision 下发能力为 `VzLPRClient_SetIOOutputAutoResp(..., durationMs)`，会在 `durationMs` 到期后自动复位到另一个电平（通常意味着最终会回到 `0`）。
> 在你给出的约束下：
> - `durationMs` 期间雷达关闭触发会被忽略；若雷达要关闭道闸，必须在 `durationMs` 严格结束后“再次触发一次”
> - `0` 仅决定“允许雷达去执行关闭”（雷达边沿/条件满足才会关）
> - 打开优先级持续到 `IO=1`（直到自动复位释放）期间
> - `AttendedWeighingStatus` 只约束 LRP 的开闸时机；雷达关闭门控只由 `SetIOOutputAutoResp` 的释放/许可位语义决定
> 因此仍需在硬件侧确认：`durationMs` 的结束时间点与雷达关闭触发边沿/条件之间的相对时序是否满足上述“忽略窗口 + 释放后再次触发”的语义

#### 时序图（`durationMs=500ms`，雷达关闭在 `IO=1` 期间被忽略）

```mermaid
sequenceDiagram
autonumber
participant LRP as "LRP识别到车牌"
participant 软件 as "软件(道闸联动)"
participant 道闸IO as "道闸IO(开/关门控)"
participant 雷达 as "激光雷达"

LRP->>软件: 车牌识别成功
软件->>道闸IO: 请求打开(自动复位500ms)<br/>IO=1
Note over 道闸IO,雷达: 500ms内：雷达关闭触发被忽略(不生效)
雷达-->>道闸IO: 关闭触发(示意，发生在500ms内)
软件-->>道闸IO: 自动复位释放<br/>IO=0(关闭许可)
Note over 道闸IO,雷达: 释放后：雷达需要“再次触发一次”才能关闭
雷达->>道闸IO: 再次触发关闭
道闸IO-->>雷达: 执行关闭(由雷达控制)
```

### 3.2 双道闸 A/B 与两路 LPR/LRP 触发

- 两个 LRP 分别对应道闸 I/O `A` 与 `B`
- A/B 两个方向都能通行
- 同一时间只能通过一辆车

这意味着软件需要“会话化”（session）：一辆车进入后，直到该车完成称重并放行，期间不应被另一方向/另一相机的车牌识别打断或重复开闸。

### 3.3 车辆进入/离开时序

- 当 LRP 接收到车牌：打开对应道闸 I/O，等待车辆上磅，并确认当前入口处
- 车辆上磅后：外部激光雷达自动关闭道闸
- 处于 `WaitingForStability`、`WeightStabilized` 时：**道闸不应该被 LRP 打开**
- 处于 `WaitingForDeparture` 时：打开出口道闸（即另一边的道闸）

### 3.4 核心诉求（以实现影响评估为导向）

1. **修改尽量不影响原来无道闸模式的功能。**
2. 道闸模式下是否要扩展 `AttendedWeighingStatus` 增加“待上磅/待上榜”状态？
   - 待上磅状态定义：打开识别到车牌那一侧道闸，出口道闸不会被 LRP 打开
3. `WaitingForStability`、`WeightStabilized` 阶段入口与出口道闸都不允许被 LRP 打开

---

## 4. 现状对照评估：关键缺口（Gap Analysis）

### 4.1 状态门控缺失（无法满足 §3.3 中的稳定/稳重禁开闸）

当前 `LprGateIoControlService` 不读取 `AttendedWeighingStatus`，因此：

- 在 `WaitingForStability` / `WeightStabilized` 阶段，只要再次收到车牌识别事件，仍会触发开闸脉冲
- 无法实现“入口/出口道闸都不允许被 LRP 打开”的约束

### 4.2 入口/出口侧选择缺失（无法保证“出口道闸不会被 LRP 打开”）

当前 `LicensePlateRecognitionConfig` 包含：

- `Direction`（`A/B`，表示设备/相机所属的道闸侧别）
- `EnableGateIo`
- `IoChannel`

在本文档语义下，LRP 与道闸 I/O 的“方向/侧别”采用**同一枚举语义：`A | B`**（见 §12.1）。其中 **Entry/Exit（入口/出口）为会话运行时角色**：由首次触发的一侧确定入口，另一侧为出口（见 §5、§7）。

但现有道闸控制只将“识别设备”映射到“`IoChannel` 脉冲下发”，没有“入口侧锁定 + 出口侧计算（另一侧）+ 出口只在 WaitingForDeparture 打开”的会话逻辑。

结果是：在同一称重周期内，哪个 LRP 再次识别到车牌，都可能触发其对应道闸 I/O，而这不符合“出口道闸不会被 LRP 打开”的定义。

### 4.3 同一时间只能通行一辆车：需要会话化/互斥机制

当前实现没有“车辆会话”概念，因此缺少互斥策略：

- 车牌识别事件可能在同一称重周期内重复出现（来自入口相机、出口相机或同一相机的多次识别）
- 当前会将每次识别都当作一次开闸触发，可能导致重复开闸/错误开闸

### 4.4 IO 电平语义与 SDK 自动复位机制潜在冲突

如 §3.1 所述，需要硬件/设备手册确认：SDK 自动复位后的电平（`0`）是否严格满足“允许外部激光雷达关闭”的语义（即：不会在 `1` 期间出现提前关闭，也不会导致激光雷达关闭能力失效）。
如 §3.1 所述，需要硬件/设备手册确认：在 `VzLPRClient_SetIOOutputAutoResp` 自动复位释放之前，雷达关闭触发是否会被忽略；且仅在 `durationMs` 严格结束后，雷达需要“再次触发一次”才允许关闭生效（关闭不依赖 `AttendedWeighingStatus`）。

---

## 5. 设计选项评估

### 方案 A（可选增强）：扩展 `AttendedWeighingStatus` 增加“待上磅/待上榜”（可读性/可视化更好，但改动较大）

**目标**：把“开闸后、车辆尚未进入称重稳定窗口（或尚未满足进入 `WaitingForStability` 的重量阈值）”显式建模为新的状态。

一种可行状态序列：

- `OffScale`（称重未开始）
- `WaitingForTop`（待上磅：已由 LPR 打开入口道闸；出口不会被 LPR 打开）
- `WaitingForStability`
- `WeightStabilized`
- `WaitingForDeparture`（此时打开出口道闸）
- `OffScale`

**可靠性/降级约束（确保道闸不可靠不影响主流程）**

- **状态推进不得依赖道闸 I/O 成功**：`AttendedWeighingStatus` 的推进应继续以重量/稳定性/称重记录等核心信号为准，道闸开闸成功与否不得成为进入/退出 `WaitingForTop`、`WaitingForStability`、`WaitingForDeparture` 的硬前置条件。
- **道闸作为可失败的旁路副作用**：道闸控制调用失败、session 推断异常或被人工遥控器干预时，系统应允许“道闸联动失效但称重/识别/记录正常运行”的降级形态（参见 §12.5）。
- **失败处理口径**：道闸异常仅影响“是否自动开闸”能力；不得阻断称重状态机、不得阻断称重记录创建、不得影响除道闸以外功能的可用性。

**优点**

- 状态语义清晰：道闸控制、UI 展示、日志审计都能直接使用同一个状态源
- 可自然表达“出口道闸不会被 LRP 打开”的约束（在 `WaitingForTop` 阶段直接拒绝出口方向的触发）

**缺点 / 风险**

- 现有 `CreateStatusStream` 的状态转换几乎完全由“重量/稳定性”推导。加入“待上磅”意味着需要引入**外部事件驱动**（来自 `LprGateIoControlService` 或 LPR 触发）来改变状态流：
  - 需要解决与重量流并发组合时的竞态与一致性
  - 需要更新/增加大量状态机单元测试
- 若“待上磅”状态与重量阈值之间的边界不够明确，可能导致状态抖动或漏状态
- 若实现不满足上述“可靠性/降级约束”，方案 A 可能将道闸 I/O 的不确定性放大为称重状态机的不稳定，从而违背“道闸不可靠不影响主流程”的要求。

### 方案 B（默认方案）：不改 `AttendedWeighingStatus`，在 `LprGateIoControlService` 内实现“道闸会话 session”（低风险/最小改动）

**目标**：在道闸控制服务内维护“当前车辆会话”的进入侧与阶段，并从 `MessageBus` 订阅 `StatusChangedMessage`，完成：

- LRP 识别时：只在合适阶段打开入口道闸
- 稳定/稳重阶段：拒绝任何 LRP 触发开闸
- 等待下磅阶段：打开出口道闸（另一侧）

**推荐会话字段**

- `sessionActive`：当前是否已有车辆会话
- `entrySide`：入口道闸侧（A 或 B）
- `exitOpened`：出口道闸是否已在本会话打开（避免重复开闸）
- （可选）`sessionStartedAt`：用于故障/超时清理

**会话语义（可执行描述）**

- **会话开始**：当 `AttendedWeighingStatus == OffScale` 且 `sessionActive == false` 时，首次收到 LRP 车牌识别事件：
  - 将该事件所属侧别 `Direction(A/B)` 记录为 `entrySide`
  - 令 `sessionActive = true`
  - `exitSide` 由 `entrySide` 推导为另一侧（`A <-> B`），不需要额外配置
- **会话期间互斥**：`sessionActive == true` 时，任何来自 A/B 任一侧的后续识别事件都不得重新开启/改变会话入口侧别（避免重复开闸、避免“同时只能通过一辆车”被破坏）。
- **出口开闸触发**：当状态变为 `WaitingForDeparture` 时，若 `sessionActive == true && exitOpened == false`，对 `exitSide` 触发开闸一次并置 `exitOpened = true`。
- **会话结束**：当状态回到 `OffScale` 时清理会话字段（`sessionActive=false`、`entrySide`/`exitOpened` 复位），允许下一辆车进入新会话。

> 说明：Entry/Exit（入口/出口）为会话运行时角色，与 `Direction(A/B)` 的侧别配置解耦；“入口/出口”不落配置，仅由首次触发决定（见 §12.0、§12.1）。

**实现要点（抽象）**

1. `LprGateIoControlService` 订阅 `StatusChangedMessage`
2. 收到 `LicensePlateRecognizedMessage` 时：
   - 若当前状态为 `WaitingForStability` 或 `WeightStabilized`：直接拒绝（满足 §3.3 的禁开闸）
   - 若当前状态为 `WaitingForDeparture`：拒绝入口开闸（只允许出口开闸由状态触发）
   - 若当前状态为 `OffScale` 且 `sessionActive == false`：打开“识别到车牌那一侧”（入口侧锁定），并设置 `sessionActive = true`
   - 若 `sessionActive == true`：拒绝重复触发（保证同一时间只能通过一辆车、且出口道闸不会被 LRP 打开）
3. 当状态变为 `WaitingForDeparture`：
   - 若 `sessionActive == true` 且 `exitOpened == false`：打开出口道闸（另一侧 I/O），并标记 `exitOpened = true`
4. 当状态回到 `OffScale`：
   - 清理会话：`sessionActive=false`、`entrySide`/`exitOpened` 复位

**优点**

- 不改 `AttendedWeighingStatus`，最大限度降低状态机风险
- 进入“待上磅”的语义可以通过 `sessionActive && AttendedWeighingStatus==OffScale` 间接表达
- 修改范围主要集中在 `LprGateIoControlService`（以及可能的配置字段映射）

**缺点 / 风险**

- UI 若需要展示“待上磅”文字含义，需要额外映射逻辑（或后续再选择方案 A）
- 需要新增“入口/出口侧映射”机制（A/B 的确定来源）

---

## 6. 推荐方案（默认：方案 B）

综合“尽量不影响无道闸模式”“降低状态机改动风险”以及“道闸失效不影响主流程”（见 §12.5），本评估**默认采用方案 B**：

- **默认**：在 `LprGateIoControlService` 内实现道闸会话 session，并通过 `StatusChangedMessage` 实现状态门控。
- **可选增强**：扩展 `AttendedWeighingStatus`（方案 A）作为下一阶段，仅在需要 UI/审计明确“待上磅”等状态语义时实施。

---

## 7. 入口/出口侧（A/B）映射需要补齐

当前 `LicensePlateRecognitionConfig` 的 `Direction` 使用 `A/B`（侧别），而你的需求是：

- 两个 LPR 对应两道闸 I/O `A` 与 `B`
- A/B 两个方向都能通行（即物理方向与“入口/出口含义”可能会随车辆行驶方向改变）

因此建议在配置层明确“相机/LRP 属于哪一侧道闸”的映射字段，并统一使用 `A/B` 侧别语义（见 §12.1），例如：

- `GateIoSide`：枚举 `A/B`（侧别），与 `LicensePlateRecognitionConfig.Direction` 同义/一致（命名以实现为准）。

并确保：

- 车牌识别设备（LPR）属于哪一侧 I/O（A 或 B）在配置中可确定
- 道闸会话中 `entrySide` 与 `exitSide` 的计算可靠（`exitSide = entrySide == A ? B : A`）

> **设计取向**：本文档中 LRP 与道闸 I/O 的“方向/侧别”统一使用 `A/B`；入口/出口为会话角色（首次触发确定入口、另一侧为出口），不再用 `In/Out` 表达（§12）。

---

## 8. 兼容性（不影响“无道闸模式”）

当前“无道闸模式”在配置层可理解为：

- `EnableGateIo == false`：`LprGateIoControlService` 不会触发 I/O

因此：

- 不建议强行把新的“状态门控逻辑”应用到所有既有 `EnableGateIo` 语义上，除非你确认现有用户期望不会依赖“识别即开闸脉冲”的旧行为。
- 推荐增加一个开关（命名可在实现时统一），例如：
  - `EnableGateIoLogicV2`（默认 false，逐步切换）

这样可保证“旧方式仍可工作”，新方式满足你提出的精确时序约束。

---

## 9. 风险与待确认事项

1. **IO 复位电平语义冲突**  
   当前使用 `VzLPRClient_SetIOOutputAutoResp` 会在 `500ms` 之后自动复位到 `0`。
   在你给出的语义下：`0` 应仅代表“允许外部激光雷达关闭”，而关闭由外部雷达执行；同时“打开优先级大于雷达关闭”。
   因此仍需确认硬件侧以下行为是否成立：
   - 在雷达未触发关闭的情况下，`1 -> 0` 是否不会导致道闸提前关闭（关闭由雷达执行）
   - 若雷达在 `VzLPRClient_SetIOOutputAutoResp` 自动复位释放之前已经触发过一次关闭：释放前触发是否被忽略，且必须在释放结束后“再次触发一次”才生效
   - 关闭门控是否与 `AttendedWeighingStatus` 无关（即不需要依赖 `WaitingForStability` / `WeightStabilized` / `WaitingForDeparture`）
   - 打开优先级是否持续到 `IO=1`（直到自动复位释放）期间，并对“释放前关闭”形成有效阻断

2. **多次识别/抖动导致重复开闸**  
   道闸会话必须拒绝重复触发：当 `sessionActive==true` 时，无论入口/出口相机继续识别都不应影响道闸（直到状态回到 `OffScale`）。

3. **“待上磅”超时清理策略**  
   如果开闸后车辆未能真正上磅（重量始终未到阈值），会话如何结束？  
   若需要安全闭合，需明确“0 可以关闭”的软件闭合能力是否可靠（又回到 §9.1）。

4. **“入口确认”机制**  
   你要求“打开识别到车牌那一侧道闸，并确认当前入口处，车辆上磅后”。  
   在方案 B 下，入口侧可直接由“触发开闸的 LPR 侧”锁定，但仍需确认是否存在“同一车辆在不同相机/不同侧反复识别”的情况。

5. **同一时间只能通过一辆车的判定边界**  
   目前互斥可以基于 `AttendedWeighingStatus != OffScale` 或会话 `sessionActive`。  
   若需要更精确（比如 OffScale 但已开闸等待上磅），则建议由 `sessionActive` 控制。

---

## 10. 测试与验证建议

### 10.1 软件单元测试（基于 MessageBus / 订阅模拟）

- `OffScale + 入口相机识别车牌 -> 开启入口道闸 A/B 一次`
- `WaitingForStability/WeightStabilized 阶段 -> 任意相机识别均不触发道闸`
- `WaitingForDeparture -> 打开出口道闸（另一侧）一次，且 LRP 识别不重复开闸`
- `OffScale 重置会话 -> 新车可再次触发`
- `快速连续多次识别 -> 只触发一次开闸`

### 10.2 集成测试（真实状态机联动）

- 通过重量模拟达到各状态转换边界，观察道闸开关与外部激光雷达闭合时序是否一致

### 10.3 硬件验证（必须）

- 验证 `VzLPRClient_SetIOOutputAutoResp(..., 500)` 在“1/0 电平语义”下的真实行为：
  - 在“雷达未触发关闭”的情况下，`1 -> 0` 是否不会导致道闸提前关闭（关闭由雷达执行）
  - 在 `durationMs` 未结束时雷达触发关闭一次：该触发是否会被忽略，且必须在 `durationMs` 结束后“再次触发一次”才会关闭
  - 在自动复位释放结束后，`0` 关闭许可是否允许雷达关闭生效，且不依赖 `AttendedWeighingStatus`

---

## 11. 结论

当前实现无法满足你提出的稳定/稳重阶段禁开闸、以及 WaitingForDeparture 开出口道闸的精确时序要求。

在“尽量不影响无道闸模式”“降低状态机改动风险”以及“道闸失效不影响主流程”的前提下，本评估建议：

- **默认**以 `LprGateIoControlService` 的会话化 session + `StatusChangedMessage` 状态门控完成全部时序约束（方案 B）
- 是否扩展 `AttendedWeighingStatus` 增加“待上磅”状态作为可选增强项（方案 A），仅在需要 UI/审计明确语义时实施
- 实现阶段应落实 §12：道闸专用方向枚举、与 LPR 解耦的 I/O 抽象、两种控制方式划分，以及启动时对道闸 I/O 配置的成对校验

---

## 12. 设计补充：道闸枚举、与 LPR 解耦、控制方式与启动校验

本节为对道闸模块的**设计约束补充**（与 §7 入口/出口映射、§5 方案选型配合），用于指导后续实现与评审。

### 12.0 当前版本配置来源约束（可执行口径）

- **配置来源**：当前版本道闸相关配置**来自 LRP 配置**（`LicensePlateRecognitionConfig`）。独立 Gate 配置项暂不实现。
- **侧别字段**：`LicensePlateRecognitionConfig.Direction` 表示相机/LRP 所属道闸侧别，枚举值为 `A | B`。
- **会话角色**：Entry/Exit（入口/出口）为运行时会话角色，不落配置；由首次触发侧别确定入口，另一侧为出口。

### 12.1 道闸与 LRP Direction 统一为 A/B

- **方向/侧别枚举定义**：LRP 与道闸 I/O 的“方向/侧别”在本文档中采用同一语义：`A | B`（物理侧别/接线侧别）。
- **入口/出口的语义**：Entry/Exit（入口/出口）为会话运行时角色，不是配置枚举；在一次会话中由首次 LRP 触发的一侧确定入口，另一侧为出口（见 §5、§7）。
- **命名约束**：实现层可使用 `GateSide` / `GateIoSide` / `Direction` 等命名，但枚举值语义应一致为 `A/B`，避免再次引入 `In/Out` 混用。

### 12.2 道闸与 LPR 解耦（预留直接 I/O）

- **设计目标**：道闸能力在架构上与 LPR **解耦**——道闸模块应能表达“不经由 LPR、仅操作道闸硬件 I/O”的控制路径。
- **接口策略**：为“直接控制道闸 I/O”保留抽象接口（例如独立的 `IGateIoController` 或与 LPR 并行的门面）；**当前实现**可在该路径上明确返回“不支持”或等价提示，**不阻塞**经 LPR SDK 的控制路径；未来版本再补齐直接 I/O 实现。

### 12.3 道闸 I/O 控制的两种方式

| 方式 | 说明 |
|------|------|
| **1. 经 LPR SDK 控制道闸** | 通过 Vzvision 等 LPR 设备提供的 I/O 能力（如 `SetIOOutputAutoResp`）下发道闸开闭脉冲，与现有 `LprGateIoControlService` 路径一致。**当前版本仅实现此方式。** |
| **2. 直接控制道闸 I/O** | 应用直接驱动道闸硬件输入/输出，不经过 LPR SDK；**仅保留接口/设计**，当前实现可阶段性返回“不支持”（不影响方式 1），与 §12.2 一致。 |

业务或配置层应能区分当前采用哪种方式（或主方式 + 降级策略），避免两种语义在同一配置项中纠缠。当前版本由于方式 2 不可用，实际工作路径固定为方式 1。

### 12.4 启动道闸功能时的配置有效性校验

在**启动或启用道闸相关功能**时，应对道闸配置做有效性检查：

- **参与校验的配置集合**：从 `LicensePlateRecognitionConfig` 中筛选 `EnableGateIo == true` 的配置（必要时再叠加 `DeviceType == Vzvision` 约束，以实现为准）。
- **有效判定**：上述集合内，`Direction` 的 `A` 与 `B` 必须且仅能各出现一次（即 **恰好一对 A/B**；不允许零对、多对或不成对）。
- **失败行为**：若校验不通过，则**道闸功能启动失败**，并**打印/记录明确日志**（说明期望“仅一对 A/B”及当前配置问题），避免带病运行导致误开闸或状态不一致。

该校验与 §8 无道闸模式开关正交：`EnableGateIo`（或等价开关）为真且进入道闸工作路径时执行；未启用道闸则不强制此项（以实现约定为准）。

### 12.5 可靠性与人工遥控开闸（降级原则）

- **核心原则**：道闸联动的 session 属于“外围联动能力”。即使 session 状态在运行时出现错误/不同步，也必须保证称重状态机与除道闸以外的业务功能**正常使用**（不被道闸 session 失败拖垮）。
- **允许人工干预**：在现场运行中，用户可能通过**遥控器**手动开/关道闸，这属于硬件外部动作，可能导致“道闸实际状态”与软件 session 推断不一致。系统应允许该行为发生，并以日志/告警方式记录，不应因此阻断主流程。
- **降级策略（文档约束）**：当检测到 session 异常、配置不满足、或道闸控制调用失败时，道闸联动可以进入“仅记录/不控制”的降级模式；降级期间仍持续产出称重状态、车牌识别、称重记录等核心业务能力。

