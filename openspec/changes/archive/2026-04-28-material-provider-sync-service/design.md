## Context

MaterialClient 桌面应用维护一个本地 SQLite 数据库，包含 `Material`、`Provider`、`Waybill`、`WeighingRecord` 和 `WaybillMaterial` 表。历史上，Material 和 Provider 行通过 EF Core 直接插入，使用自增本地 ID — 它们从未经过服务端 API，因此缺少服务端分配的 ID（Material 的 `GoodsId`、Provider 的 `ProviderId`）。

下游表（`Waybill.MaterialId`、`Waybill.ProviderId`、`WeighingRecord.ProviderId`、`WaybillMaterial.MaterialId`）以这些本地 ID 作为外键。此外，`WeighingRecord.MaterialsJson` 存储了 JSON 序列化的 `WeighingRecordMaterial` 列表，每项包含一个 `MaterialId`，同样引用本地 Material ID。

服务端 API 是主数据的唯一数据源，服务端的 `CreateMaterialByName` / `CreateProvider` 端点分配权威 ID。

Toolkit 项目已包含 `DatabaseMigrationService`，演示了该模式：从事务内的数据源读取、映射并写入 `MaterialClientDbContext`。新服务遵循相同的结构模式。

## Goals / Non-Goals

**Goals:**
- 提供单个 `SyncAsync()` 方法，将所有仅存在于本地的 Material 和 Provider 推送到服务端，并重写所有本地 FK 引用（包括 `MaterialsJson` 中 JSON 嵌套的 ID）。
- 在打开数据库事务之前完成所有网络 I/O，确保 SQLite 锁仅在写入期间持有。
- 在单个 EF Core 事务中执行所有数据库写入（实体替换 + FK 更新），使写入阶段的故障不会改变数据库。
- 用服务端返回的 DTO 替换整个本地实体（通过现有 `ToEntity()` 转换器），使审计字段、CoId、ProId 等与服务端一致。
- 通过 `ILogger` 清晰记录进度和错误。

**Non-Goals:**
- 不做增量 / 差异同步 — 这是一次性迁移工具。
- 不做触发同步的 UI — 通过编程方式调用（如 CLI 入口或一次性脚本）。
- 不修改 Common 实体、DTO 或 `IMaterialPlatformApi` 接口。
- 不修改现有业务服务（`MaterialService`、`ProviderService`）。
- 不处理 MaterialUnit 记录的同步 — 同步后将清空 `MaterialUnit` 和 `MaterialType` 表，由后续服务端数据拉取重新填充。

## Decisions

### 1. 服务注册：ABP 约定（`ITransientDependency` + `[AutoConstructor]`）

**选择：** 遵循项目首选的 ABP 集成模式（参见 AGENTS.md）。

**理由：** 现有 `DatabaseMigrationService` 并未使用此模式（它是一个通过构造函数注入的普通类）。但项目约定明确要求优先使用 ABP 接口 + `[AutoConstructor]`。由于同步服务依赖 `IMaterialPlatformApi`（已 Refit 注册）和 `MaterialClientDbContext`，ABP 自动注册是最简洁的方案。

**备选方案：** 像 `DatabaseMigrationService` 一样的普通类 — 因偏离当前项目约定而被否决。

### 2. 事务范围：在网络 I/O 处分割（两阶段方案）

**选择：** 将流水线分为两个阶段，各自有独立的事务边界：
- **Phase A（无事务）：** 读取本地数据 → 调用服务端 API → 构建内存 ID 映射。此阶段仅执行数据库读取和网络 I/O。API 调用期间不持有数据库事务。
- **Phase B（单事务）：** 替换 Material/Provider 实体 → 清空 MaterialType 和 MaterialUnit → 更新所有 FK 引用（包括 `MaterialsJson`）→ 验证完整性 → 提交。

**理由：** 在网络 API 调用期间保持 EF Core `IDbContextTransaction` 开启是一种反模式，尤其对 SQLite 而言。每条记录的 API 调用可能耗时数秒到数分钟，期间数据库锁被持有。如果进程崩溃或网络挂起，事务将无限期保持开启。通过在打开事务前完成所有网络 I/O，数据库写入阶段快速且确定。

