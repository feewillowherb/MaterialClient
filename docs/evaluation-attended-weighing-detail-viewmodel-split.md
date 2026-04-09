# AttendedWeighingDetailViewModel 拆分评估报告

> 日期：2026-04-02
> 状态：评估完成，建议执行

## 1. 背景

`AttendedWeighingDetailViewModel`（1410 行）以 `IsSolidWasteMode` 布尔开关在同一个类内分叉处理**标准模式**和**固废模式**两种业务。View 层已拆分为 `StandardModeFormView` / `SolidWasteModeFormView` 两个独立控件，但 ViewModel 始终合一，导致职责混杂、条件分支泛滥。

## 2. 现状问题分析

### 2.1 属性交织

| 维度 | 标准模式 | 固废模式 | 交织方式 |
|------|----------|----------|----------|
| 属性 | `Providers`, `Materials`, `SelectedProvider`, `MaterialItems` | `SolidWasteMaterials`, `SelectedSolidWasteMaterial`, `SelectedStreet`, `SolidWasteOrderNumber`, `SelectedSolidWasteType` | 共存于同一类，约 15 个 Reactive 字段各有归属 |
| 加载 | `LoadProvidersAsync` + `LoadMaterialsAsync` + 初始化 `MaterialItemRow` | `LoadSolidWasteDataAsync`（读 ExtraProperties） | `LoadDropdownDataAsync` 内 `if/else` 分叉 |
| 保存 | `UpdateListItemAsync` | `SaveSolidWasteModeAsync`（ExtraProperties） | `SaveAsync` 内 `if/else` 分叉 |
| 完成 | `CompleteStandardModeAsync` | `CompleteSolidWasteModeAsync` | `CompleteAsync` 内 `if/else` 分叉 |
| 验证 | 验证 supplier/material/unit/quantity | 验证 supplier/material/street/type/orderNumber | 完全不同的校验规则 |
| UI | `StandardModeFormView`（DataGrid 多行） | `SolidWasteModeFormView`（SearchableSelectionBox 单项） | 已在 View 层分离 |

### 2.2 条件分支统计

`if (IsSolidWasteMode)` 在类中出现 **5+ 处**：

1. **构造函数**：`WeighingMode` → `IsSolidWasteMode` 映射
2. **构造函数**：固废模式材料选择自动单位（`SelectedSolidWasteMaterial` 订阅）
3. **构造函数**：固废模式运单数量自动计算（`GoodsWeight` 订阅）
4. **`LoadDropdownDataAsync`**：标准模式初始化 `MaterialItemRow` vs 固废模式 `LoadSolidWasteDataAsync`
5. **`SaveAsync`**：`SaveSolidWasteModeAsync` vs `UpdateListItemAsync`
6. **`CompleteAsync`**：`CompleteSolidWasteModeAsync` vs `CompleteStandardModeAsync`

### 2.3 固废模式独有字段（约占 40%）

```csharp
// 固废模式独有 Reactive 属性
[Reactive] private WeighingMode _weighingMode;
[Reactive] private bool _isSolidWasteMode;
[Reactive] private string? _solidWasteOrderNumber;
[Reactive] private ObservableCollection<string> _streets;
[Reactive] private string? _selectedStreet;
[Reactive] private ObservableCollection<string> _solidWasteTypes;
[Reactive] private string? _selectedSolidWasteType;
[Reactive] private ObservableCollection<Material> _solidWasteMaterials;
[Reactive] private Material? _selectedSolidWasteMaterial;
[Reactive] private SelectionItem? _selectedProviderItem;
[Reactive] private SelectionItem? _selectedMaterialItem;
[Reactive] private SelectionItem? _selectedStreetItem;

// 固废模式独有委托属性
public Func<...> ProviderLoadPageAsync { get; }
public Func<...> MaterialLoadPageAsync { get; }
public Func<...> StreetLoadPageAsync { get; }
public Func<...>? ProviderCreateNewAsync { get; }
public Func<...>? MaterialCreateNewAsync { get; }
```

## 3. 拆分合理性论证

### 3.1 单一职责原则（SRP）

当前类承载两套几乎无交叉的业务逻辑。固废模式独有的属性和委托在标准模式下全无用途，违反 SRP。

### 3.2 View 层已分离

XAML 已拆为两个独立控件（`StandardModeFormView` / `SolidWasteModeFormView`），唯独 ViewModel 还是一坨。这是架构不一致的根因。

### 3.3 可维护性

未来固废模式若增加字段（如新的 ExtraProperties），改动只影响固废 ViewModel，不会误伤标准模式。

### 3.4 结论

**合理，推荐执行。** 当前架构是典型的"上帝 ViewModel"反模式前兆。

## 4. 推荐拆分方案

### 4.1 类层次结构

