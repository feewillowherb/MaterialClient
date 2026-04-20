# MaterialClient 与 Materials 同步改造调研报告

## 背景与目标

当前客户端在“新增/更新物料”场景下，仍存在直接在本地数据源落库的路径。目标是将这些写操作统一改为调用 `FdSoft.Materials` 服务端接口，由服务端完成数据持久化与业务校验，客户端仅作为交互与编排层。

本次调研仅覆盖“新增、更新”链路，不包含删除、批量导入、历史追溯等扩展需求。

## 关键前提

- 本地已有数据不通过客户端程序自动迁移。
- 由运维团队将既有本地数据自行迁移到服务端并负责迁移验证。
- 客户端上线后，新增与更新以服务端为唯一写入口（Single Source of Truth）。

## 改造范围总览

- **MaterialClient（客户端）**：把本地写入逻辑替换为调用 `FdSoft.Materials` 接口；完善异常处理、重试策略、用户提示与本地缓存刷新。
- **Materials（服务端）**：提供/完善新增与更新接口，统一校验、幂等、并发控制、审计与可观测性能力，确保可被客户端稳定调用。

---

## 一、给 MaterialClient 项目的调研结论

### 1. 需要调整的功能点

- 梳理所有“新增物料”“编辑物料”入口（UI、ViewModel、Service、Repository）。
- 将本地仓储写入（Insert/Update）改为应用服务或网关服务的 HTTP/RPC 调用。
- 写操作完成后，从服务端重新拉取单条或列表数据，避免本地状态与服务端状态不一致。

### 2. 建议的客户端分层改造

- 新增 `IMaterialsRemoteService`（或同等职责接口）封装远程调用。
- 原本直接访问本地数据层的业务服务改为依赖该远程服务。
- ViewModel 保持“命令 -> 服务调用 -> UI反馈”模式，避免感知底层传输细节。

### 3. 数据契约与对象映射

- 明确客户端 DTO 与服务端契约字段映射（必填、可空、默认值、枚举值）。
- 对“客户端历史字段”做兼容策略：
  - 服务端已支持：直接透传；
  - 服务端未支持：在客户端禁用编辑或给出显式提示；
  - 命名/语义不一致：在映射层集中转换，避免散落在 UI 或业务代码。

### 4. 交互与异常处理建议

- 新增/更新成功后提示“已同步到服务端”。
- 网络异常、超时、鉴权失败、业务校验失败需区分提示文案。
- 对可恢复错误（如短时网络波动）可加入有限重试（建议最多 1~2 次，指数退避）。
- 对不可恢复错误（如业务规则不满足）直接展示服务端返回原因。

### 5. 离线与可用性策略

- 明确本次是否支持离线写入：
  - 若不支持：离线状态禁用“保存”，并提示“需连接服务端”；
  - 若支持（后续增强）：采用本地队列 + 后台补偿同步，但不建议在本阶段引入。

### 6. 测试建议（客户端）

- 单元测试：映射、错误分支、重试分支。
- 集成测试：对接测试环境服务端，覆盖新增成功、更新成功、校验失败、并发冲突。
- 回归测试：确保依赖本地读缓存的页面在写后刷新行为正确。

---

## 二、给 Materials（FdSoft.Materials）参考的调研结论

### 1. 需要提供或确认的接口能力

- 新增物料接口（Create）。
- 更新物料接口（Update）。
- 按主键/业务键查询接口（用于写后回读）。
- 可选：批量查询接口（优化客户端列表刷新成本）。

### 2. 服务端业务规则与一致性

- 在服务端统一执行字段校验（必填、格式、长度、枚举合法性）。
- 执行唯一性约束校验（如物料编码、名称组合键等）。
- 对更新操作增加并发控制（建议版本号/时间戳机制）。
- 保证接口幂等性（至少在客户端重试时避免重复创建）。

### 3. 返回模型与错误语义

