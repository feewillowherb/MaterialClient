# 有人值守称重详情：方案 B（双 View + 双 ViewModel + Host 壳）分离初稿

**日期**: 2026-03-17  
**状态**: 初稿 / 待评审  
**关联**: `Waybill.WeighingMode`（`Standard` / `SolidWaste`）、`AttendedWeighingDetailViewModel`、`AttendedWeighingViewModel`

---

## 一、目标

- **单一职责**：标准模式与固废模式的**表单字段、校验、加载、保存/完成**互不污染；各自 View / ViewModel 内不出现 `if (IsSolidWasteMode)` 式分支。
- **纯净 View**：通过 **不同 UserControl + DataTemplate** 切换整块详情 UI，而非在同一 XAML 内大量 `IsVisible`/Converter 切换。
- **行为不变**：对外仍由 `AttendedWeighingViewModel.OpenDetail` 打开详情；MessageBus / 事件（`SaveCompleted`、`CompleteCompleted` 等）与现有父窗口契约保持一致或可平滑迁移。

---

## 二、现状简述

| 位置 | 问题 |
|------|------|
| `AttendedWeighingDetailViewModel` | `WeighingMode` 分支贯穿：构造订阅、`LoadDropdownDataAsync`、`SaveAsync`、`CompleteAsync`、固废专属弹窗与 `LoadSolidWasteDataAsync` 等。 |
| `AttendedWeighingViewModel` | 列表侧摘要仍有 `WeighingMode` 分支（如 Block6 净重/偏差）；可选后续用独立 Formatter 或保留在列表 VM（与详情拆分独立）。 |

本初稿**以详情页方案 B 为主**；主列表摘要可另起小步重构。

---

## 三、方案 B 结构总览

```
AttendedWeighingDetailHostViewModel          ← 壳：选子 VM、转发命令/事件、共用流程
├── Current: ReactiveObject?                  ← StandardDetailVM | SolidWasteDetailVM
├── InitializeData(listItem, billPhotoPath)
├── SaveCommand / CompleteCommand / …        ← 委托给 Current 或壳内共用逻辑
└── 事件：SaveCompleted, CompleteCompleted, …（与现 AttendedWeighingDetailViewModel 对齐）

AttendedWeighingDetailHostView.axaml
└── ContentControl Content="{Binding Current}"
    DataTemplates:
      StandardAttendedWeighingDetailViewModel → StandardAttendedWeighingDetailView
      SolidWasteAttendedWeighingDetailViewModel → SolidWasteAttendedWeighingDetailView
```

### 3.1 Host（壳）职责

| 职责 | 说明 |
|------|------|
| 路由 | `InitializeData` 时根据 `listItem.WeighingMode` 实例化并赋值 `Current`。 |
| 共用上下文 | 持有 `_listItem` 引用（或只读快照）、`_capturedBillPhotoPath`；与模式无关的标题类属性可挂在 Host（如 `ProviderLabelText`、`DeliveryTypeTitleText`、`CompleteButtonText`）。 |
| 共用后置逻辑 | 保存/完成后的：BillPhoto 附件 `CreateOrReplaceBillPhotoAsync`、发送 `SaveCompletedMessage`、`SaveCompleted`/`CompleteCompleted` 事件、通知 Toast——可统一在 Host 的 `AfterSaveAsync` 中执行，子 VM 只负责「写业务数据」。 |
| 关闭/废单/匹配 | 若行为两模式一致，命令在 Host；若仅标准有「匹配」，可由 Host 在 `Current` 为标准 VM 时启用 `MatchCommand`。 |

### 3.2 Standard 子 VM / View（纯净标准）

- **VM**：`StandardAttendedWeighingDetailViewModel`
  - 字段：`PlateNumber`、`Providers`、`SelectedProvider`、`MaterialItems`、`MaterialsSelectionPopup`、重量与时间等标准流程所需。
  - 方法：`LoadDropdownDataAsync`（供应商+材料+推荐填充）、`SaveCoreAsync`（`UpdateListItemAsync`）、`CompleteCoreAsync`（校验 + `UpdateListItemAsync` + `CompleteOrderAsync`）。
  - **不包含**：街道/固废类型/联单号、`GenericSelectionPopup`（镇街/固废材料/供应商分页三件套中仅固废专用部分）、`LoadSolidWasteDataAsync`。

- **View**：`StandardAttendedWeighingDetailView.axaml`
  - 仅绑定标准 VM；无固废控件。

### 3.3 SolidWaste 子 VM / View（纯净固废）

- **VM**：`SolidWasteAttendedWeighingDetailViewModel`
  - 字段：`SolidWasteOrderNumber`、`SelectedStreet`、`SelectedSolidWasteType`、`SelectedSolidWasteMaterial`、`StreetsPopup` / `MaterialsPopup` / `ProvidersPopup`、`MaterialItems`（若仍用首行承载单位）、`GoodsWeight` 与 `WaybillQuantity` 同步订阅等。
  - 方法：`LoadSolidWasteDataAsync`、`SaveCoreAsync`（`UpdateSolidWasteModeAsync`）、`CompleteCoreAsync`（固废校验 + `UpdateSolidWasteModeAsync` + `CompleteOrderAsync`）。

