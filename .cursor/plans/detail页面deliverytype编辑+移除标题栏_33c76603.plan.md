---
name: detail页面DeliveryType编辑+移除标题栏
overview: 在详情页为 WeighingRecord 增加可编辑的 DeliveryType 下拉框（Waybill 不显示），并移除现有 Title Bar 使内容整体上移；同时确保固废/标准两种模式保存时都能把 DeliveryType 写回数据库。
todos:
  - id: vm-deliverytype-state
    content: 在 AttendedWeighingDetailViewModel 增加 IsWeighingRecord/SelectedDeliveryType/Options，并在变更时联动更新依赖显示字段
    status: pending
  - id: ui-deliverytype-dropdown
    content: 在 StandardModeFormView.axaml 与 SolidWasteModeFormView.axaml 增加 DeliveryType 下拉，并仅 WeighingRecord 可见
    status: pending
  - id: persist-deliverytype-solidwaste
    content: 扩展 UpdateSolidWasteModeInput 并在 WeighingMatchingService.UpdateSolidWasteModeAsync(WeighingRecord) 写入 DeliveryType；ViewModel 保存时传入
    status: pending
  - id: remove-titlebar-shift-rows
    content: 删除 AttendedWeighingDetailView.axaml Title Bar，并调整 Grid 行号让内容整体上移
    status: pending
  - id: smoke-check
    content: 本地启动后手工验证：WeighingRecord/Waybill 显示差异、两模式保存落库、布局上移无错位
    status: pending
isProject: false
---

## 目标

- **仅当详情项为 `WeighingRecord`** 时，在 Detail 页面提供 `DeliveryType`（收料/发料）下拉框可编辑。
- **当详情项为 `Waybill`** 时不显示该选项。
- **固废模式与标准模式都生效**：UI 都能编辑、保存都能落库。
- 删除 `AttendedWeighingDetailView.axaml` 顶部 Title Bar（你标注的 29-50 行），其余内容整体上移，且对两种模式都生效。

## 现状要点（用于定位改动点）

- 详情页容器：`MaterialClient/Views/Controls/AttendedWeighingDetailView.axaml`
  - Title Bar 目前在 Grid.Row=0（包含 `DeliveryTypeTitleText` 和日期），下方第一块信息在 Grid.Row=1。
  - 模式切换区域在 Grid.Row=2/3：`StandardModeFormView` 与 `SolidWasteModeFormView`。
- 两个模式表单：
  - `MaterialClient/Views/Controls/StandardModeFormView.axaml`
  - `MaterialClient/Views/Controls/SolidWasteModeFormView.axaml`
- 保存逻辑：`MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs`
  - 标准模式走 `UpdateListItemAsync(UpdateListItemInput)`，该 input **已包含 `DeliveryType?`**（`MaterialClient.Common/Services/WeighingMatchingService.cs` 中的 record 定义）。
  - 固废模式走 `UpdateSolidWasteModeAsync(UpdateSolidWasteModeInput)`，目前 **不包含 DeliveryType**，需要扩展以满足“固废模式也落库”。

## 方案设计

- **ViewModel 增加编辑态字段**（`AttendedWeighingDetailViewModel`）：
  - `IsWeighingRecord`（bool）：`_listItem.ItemType == WeighingListItemType.WeighingRecord`。
  - `DeliveryTypeOptions`：提供下拉源（两项：Receiving/Sending + 中文显示）。
  - `SelectedDeliveryType`（DeliveryType? 或 DeliveryType）：
    - 初始化时来自 `_listItem.DeliveryType`；
    - 用户修改后同步写回 `_listItem.DeliveryType`，并触发 `ProviderLabelText` / `DeliveryTypeTitleText` / `CompleteButtonText` 的 `RaisePropertyChanged`（它们当前依赖 `_listItem.DeliveryType`）。
- **UI 增加下拉框**（仅 WeighingRecord 可见）：
  - 在 `StandardModeFormView.axaml` 与 `SolidWasteModeFormView.axaml` 的表单头部区域各插入一行“收发类型”下拉。
  - `IsVisible` 绑定 `IsWeighingRecord`，确保 Waybill 不出现。