- 统一成功返回结构（状态码、业务码、消息、数据体）。
- 统一失败返回结构（可定位字段错误、业务冲突、权限问题）。
- 提供可直接用于客户端展示的错误消息，或提供错误码字典供客户端映射。

### 4. 安全与权限

- 校验客户端身份（令牌、签名或网关鉴权策略）。
- 按角色或组织范围控制新增/更新权限。
- 关键字段变更建议记录审计日志（操作人、时间、变更前后值）。

### 5. 可观测性与运维支持

- 输出接口调用日志（traceId、请求参数摘要、响应码、耗时）。
- 建立关键指标：成功率、P95/P99 延迟、4xx/5xx 比例。
- 对常见失败场景（重复编码、并发冲突）建立可检索告警维度。

### 6. 测试建议（服务端）

- 合约测试：确保接口字段与约束稳定。
- 并发测试：验证更新冲突处理是否符合预期。
- 兼容测试：验证对旧客户端版本的影响边界（若需要灰度兼容）。

---

## 三、接口 API 定义（建议稿）

以下为建议的 REST API 契约，用于 `MaterialClient` 与 `FdSoft.Materials` 联调。最终以服务端发布的 OpenAPI 文档为准。

### 1. 通用约定

- 路由风格参考当前 `FdSoft.Material.PublicApi/Controllers`：`api/[controller]/[action]`
- 建议本次 Simple API 使用控制器路由：`/api/Material/[action]`
- 鉴权：`Authorization: Bearer <token>`
- Content-Type：`application/json`
- 幂等请求头（Create 可选）：`X-Idempotency-Key: <uuid>`
- 追踪请求头（可选）：`X-Correlation-Id: <trace-id>`
- 本次仅设计和实现 Simple API（仅名称输入），不设计 Standard API。

统一响应包装建议：

```json
{
  "code": "OK",
  "message": "success",
  "traceId": "4c3b20aef1ad45a6b8e31d8f2d8f56a1",
  "data": {}
}
```

### 2. 接口策略（仅 Simple）

- 客户端只传 `name`（和更新时的 `version`），服务端负责补全复杂字段。
- 接口命名尽量与现有 PublicApi Action 风格保持一致（动词+业务名），避免引入 REST 风格混用。
- 建议客户端封装专用方法：`CreateMaterialByNameAsync`、`RenameMaterialAsync`，避免 UI 直接拼接 HTTP 细节。

### 3. Simple API：按名称创建（Create By Name）

- Method & Path（建议，参考现有 Controller 风格）：`POST /api/Material/CreateMaterialByName`
- 用途：客户端仅提交名称，由服务端按默认策略创建完整物料对象。

请求体示例（仅名称）：

```json
{
  "name": "冷轧钢板"
}
```

成功响应（200/201）示例：

```json
{
  "code": "OK",
  "message": "Material created by name",
  "traceId": "2d9fdbf7f5a64453be9c4673e09b2455",
  "data": {
    "id": "8f8b4d6a-40ec-4ca9-95d3-3c9f7f8f3511",
    "materialCode": "M-10001",
    "materialName": "冷轧钢板",
    "specification": "",
    "unit": "pcs",
    "categoryCode": "UNCLASSIFIED",
    "isEnabled": true,
    "remark": "",
    "version": 1,
    "updatedAt": "2026-04-20T08:00:00Z"
  }
}
```

### 4. Simple API：按名称重命名（Rename）

- Method & Path（建议，参考现有 Controller 风格）：`POST /api/Material/RenameMaterialByName`
- 用途：仅修改名称；需携带 `version` 做并发控制，其他复杂字段不允许由客户端变更。

请求体示例（仅名称 + 版本）：

```json
{
  "id": "8f8b4d6a-40ec-4ca9-95d3-3c9f7f8f3511",
  "name": "冷轧钢板-A",
  "version": 1
}
```

成功响应（200）示例：