**风险权衡：** 如果 Phase A 完成（所有服务端记录已创建）但 Phase B 失败，服务端将产生无本地引用的孤立记录。这是可接受的，因为：(a) 数据集较小，Phase B 不太可能失败；(b) 调用方可以重试，服务端应能优雅处理重复创建；(c) 这是一次性迁移工具。

**备选方案（已否决）：** 单事务包裹所有操作 — 因在 SQLite 上持事务期间执行网络 I/O 不安全且可能长时间锁定数据库而被否决。

### 3. ID 映射策略：内存 `Dictionary<int, int>`（本地 → 服务端）

**选择：** 在推送阶段于内存中构建两个字典（`materialIdMap`、`providerIdMap`）。

**理由：** 数据集较小（proposal 中说明"数据库规模不大，可以全量处理"）。无需 CSV 导出作为临时存储 — 内存字典更简单、更快，且避免文件系统清理问题。

**备选方案：** 基于 CSV 的导出（如原始 proposal 中所述） — 因数据集足够小可放入内存而被否决；CSV 增加了 I/O 复杂性和清理风险，且无实际收益。

### 4. 实体替换：使用服务端 DTO 的 `ToEntity()` 转换器，然后 `RemoveRange` + `AddRange`

**选择：** 收集所有服务端响应后，删除所有本地 Material/Provider 行，并通过 `ToEntity()` 插入服务端返回的实体。

**理由：** 服务端 DTO 包含权威的 `GoodsId`/`ProviderId` 和所有审计字段。使用 `ToEntity()` 确保一致性。`RemoveRange` + `AddRange` 比逐属性更新更简单，且避免遗漏字段。

**备选方案：** 对现有实体使用 `UpdateRange` — 因服务端 DTO 可能包含本地实体没有的字段（反之亦然），导致逐字段映射容易出错而被否决。

### 5. API 输入要求：从现有本地数据获取 CoId 和 ProId

**选择：** 从现有本地 `Material` / `Provider` 实体读取 `CoId` 和 `ProId`，传递给 `CreateMaterialByNameInput` / `CreateProviderInput`。

**理由：** 这些值已存在于本地实体上。`CreateProviderInput` 还需要 `DeliveryType` — 由于本地 `Provider` 实体不直接存储配送类型，将默认为 `0`。

### 6. FK 更新顺序：Material → Waybill/WaybillMaterial/WeighingRecord.MaterialsJson，Provider → Waybill/WeighingRecord

**选择：** 实体替换后，按以下顺序批量更新 FK：
1. `WaybillMaterial.MaterialId`（引用 Material）
2. `Waybill.MaterialId` 和 `Waybill.ProviderId`
3. `WeighingRecord.ProviderId`
4. `WeighingRecord.MaterialsJson` — 反序列化每条记录的 JSON，使用 `materialIdMap` 重写 `WeighingRecordMaterial.MaterialId`，序列化回

**理由：** `WaybillMaterial` 具有复合关系（WaybillId + MaterialId）。先更新 Material FK 确保不会出现违反引用完整性的中间状态。SQLite 无强制 FK 约束，顺序不影响功能，但这是良好实践。

### 7. WeighingRecord.MaterialsJson — JSON 嵌套的 Material ID 重写

**选择：** 实体替换后，遍历所有 `MaterialsJson` 非空的 `WeighingRecord` 实体。对每条记录：反序列化为 `List<WeighingRecordMaterial>`，使用 `materialIdMap` 重写每项的 `MaterialId`，序列化回并赋值给 `MaterialsJson`。

**理由：** `WeighingRecord` 将材料引用存储为 JSON 而非传统 FK 列。`WeighingRecordMaterial` 类具有 `MaterialId` 属性（nullable int），引用本地 Material 表。如果不更新这些 ID，同步后称重记录将指向不存在或错误的材料。`WeighingRecord` 上的 `Materials` 属性已通过 `JsonSerializer` 提供反序列化器/序列化器，重写逻辑简单直接。

**备选方案（已否决）：** 不处理 MaterialsJson — 因会在称重记录中产生孤立的材料引用而被否决。

