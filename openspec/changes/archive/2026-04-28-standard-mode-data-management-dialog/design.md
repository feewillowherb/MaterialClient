## Context

现有 `DataManagementDialogWindow` 专为固废模式设计，使用 `SolidWasteExportRow`（18 列）和 `ISolidWasteService` 作为数据源。标准模式的台账数据来源于 `Waybill` 实体，展示字段与固废模式几乎不重叠（仅车牌号、备注存在语义对应）。项目已有 `WeighingMode` 枚举（Standard / SolidWaste）区分两种模式，且 `AttendedWeighingWindow` 是两种模式的共享入口。

当前文件组织：
```
Views/AttendedWeighing/
├── AttendedWeighingWindow.axaml        # 共享主窗口
├── DataManagementDialogWindow.axaml    # 固废模式台账对话框
└── ...
ViewModels/
├── DataManagementDialogViewModel.cs    # 固废模式台账 ViewModel
└── ...
Common/Models/
├── SolidWasteExportRow.cs              # 固废导出行 DTO
└── ...
```

## Goals / Non-Goals

**Goals:**
- 为标准模式提供独立的数据管理对话框，展示 15 列标准模式专用字段
- 保持与固废模式对话框一致的视觉风格（标题栏、查询区、分页栏布局）
- 根据当前 WeighingMode 自动路由到正确的对话框

**Non-Goals:**
- 不重构现有固废模式对话框
- 不实现标准模式 CSV/Excel 导出功能
- 不抽象共享基类（两种模式差异过大，独立文件更清晰）
- 不修改现有固废模式的任何行为

## Decisions

### D1: 独立文件而非参数化单文件

**选择**：创建独立的 `StandardDataManagementDialogWindow` 文件

**理由**：两种模式的列定义完全不重叠（17 列 vs 15 列，仅 2-3 列语义对应），数据来源完全不同（SolidWasteExportRow vs Waybill），参数化会导致单文件复杂度爆炸。

**替代方案**：
- 共享基类 + 继承：引入不必要的抽象层，两种模式的查询逻辑和数据映射完全不同
- 单文件 Mode 参数：违反 SRP，XAML 中需要大量条件逻辑

### D2: StandardExportRow 字段映射

```
标准模式列名          StandardExportRow 属性    Waybill 实体字段
───────────          ──────────────────────    ─────────────────
车牌号               PlateNumber               PlateNumber
类型                 DeliveryType              DeliveryType (收料/发料)
商品                 MaterialName              Material.Name (关联查询)
状态                 OrderType                 OrderType (首称中/已完成/已取消)
运单数量             PlanQuantity              OrderPlanOnPcs
运单重量             PlanWeight                OrderPlanOnWeight
扣量                 OffsetCount               OffsetCount
实际数量             ActualQuantity            OrderPcs
实际重量             ActualWeight              OrderGoodsWeight
单位换算             UnitConversion            MaterialUnitRate
进场时间             JoinTime                  JoinTime
出场时间             OutTime                   OutTime
供应商               ProviderName              Provider.Name (关联查询)
发货单号             OrderNo                   OrderNo
备注                 Remark                    Remark
```

**理由**：字段映射直接对应 Waybill 实体属性，`MaterialName` 和 `ProviderName` 需要通过 Include 关联查询获取。

### D3: 查询筛选字段

标准模式保留与固废模式相同的筛选结构：车牌号、类型（收料/发料）、商品名称、状态、日期范围。

**理由**：筛选模式一致，用户习惯统一。标准模式移除"发货单位"筛选（因为标准模式已有"供应商"列展示）。

### D4: 数据查询方式

使用 EF Core Repository 直接查询 `Waybill` 表，按 `WeighingMode == Standard` 过滤，通过 Include 关联 `Material` 和 `Provider` 表获取名称。分页使用 `Skip/Take`。

**理由**：与固废模式使用 `ISolidWasteService` 的模式一致，不需要创建新的 Service 接口。查询逻辑封装在 ViewModel 内即可。

### D5: 路由逻辑位置

在 `AttendedWeighingWindow.OpenLedgerManagementDialogAsync()` 中，根据当前 `WeighingMode` 决定打开哪种对话框。WeighingMode 可通过 `IConfiguration` 或 `SystemSettings` 获取。

```
组件层次结构
├── AttendedWeighingWindow
│   ├── OpenLedgerManagementDialogAsync()
│   │   ├── [WeighingMode.Standard]
│   │   │   └── StandardDataManagementDialogWindow
│   │   │       └── StandardDataManagementDialogViewModel
│   │   └── [WeighingMode.SolidWaste]
│   │       └── DataManagementDialogWindow
│   │           └── DataManagementDialogViewModel
│   └── DataManagementMenuPopup
└── ...
```

## Risks / Trade-offs

- **[代码重复]** → 分页、查询区域、标题栏布局与固废模式高度相似。接受此重复以换取独立性和清晰度。未来如需统一可提取共享 UserControl。
- **[数据查询性能]** → Waybill 关联查询（Include Material + Provider）可能影响分页查询速度。通过合理的索引和分页大小（10 条/页）缓解。
- **[WeighingMode 判断时机]** → 需确保在打开对话框时能正确获取当前模式。通过 `SystemSettings.WeighingMode` 获取，该值在启动时确定。