```json
{
  "code": "OK",
  "message": "Material renamed",
  "traceId": "f89e91ca4df742f1ab8042f2f53f7ea5",
  "data": {
    "id": "8f8b4d6a-40ec-4ca9-95d3-3c9f7f8f3511",
    "materialCode": "M-10001",
    "materialName": "冷轧钢板-A",
    "specification": "",
    "unit": "pcs",
    "categoryCode": "UNCLASSIFIED",
    "isEnabled": true,
    "remark": "",
    "version": 2,
    "updatedAt": "2026-04-20T08:30:00Z"
  }
}
```

### 5. 写后回读（Get By Id / Get By Code）

- `POST /api/Material/GetById`：按主键查询（请求体传 `id`）。
- `POST /api/Material/GetByCode`：按业务编码查询（请求体传 `materialCode`）。

客户端在新增/更新成功后，建议按 `id` 回读一次，确保界面展示与服务端最终状态一致。

### 6. 列表查询（可选但推荐）

- Method & Path（建议）：`POST /api/Material/ListByKeyword`
- Query 参数建议：
  - `keyword`：关键字（编码/名称）
  - `categoryCode`：分类过滤
  - `isEnabled`：启用状态
  - `page` / `pageSize`：分页
- 用途：支持客户端列表刷新与检索。

### 7. 错误码约定（建议）

| HTTP Status | 业务码 | 场景 | 客户端建议处理 |
|---|---|---|---|
| 400 | `NAME_REQUIRED` | 名称为空 | 直接提示“名称不能为空” |
| 400 | `NAME_TOO_LONG` | 名称超过长度限制 | 提示长度超限 |
| 400 | `VALIDATION_ERROR` | 字段校验失败 | 直接展示字段错误 |
| 401 | `UNAUTHORIZED` | 未登录或令牌无效 | 引导重新登录 |
| 403 | `FORBIDDEN` | 无权限新增/更新 | 提示无权限并记录日志 |
| 404 | `MATERIAL_NOT_FOUND` | 更新目标不存在 | 提示数据已不存在并刷新列表 |
| 409 | `NAME_DUPLICATE` | 名称重复（按业务唯一策略） | 提示修改名称后重试 |
| 409 | `MATERIAL_CODE_CONFLICT` | 编码重复 | 提示修改编码后重试 |
| 409 | `VERSION_CONFLICT` | 并发版本冲突 | 提示“数据已被他人修改”，触发回读 |
| 500 | `INTERNAL_ERROR` | 服务端异常 | 显示通用错误并上报 traceId |

### 8. 默认值与服务端补全策略（Simple API 关键）

- `materialCode`：由服务端规则生成（如前缀 + 流水号），客户端不传。
- `specification`：默认空字符串或模板默认值。
- `unit`：默认值（例如 `pcs`），由服务端统一配置。
- `categoryCode`：默认分类（例如 `UNCLASSIFIED`），由服务端统一配置。
- `isEnabled`：默认 `true`。
- `remark`：默认空。
- 以上默认策略必须集中在单一策略组件（如 `MaterialDefaultValuePolicy`），避免散落。

### 10. Provider（供应商）对象的特殊封装建议

由于客户端在本期仅提供“名称”输入，无法维护供应商关联、结算规则、主供标记等复杂属性，建议由服务端对 Material-Provider（物料-供应商）关系进行统一封装：

- 客户端请求只传 `name`（以及更新时 `version`），不传 `providerId`、`providerCode`、`providerPrice` 等复杂字段。
- 服务端在 Simple API 中执行“供应商补全策略”：
  - 按规则绑定默认供应商（如系统默认供应商、组织默认供应商）。
  - 若无默认供应商，创建时允许空关系，但返回 `providerBindingStatus` 供客户端提示。
  - 对需要强制供应商的租户/组织，直接返回业务错误，阻止创建成功但数据不完整。