```
AttendedWeighingDetailViewModel (抽象基类)
│
├── 共享属性
│   AllWeight, TruckWeight, GoodsWeight, PlateNumber,
│   Remark, JoinTime, OutTime, Operator, WeighingRecordId,
│   SelectedDeliveryType, DeliveryTypeOptions, IsWeighingRecord,
│   MaterialItems, _listItem, _capturedBillPhotoPath
│
├── 共享命令
│   AbolishAsync, Close, MatchAsync
│
├── 共享事件
│   SaveCompleted, AbolishCompleted, CloseRequested,
│   CompleteCompleted, MatchCompleted, ManualMatchSaveCompleted
│
├── 共享方法
│   ShowMessageBoxAsync, ShowMessageBoxAsyncWithoutBlocking,
│   GetParentWindow, InitializeData (部分), OnSaveCompleted()
│
├── StandardWeighingDetailViewModel
│   ├── 独有属性：Providers, SelectedProvider, Materials,
│   │             SelectedProviderId, MaterialsSelectionPopupViewModel...
│   ├── 独有方法：LoadProvidersAsync, LoadMaterialsAsync,
│   │             CompleteStandardModeAsync, AddMaterialAsync...
│   └── 独有命令：SaveAsync (标准), CompleteAsync (标准)
│
└── SolidWasteWeighingDetailViewModel
    ├── 独有属性：SolidWasteMaterials, SelectedSolidWasteMaterial,
    │             SelectedProviderItem, SelectedMaterialItem,
    │             SelectedStreetItem, SolidWasteOrderNumber,
    │             Streets, SolidWasteTypes, ProviderLoadPageAsync...
    ├── 独有方法：LoadSolidWasteDataAsync, LoadStreetsPageAsync,
    │             CompleteSolidWasteModeAsync, CreateNewProviderAsync...
    └── 独有命令：SaveAsync (固废), CompleteAsync (固废)
```

### 4.2 关键设计决策

| 决策点 | 方案 |
|--------|------|
| `InitializeData` 方法 | 拆为 `base.InitializeCommonData()` + 子类 `override InitializeModeSpecificData()` |
| `SaveAsync` 末尾 BillPhoto + 事件发送 | 提取为 `protected void OnSaveCompleted()` 在基类中 |
| 构造函数 ReactiveUI 订阅链 | 基类构造先执行共享订阅，子类追加独有订阅（C# 构造顺序天然保证） |
| `LoadDropdownDataAsync` | 基类加载共享数据，子类 override 完成各自加载逻辑 |
| 抽象方法 | `protected abstract Task SaveModeSpecificAsync()` / `protected abstract Task CompleteModeSpecificAsync()` |

## 5. 改动影响评估

| 改动项 | 影响范围 | 复杂度 | 说明 |
|--------|----------|--------|------|
| 提取基类 | 低 | ⭐⭐ | 共享逻辑明确，无歧义 |
| 拆分两个子类 | 中 | ⭐⭐⭐ | 需调整 ReactiveUI 订阅的 `this.` 引用 |
| `AttendedWeighingViewModel.OpenDetail` | 低 | ⭐ | 根据 `WeighingMode` 创建不同子类实例 |
| `AttendedWeighingDetailView.axaml` | 低 | ⭐ | 已有条件视图切换，只需绑定到对应子类 |
| DI 注册 | 低 | ⭐ | 两个子类分别注册为 Transient |
| 事件订阅（父级） | 低 | ⭐ | 事件定义在基类，无需改签名 |
| 单元测试 | 中 | ⭐⭐ | 需分别覆盖两套逻辑 |

## 6. 风险点与应对

| 风险 | 应对 |
|------|------|
| ReactiveUI 订阅链拆分 | C# 构造顺序：基类构造先执行 → 子类追加。`WhenAnyValue` 链无碍 |
| `InitializeData` 中共享初始化 + 模式分支 | 拆为 `base.InitializeCommonData()` + 子类 override |
| `SaveAsync` 末尾共享逻辑（BillPhoto、事件） | 提取为 `protected void OnSaveCompleted()` |
| 子类 View 绑定兼容性 | XAML 中 `DataContext` 类型改为基类，属性绑定自动兼容 |

## 7. 预期效果

拆分后：

- **基类**：~400 行（共享逻辑 + 抽象定义）
- **StandardWeighingDetailViewModel**：~400 行
- **SolidWasteWeighingDetailViewModel**：~500 行

每个类职责单一，与 View 层已有的分离策略对齐，改动量可控，风险低。

## 8. 涉及文件清单

### 需新建
- `ViewModels/AttendedWeighingDetailViewModelBase.cs`（或保留原名作为基类）
- `ViewModels/StandardWeighingDetailViewModel.cs`
- `ViewModels/SolidWasteWeighingDetailViewModel.cs`

### 需修改
- `ViewModels/AttendedWeighingViewModel.cs`（`OpenDetail` 中根据模式创建子类）
- `Views/Controls/AttendedWeighingDetailView.axaml`（DataContext 类型适配）
- DI 注册配置

### 不需改动
- `Views/Controls/StandardModeFormView.axaml`（已独立）
- `Views/Controls/SolidWasteModeFormView.axaml`（已独立）
- `MaterialItemRow` 类（随基类移动）
- 服务层（`WeighingMatchingService`、`SolidWasteService` 等）