### 8. 清空 MaterialType 和 MaterialUnit 表

**选择：** 在 Phase B 中替换 Material/Provider 实体之后、更新 FK 之前，使用 `RemoveRange` 清空 `MaterialType` 和 `MaterialUnit` 两张表的所有数据。

**理由：**
- `MaterialUnit` 具有 `MaterialId` 外键引用 Material。同步后 Material 的本地 ID 被替换为服务端 ID，旧的 MaterialUnit 记录指向已删除的 Material 行，成为孤立数据。
- `MaterialType` 是本地分类数据，同样引用旧的本地 ID 体系，需要清除以便后续从服务端重新拉取权威数据。

**执行顺序：** 先清空 MaterialUnit（因引用 Material），再清空 MaterialType。在事务内执行，失败可回滚。

**备选方案（已否决）：** 保留 MaterialType/MaterialUnit 不处理 — 因旧 ID 体系下这些数据对新的服务端 ID 无意义，保留会导致业务逻辑混乱。

## Risks / Trade-offs

- **[服务端重复创建]** → 如果服务端已存在同名 Material/Provider，`CreateMaterialByNameAsync` 可能返回错误或创建重复项。**缓解措施：** 清晰记录 API 响应；调用方可检查并决定。服务不做去重逻辑（属于服务端职责）。
- **[同步期间网络故障]** → API 调用在数据库事务开启之前执行，因此本地数据库在网络 I/O 期间不会面临风险。如果 Phase A 部分完成（部分服务端记录已创建），调用方可重试 — 服务端应能优雅处理重复名称。**缓解措施：** 服务逐条记录进度日志；故障时调用方精确知道哪些记录已推送。
- **[本地实体缺少 CoId/ProId]** → 部分本地记录可能 `CoId = 0` 或 ProId 为 null。**缓解措施：** 服务记录警告并跳过 CoId/ProId 无效的记录，而非静默失败。
- **[WaybillMaterial.MaterialName 过期]** → `WaybillMaterial` 存储了反规范化的 `MaterialName`。同步后服务端可能分配了不同的规范名称。**缓解措施：** 如服务端响应中有可用信息，更新 `WaybillMaterial` 的 `MaterialName`。
- **[MaterialsJson 格式异常]** → 部分记录的 `WeighingRecord.MaterialsJson` 可能包含无效 JSON 或 null `MaterialId` 值。**缓解措施：** 现有 `Materials` 属性的 getter 已能优雅处理反序列化失败（返回空列表）。重写时跳过 null `MaterialId` 值。

## Data Flow

```
Phase A (无数据库事务)                Phase B (单数据库事务)
─────────────────────                ────────────────────────────────

┌──────────┐                          ┌──────────────────────────┐
│ SQLite DB│──读取 Materials──┐       │ RemoveRange(Materials)   │
│          │──读取 Providers──┤       │ RemoveRange(Providers)   │
└──────────┘                  │       │ AddRange(server Materials)│
                              ▼       │ AddRange(server Providers)│
                     ┌────────────┐   │ RemoveRange(MaterialUnits)│
                     │ 内存 ID    │   │ RemoveRange(MaterialTypes)│
                     │ 映射表     │   │                          │
                     └─────┬──────┘   │ 更新 FK:                 │
                           │          │  WaybillMaterial.MaterialId
                           ▼          │  Waybill.MaterialId      │
                     ┌────────────┐   │  Waybill.ProviderId      │
                     │ 服务端 API │   │  WeighingRecord.ProviderId
                     │ POST /api/ │   │  WeighingRecord.MaterialsJson
                     │ Material/* │   │                          │
                     │ Provider/* │   │ 验证完整性               │
                     └────────────┘   │ 提交                     │
                                      └──────────────────────────┘
```

## Detailed Code Changes

| 文件路径 | 变更类型 | 变更说明 |
|----------|----------|----------|
| `MaterialClient.Toolkit/Services/MaterialProviderSyncService.cs` | **新增** | 接口 + 实现（`IMaterialProviderSyncService`、`MaterialProviderSyncService`） |