- 建议在响应中增加供应商摘要字段，避免客户端二次拼装：
  - `providerId`
  - `providerName`
  - `providerBindingStatus`（`BOUND` / `UNBOUND` / `REQUIRED`）
- 重命名接口（`simple-rename`）默认不允许修改供应商关系；供应商维护通过独立接口完成，防止职责混淆。
- 若后续需要客户端可选供应商，建议新增扩展接口而不是破坏 Simple API 纯名称契约，例如：
  - `POST /api/Material/BindProvider`
  - 请求体：`providerId`、`version`
  - 仅处理物料-供应商绑定，不处理名称与其他字段。

### 11. 当前代为实现位置（客户端现状接口）

当前在客户端侧，已有“代为创建/更新”的接口形态如下（本次改造需要将其内部实现切换为调用 `FdSoft.Materials` API，而非本地直写）：

```csharp
public async Task<Material> CreateMaterialAsync(string materialName)

/// <summary>
///     新增供应商
/// </summary>
/// <param name="providerName">供应商名称</param>
/// <param name="deliveryType">当前称重记录/联单的 DeliveryType</param>
Task<Provider> CreateProviderAsync(string providerName, DeliveryType deliveryType);

/// <summary>
///     更新供应商信息
/// </summary>
Task<ProviderDto> UpdateProviderAsync(int id, string providerName, string? contactName, string? contactPhone);
```

改造约束建议：

- `CreateMaterialAsync(string materialName)`：保持“仅名称输入”契约，对应调用 `simple-create`。
- `CreateProviderAsync(...)`：保持与 `DeliveryType` 的业务绑定，由服务端决定默认属性填充与规则校验。
- `UpdateProviderAsync(...)`：保留“轻量更新”语义，避免扩展为全量供应商字段覆盖，防止误改。
- 上述接口签名可先保持不变，通过实现层切换远程调用，降低 UI/ViewModel 改造成本。

### 12. 最小字段定义（Simple）

- **Simple Create 请求**：`name`
- **Simple Rename 请求**：`name` + `version`
- **Simple 响应建议补充**：`providerId`、`providerName`、`providerBindingStatus`

> 注：若现有服务端字段名与以上示例不一致，建议以服务端统一命名为准，客户端通过映射层适配。当前版本仅包含 Simple API，不引入 Standard API。

---

## 四、迁移边界与职责划分

### 运维侧职责（已确定）

- 负责将本地历史数据迁移到 `FdSoft.Materials`。
- 输出迁移结果核对清单（总量、失败记录、抽样一致性）。
- 在切换窗口前完成迁移并提供“可切换”确认。

### 开发侧职责

- 客户端切断本地新增/更新写路径。
- 服务端保证新写入路径稳定可用且具备可观测性。
- 双方对齐字段与错误语义，完成联调验证。

---

## 五、实施建议（分阶段）

1. **接口与契约冻结**：先冻结 Create/Update 输入输出契约与错误码。
2. **客户端改造**：替换写入链路，保留读取兼容（必要时短期双读）。
3. **联调与灰度**：在测试/预发环境验证核心场景，再灰度发布。
4. **生产切换**：运维完成迁移后，开启仅服务端写入模式。
5. **观察与收敛**：重点观察 1~2 周，处理错误码与性能瓶颈。

## 六、风险与应对

- **字段不一致风险**：通过契约文档 + 联调用例前置发现。
- **网络不稳定风险**：客户端增加超时与轻量重试，服务端提供稳定 SLA。
- **并发覆盖风险**：服务端引入版本控制并返回冲突错误码。
- **迁移质量风险**：由运维提供核对报告，业务侧做抽样验收。

## 七、结论

该改造可将“数据写入责任”集中到 `FdSoft.Materials`，降低客户端本地写入分叉带来的一致性风险。由于历史数据迁移由运维负责，开发改造可聚焦于“新数据写入路径切换 + 契约对齐 + 稳定性保障”，总体改造可控，建议按阶段推进并配合灰度验证。