- **保存落库**：
  - 标准模式：在 `UpdateListItemInput(...)` 的 `DeliveryType` 参数位置传 `SelectedDeliveryType`（目前传了 `null`）。
  - 固废模式：扩展 `UpdateSolidWasteModeInput` 增加 `DeliveryType? DeliveryType` 字段；在 `WeighingMatchingService.UpdateSolidWasteModeAsync` 的 WeighingRecord 分支中更新 `record.DeliveryType`；在 `AttendedWeighingDetailViewModel.SaveSolidWasteModeAsync` 传入该字段。
- **移除 Title Bar 并上移内容**：
  - 在 `AttendedWeighingDetailView.axaml` 删除 Grid.Row=0 的 Title Bar Border。
  - 调整 `Grid.RowDefinitions` 与后续控件的 `Grid.Row`：
    - 让“第一行信息块”从 Row=1 改到 Row=0；
    - 模式表单区与按钮栏整体上移（Row 索引减 1），以保持布局不变但去掉标题占位。

## 需要改动的文件

- `MaterialClient/Views/Controls/AttendedWeighingDetailView.axaml`
  - 删除 Title Bar（29-50 行）并调整 Grid 行号。
- `MaterialClient/Views/Controls/StandardModeFormView.axaml`
  - 增加 DeliveryType 下拉行（`IsVisible={Binding IsWeighingRecord}`）。
- `MaterialClient/Views/Controls/SolidWasteModeFormView.axaml`
  - 增加 DeliveryType 下拉行（`IsVisible={Binding IsWeighingRecord}`）。
- `MaterialClient/ViewModels/AttendedWeighingDetailViewModel.cs`
  - 新增 `IsWeighingRecord`、`DeliveryTypeOptions`、`SelectedDeliveryType`，并在保存时传入。
- `MaterialClient.Common/Services/WeighingMatchingService.cs`
  - 扩展 `UpdateSolidWasteModeInput` 增加 `DeliveryType? DeliveryType`；
  - `UpdateSolidWasteModeAsync` 在 WeighingRecord 分支写入 DeliveryType。

## 修改后布局演示

### Detail 容器（删除 Title Bar 后）

```text
┌──────────────────────────────────────────────────────────────┐
│ [第一行信息块] 毛重/皮重/净重 + 进场/出场时间 + 操作员          │  <- Row 0（原 Row 1）
├──────────────────────────────────────────────────────────────┤
│ [模式表单区]                                                  │  <- Row 1~2（原 Row 2~3）
│  ├─ StandardModeFormView（标准模式）                         │
│  └─ SolidWasteModeFormView（固废模式）                        │
├──────────────────────────────────────────────────────────────┤
│ [按钮栏] 保存 / 下一个 / 完成 / 匹配 / 废单                    │  <- Row 3（原 Row 4）
└──────────────────────────────────────────────────────────────┘
```

### StandardModeFormView（新增收发类型下拉）

```text
车牌号      [___________]
收发类型    [收料 ▼]        <- 仅 WeighingRecord 可见（Waybill 隐藏）
供应商      [___________]
备注        [___________]
-----------------------------------
DataGrid(材料明细)
```

### SolidWasteModeFormView（新增收发类型下拉）

```text
车牌号      [___________]
收发类型    [收料 ▼]        <- 仅 WeighingRecord 可见（Waybill 隐藏）
供应商      [___________]
材料名称    [___________]
联单编号    [___________]
所属镇街    [___________]
类型选择    [___________]
备注        [___________]
```

### 可见性规则演示

```text
if ItemType == WeighingRecord:
    显示 "收发类型" 下拉（标准/固废都显示）
else if ItemType == Waybill:
    不显示 "收发类型" 下拉（标准/固废都隐藏）
```

## 验证方式（手工）

- 打开 Detail：
  - 选择一个 `WeighingRecord`：应出现“收发类型”下拉；切换后 `ProviderLabelText`、`CompleteButtonText` 等随之变化。
  - 选择一个 `Waybill`：不出现该下拉。
- 保存：
  - 标准模式 WeighingRecord：保存后再次进入应保持 DeliveryType。
  - 固废模式 WeighingRecord：保存后再次进入应保持 DeliveryType。
- 布局：Title Bar 不再显示，原“第一行信息块”贴近顶部，固废/标准两模式一致。

