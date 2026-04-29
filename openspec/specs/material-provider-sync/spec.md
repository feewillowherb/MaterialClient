## Purpose

提供一次性同步服务，将本地创建的 Material 和 Provider 记录推送到服务端，获取服务端分配的 ID，并更新所有下游实体（Waybill、WeighingRecord、WaybillMaterial）的外键引用，使数据完全可追溯。

## Requirements

### Requirement: 同步服务读取所有本地 Material 和 Provider 记录
`MaterialProviderSyncService` SHALL 在同步操作开始时从本地数据库查询所有未删除的 `Material` 和 `Provider` 实体。

#### Scenario: 本地数据库包含材料和供应商记录
- **WHEN** 调用 `SyncAsync()` 且本地数据库包含 5 条 Material 记录和 3 条 Provider 记录
- **THEN** 服务将所有 5 条 Material 和 3 条 Provider 实体加载到内存中处理

#### Scenario: 本地数据库为空
- **WHEN** 调用 `SyncAsync()` 且不存在 Material 或 Provider 记录
- **THEN** 服务立即完成，输出日志消息提示无数据可同步，无错误返回

### Requirement: 同步服务将每条本地 Material 推送到服务端
服务 SHALL 对每条本地 Material 记录调用 `IMaterialPlatformApi.CreateMaterialByNameAsync`，以材料的 `Name`、`CoId` 和 `ProId` 作为请求体。

#### Scenario: 服务端成功创建材料
- **WHEN** 处理一条本地 Material，`Name="Concrete A"`、`CoId=1`、`ProId="proj-01"`
- **THEN** 服务向服务端发送 `CreateMaterialByNameInput(Name="Concrete A", CoId=1, ProId="proj-01")`，并将返回的 `GoodsId` 记录到本地→服务端 ID 映射中

#### Scenario: 服务端返回材料创建错误
- **WHEN** 服务端对 Material 创建请求返回非成功响应
- **THEN** 服务记录包含材料名称和本地 ID 的错误日志，并抛出异常以在任何数据库写入之前中止同步

#### Scenario: 本地材料 CoId 无效或 ProId 缺失
- **WHEN** 一条本地 Material 的 `CoId=0` 且 ProId 为 null
- **THEN** 服务记录警告并跳过该 Material，不尝试 API 调用

### Requirement: 同步服务将每条本地 Provider 推送到服务端
服务 SHALL 对每条本地 Provider 记录调用 `IMaterialPlatformApi.CreateProviderAsync`，以 `ProviderName`、`DeliveryType=0`、`CoId` 和 `ProId` 作为请求体。

#### Scenario: 服务端成功创建供应商
- **WHEN** 处理一条本地 Provider，`ProviderName="Supplier X"`、`CoId=1`、`ProId="proj-01"`
- **THEN** 服务向服务端发送 `CreateProviderInput(ProviderName="Supplier X", DeliveryType=0, CoId=1, ProId="proj-01")`，并将返回的 `ProviderId` 记录到本地→服务端 ID 映射中

#### Scenario: 服务端返回供应商创建错误
- **WHEN** 服务端对 Provider 创建请求返回非成功响应
- **THEN** 服务记录包含供应商名称和本地 ID 的错误日志，并抛出异常以在任何数据库写入之前中止同步

### Requirement: 同步服务用服务端返回的实体替换本地 Material 实体
所有 Material 推送完成后，服务 SHALL 删除所有本地 Material 实体，并使用 `MaterialGoodListResultDto.ToEntity()` 插入新实体，保留服务端分配的 `GoodsId` 作为主键。

#### Scenario: Material 实体被服务端数据替换
- **WHEN** 服务端已返回所有本地 Material 的 `MaterialGoodListResultDto` 响应
- **THEN** 本地 `Materials` DbSet 被清空，并通过 `ToEntity()` 转换重新填充，每个实体的 `Id` 等于服务端的 `GoodsId`

### Requirement: 同步服务用服务端返回的实体替换本地 Provider 实体
所有 Provider 推送完成后，服务 SHALL 删除所有本地 Provider 实体，并使用 `MaterialProviderListResultDto.ToEntity()` 插入新实体，保留服务端分配的 `ProviderId` 作为主键。

#### Scenario: Provider 实体被服务端数据替换
- **WHEN** 服务端已返回所有本地 Provider 的 `MaterialProviderListResultDto` 响应
- **THEN** 本地 `Providers` DbSet 被清空，并通过 `ToEntity()` 转换重新填充，每个实体的 `Id` 等于服务端的 `ProviderId`

### Requirement: 同步服务清空 MaterialUnit 和 MaterialType 表
在替换 Material/Provider 实体之后，服务 SHALL 清空 `MaterialUnit` 和 `MaterialType` 两张表的所有数据。

#### Scenario: MaterialUnit 表被清空
- **WHEN** Material/Provider 实体替换完成
- **THEN** `MaterialUnits` DbSet 中所有记录被删除（因 `MaterialId` 引用旧本地 ID，同步后失效）

#### Scenario: MaterialType 表被清空
- **WHEN** Material/Provider 实体替换完成
- **THEN** `MaterialTypes` DbSet 中所有记录被删除（本地分类数据需清除以便后续从服务端重新拉取）

#### Scenario: 清空操作在事务内执行
- **WHEN** Phase B 事务中执行清空操作后发生错误
- **THEN** 整个事务回滚，MaterialUnit 和 MaterialType 表恢复原状

### Requirement: 同步服务更新 Waybill 外键
服务 SHALL 使用本地→服务端 ID 映射更新每个 `Waybill` 实体的 `MaterialId` 和 `ProviderId`。

