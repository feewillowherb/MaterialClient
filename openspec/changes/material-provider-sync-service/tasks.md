## 1. 服务骨架

- [ ] 1.1 创建 `MaterialClient.Toolkit/Services/MaterialProviderSyncService.cs`，包含 `IMaterialProviderSyncService` 接口（无参数 `SyncAsync` 方法）和 `MaterialProviderSyncService` 实现，使用 `[AutoConstructor]` 和 `ITransientDependency`
- [ ] 1.2 通过构造函数参数注入 `IMaterialPlatformApi`、`MaterialClientDbContext` 和 `ILogger<MaterialProviderSyncService>`

## 2. Phase A — 读取本地数据（无事务）

- [ ] 2.1 从 `Materials` DbSet 查询所有未删除的 `Material` 实体到列表
- [ ] 2.2 从 `Providers` DbSet 查询所有未删除的 `Provider` 实体到列表
- [ ] 2.3 处理空数据库提前退出，输出信息日志

## 3. Phase A — 推送到服务端（无事务）

- [ ] 3.1 遍历 Material，对每条有效记录调用 `CreateMaterialByNameAsync`（跳过 `CoId <= 0` 的记录）；构建 `materialIdMap`（本地 Id → 服务端 GoodsId）
- [ ] 3.2 遍历 Provider，对每条有效记录调用 `CreateProviderAsync`（跳过 `CoId <= 0` 的记录）；构建 `providerIdMap`（本地 Id → 服务端 ProviderId）；使用 `DeliveryType=0`
- [ ] 3.3 处理 API 错误：记录日志并抛出异常，在任何数据库写入之前中止

## 4. Phase B — 替换实体并更新 FK（在事务内）

- [ ] 4.1 开启 `IDbContextTransaction` 并设置 `DisableAuditConcepts = true`
- [ ] 4.2 删除所有本地 `Material` 实体，通过 `MaterialGoodListResultDto.ToEntity()` 插入服务端返回的实体
- [ ] 4.3 删除所有本地 `Provider` 实体，通过 `MaterialProviderListResultDto.ToEntity()` 插入服务端返回的实体
- [ ] 4.4 清空 `MaterialUnit` 表所有数据（`RemoveRange`，因 `MaterialId` 引用旧本地 ID，同步后失效）
- [ ] 4.5 清空 `MaterialType` 表所有数据（`RemoveRange`，本地分类数据需清除以便后续从服务端重新拉取）
- [ ] 4.6 使用 `materialIdMap` 更新 `WaybillMaterial.MaterialId`
- [ ] 4.7 使用 `materialIdMap` 和 `providerIdMap` 更新 `Waybill.MaterialId` 和 `Waybill.ProviderId`
- [ ] 4.8 使用 `providerIdMap` 更新 `WeighingRecord.ProviderId`
- [ ] 4.9 反序列化 `WeighingRecord.MaterialsJson`，使用 `materialIdMap` 重写 `WeighingRecordMaterial.MaterialId`（跳过 null MaterialId），序列化回并赋值给 `MaterialsJson`

## 5. Phase B — 验证与提交

- [ ] 5.1 查询孤立 FK 引用（Waybill/WaybillMaterial/WeighingRecord 的 FK 和 MaterialsJson 嵌套的 MaterialId 不匹配任何 Material/Provider Id）并记录警告
- [ ] 5.2 成功时提交事务
- [ ] 5.3 在 catch 块中确保事务回滚并记录错误日志
- [ ] 5.4 在 finally 块中恢复 `DisableAuditConcepts = false`