- **View**：`SolidWasteAttendedWeighingDetailView.axaml`
  - 仅绑定固废 VM；无标准多行物料弹窗选择（若产品要求固废单行，UI 更简单）。

---

## 四、建议接口契约（子 VM）

便于 Host 统一调度，子 VM 可实现同一接口（名称可调整）：

```csharp
public interface IAttendedWeighingDetailModeViewModel
{
    Task SaveAsync();           // 仅业务持久化；不含附件与 MessageBus
    Task CompleteAsync();       // 同上
    // 可选：ValidateForSave() / ValidateForComplete()
}
```

Host 流程示例：

1. `await ((IAttendedWeighingDetailModeViewModel)Current).SaveAsync();`
2. `await CreateBillPhotoIfNeededAsync();`
3. `MessageBus` + 事件 + 通知。

---

## 五、与 `AttendedWeighingViewModel` 的对接

| 现逻辑 | 调整后 |
|--------|--------|
| `DetailViewModel = GetRequiredService<AttendedWeighingDetailViewModel>()` | 改为 `AttendedWeighingDetailHostViewModel`（或保留属性名 `DetailViewModel` 但类型为 Host）。 |
| `DetailViewModel.InitializeData(item, path)` | Host 的 `InitializeData`。 |
| 订阅 `SaveCompleted`、`CompleteCompleted` 等 | 订阅 Host 上同名事件（Host 在子 VM 完成后转发或统一触发）。 |

**DI**：`AttendedWeighingDetailViewModel` 可标记 `[Obsolete]` 并逐步移除；Host + 两个子 VM 均 `ITransientDependency`，或由 Host 内部 `new` 子 VM（视是否需单独测试注入而定）。

---

## 六、DataTemplate 注册

在 `App.axaml` 或 Host 所在 ResourceDictionary 中：

```xml
<DataTemplate DataType="{x:Type vm:StandardAttendedWeighingDetailViewModel}">
  <views:StandardAttendedWeighingDetailView />
</DataTemplate>
<DataTemplate DataType="{x:Type vm:SolidWasteAttendedWeighingDetailViewModel}">
  <views:SolidWasteAttendedWeighingDetailView />
</DataTemplate>
```

Host 根布局：`ContentControl` 绑定 `Current`，由 Avalonia 根据运行时类型选模板。

---

## 七、实施任务清单（建议顺序）

1. **新增** `AttendedWeighingDetailHostViewModel` + `AttendedWeighingDetailHostView.axaml`，`Current` 先仍指向**现有** `AttendedWeighingDetailViewModel`（或整页 Content 仍为旧 View），保证打开详情无回归。
2. **抽取** `StandardAttendedWeighingDetailViewModel`，从现有类拷贝标准分支逻辑与属性；**抽取** `SolidWasteAttendedWeighingDetailViewModel`，拷贝固废分支。
3. **拆分 XAML**：从现有 `AttendedWeighingDetailView` 拆成两个 UserControl，Host 用 `ContentControl` + DataTemplate 切换。
4. **接线** Host 的 Save/Complete 后置逻辑与事件。
5. **替换** `AttendedWeighingViewModel` 中 `DetailViewModel` 类型与构造。
6. **删除或瘦身** 原 `AttendedWeighingDetailViewModel`（合并进 Host + 两子类后）。
7. **回归**：标准运单/记录保存与完成；固废保存与完成；BillPhoto；手动匹配（仅标准）；废单；关闭后导航。

---

## 八、风险与待决

| 项 | 说明 |
|----|------|
| **共用字段归属** | `PlateNumber`、`Remark`、重量若两边都要编辑，可只在 Host 暴露，子 VM 通过接口 `ApplyFromHost`/`CommitToHost` 同步，或复制到子 VM（Initialize 时写入）。需定一种，避免双源 truth。 |
| **MaterialItems 共享** | 固废仍用 `MaterialItemRow` 首行时，可只在固废 VM 内保留一行逻辑；标准保留多行。 |
| **测试与 Mock** | 子 VM 若依赖大量 `GetRequiredService`，可考虑把 `IWeighingMatchingService` 等注入子 VM 构造，便于单测。 |

---

## 九、非目标（本初稿不展开）

- 主列表 `AttendedWeighingViewModel.UpdateDisplayInfoFromListItem` 的净重/偏差分支（可另文 `WeighingModeSummaryFormatter`）。
- 后端 `Waybill.WeighingMode` 或 API 变更。

---

## 十、文档修订记录

| 版本 | 日期 | 说明 |
|------|------|------|
| 0.1 | 2026-03-17 | 初稿：方案 B 结构、职责划分、任务顺序与风险。 |