#### Scenario: Waybill 引用更新后的 Material 和 Provider ID
- **WHEN** 一条 Waybill 的 `MaterialId=1`（本地）和 `ProviderId=2`（本地），映射为 `1→100`、`2→200`
- **THEN** 该 Waybill 更新为 `MaterialId=100` 和 `ProviderId=200`

#### Scenario: Waybill 的 MaterialId 或 ProviderId 为 null
- **WHEN** 一条 Waybill 的 `MaterialId=null` 或 `ProviderId=null`
- **THEN** null FK 保持不变

### Requirement: 同步服务更新 WeighingRecord 外键
服务 SHALL 使用本地→服务端 Provider ID 映射更新每个 `WeighingRecord` 实体的 `ProviderId`。

#### Scenario: WeighingRecord 引用更新后的 Provider ID
- **WHEN** 一条 WeighingRecord 的 `ProviderId=2`（本地），映射为 `2→200`
- **THEN** 该 WeighingRecord 更新为 `ProviderId=200`

#### Scenario: WeighingRecord 的 ProviderId 为 null
- **WHEN** 一条 WeighingRecord 的 `ProviderId=null`
- **THEN** null FK 保持不变

### Requirement: 同步服务更新 WaybillMaterial 外键
服务 SHALL 使用本地→服务端 Material ID 映射更新每个 `WaybillMaterial` 实体的 `MaterialId`。

#### Scenario: WaybillMaterial 引用更新后的 Material ID
- **WHEN** 一条 WaybillMaterial 的 `MaterialId=1`（本地），映射为 `1→100`
- **THEN** 该 WaybillMaterial 更新为 `MaterialId=100`

### Requirement: 同步服务更新 WeighingRecord.MaterialsJson 中嵌套的 Material ID
服务 SHALL 反序列化每个 `WeighingRecord.MaterialsJson`，使用本地→服务端 Material ID 映射重写 `WeighingRecordMaterial.MaterialId` 值，然后将更新后的列表序列化回去。

#### Scenario: WeighingRecord 的 MaterialsJson 包含可映射的 Material ID
- **WHEN** 一条 WeighingRecord 的 `MaterialsJson` 包含一个 `WeighingRecordMaterial`，`MaterialId=1`（本地），映射为 `1→100`
- **THEN** `MaterialsJson` 更新后该项的 `MaterialId` 变为 `100`

#### Scenario: WeighingRecord 的 MaterialsJson 包含 null MaterialId
- **WHEN** `MaterialsJson` 中的一个 `WeighingRecordMaterial` 的 `MaterialId=null`
- **THEN** 该项的 `MaterialId` 保持不变（null）

#### Scenario: WeighingRecord 没有 MaterialsJson
- **WHEN** 一条 WeighingRecord 的 `MaterialsJson=null` 或为空
- **THEN** 跳过该记录，不做修改

### Requirement: 同步服务使用两阶段执行和分割的事务边界
服务 SHALL 将网络 I/O 与数据库写入分为两个阶段。Phase A（读取 + API 调用）不使用数据库事务执行。Phase B（实体替换 + FK 更新 + 验证）在单个 `IDbContextTransaction` 内执行。

#### Scenario: Phase A 期间 API 调用失败
- **WHEN** Phase A 期间第 3 次 Material API 调用因网络错误失败
- **THEN** 尚未进行任何数据库更改，无需事务回滚；服务抛出异常，包含已成功推送的记录详情

#### Scenario: Phase B 期间数据库写入失败
- **WHEN** Phase B 期间发生数据库写入错误
- **THEN** 事务回滚，数据库保持原始状态

#### Scenario: 同步成功后原子提交
- **WHEN** 所有 API 调用成功且所有 FK 更新已应用
- **THEN** Phase B 事务提交，持久化所有实体替换和 FK 重写

### Requirement: 同步服务在更新后验证引用完整性
FK 更新完成后，服务 SHALL 验证没有 `Waybill.MaterialId`、`Waybill.ProviderId`、`WeighingRecord.ProviderId`、`WaybillMaterial.MaterialId` 或 `WeighingRecord.MaterialsJson` 嵌套的 `MaterialId` 引用了不存在的本地 Material 或 Provider ID。

#### Scenario: 同步后所有 FK 引用有效
- **WHEN** 同步完成且所有 FK（包括 JSON 嵌套的）已重写
- **THEN** 服务查询孤立 FK 引用，记录零条警告

#### Scenario: 检测到孤立 FK 引用
- **WHEN** 一条 WaybillMaterial 的 `MaterialId` 不匹配任何现有 Material `Id`
- **THEN** 服务记录警告，包含孤立实体的 ID 和 FK 值，然后完成

### Requirement: 同步服务通过 ABP 约定注册
`MaterialProviderSyncService` SHALL 实现 `ITransientDependency` 并使用 `[AutoConstructor]` 进行自动依赖注入，与项目约定保持一致。

#### Scenario: 从 DI 容器解析服务
- **WHEN** 一个类从 ABP 服务提供者请求 `IMaterialProviderSyncService`
- **THEN** `MaterialProviderSyncService` 被解析，`IMaterialPlatformApi` 和 `MaterialClientDbContext` 通过构造函数注入

### Requirement: 同步服务暴露无参数的异步入口点
服务 SHALL 提供 `Task SyncAsync(CancellationToken cancellationToken = default)` 方法，执行完整流水线，无需调用方提供参数。

#### Scenario: 调用方无参数调用 SyncAsync
- **WHEN** 以默认参数调用 `SyncAsync()`
- **THEN** 完整的四阶段流水线（读取、推送、更新、验证）执行至完成
