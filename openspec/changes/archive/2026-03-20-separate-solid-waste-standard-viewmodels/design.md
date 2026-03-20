# 设计文档：分离固废模式和标准模式的 ViewModel

## Context

### 当前状态

`AttendedWeighingDetailViewModel.cs` 是一个约 1555 行的大型 ViewModel，同时处理两种称重模式：

```
当前架构
┌─────────────────────────────────────────────────────────────────────┐
│                  AttendedWeighingDetailViewModel (1555 行)           │
├─────────────────────────────────────────────────────────────────────┤
│  共享逻辑（约 40%）                                                   │
│  ├─ 重量计算：AllWeight、TruckWeight、GoodsWeight                    │
│  ├─ 车牌验证：PlateNumber、PlateNumberValidator                     │
│  ├─ 通用命令：Close、Abolish、Match                                   │
│  └─ 事件：SaveCompleted、CompleteCompleted 等                        │
├─────────────────────────────────────────────────────────────────────┤
│  标准模式逻辑（约 30%）                                               │
│  ├─ Providers 下拉                                                   │
│  ├─ Materials 选择 + MaterialItems DataGrid                          │
│  └─ MaterialsSelectionPopupViewModel                                 │
├─────────────────────────────────────────────────────────────────────┤
│  固废模式逻辑（约 30%）                                               │
│  ├─ SolidWasteOrderNumber、Streets、SolidWasteTypes                 │
│  ├─ 三个增强型选择弹窗（Streets/Materials/Providers）                 │
│  └─ 额外的字段：SelectedSolidWasteMaterial 等                        │
└─────────────────────────────────────────────────────────────────────┘
```

### 约束

1. **保持功能不变**：用户界面和业务逻辑行为保持一致
2. **最小化绑定变更**：属性名称保持不变，仅改变类型层次
3. **遵循现有模式**：使用 ReactiveUI + ReactiveUI.SourceGenerators
4. **DI 兼容**：新 ViewModel 需要正确注册到 DI 容器

### 利益相关者

- 开发团队：需要维护两套独立的业务逻辑
- 测试团队：需要对单一模式进行独立测试

## Goals / Non-Goals

**Goals:**

1. 将 `AttendedWeighingDetailViewModel` 拆分为基类 + 两个派生类
2. 提取共享逻辑到抽象基类，避免代码重复
3. 确保每个派生类职责单一，易于理解和修改
4. 保持现有的 View 绑定路径不变
5. 支持根据 `WeighingMode` 动态选择 ViewModel

**Non-Goals:**

1. 不改变用户界面布局或行为
2. 不修改 API 或数据库结构
3. 不添加新功能或修改现有功能逻辑
4. 不添加单元测试（超出本次变更范围）
5. 不更新文档（超出本次变更范围）

## Decisions

### 决策 1：使用继承而非组合

**选择**：使用抽象基类继承

**理由**：
- 共享逻辑（约 40%）可以直接继承，无需委托
- ReactiveUI 的属性和命令支持继承
- 符合项目中其他 ViewModel 的模式

**备选方案**：

| 方案 | 优点 | 缺点 |
|-----|-----|-----|
| **继承**（已选） | 代码复用简单、属性绑定无需修改 | 耦合度较高 |
| **组合** | 解耦更彻底、更灵活 | 需要委托大量属性、增加复杂度 |
| **策略模式** | 运行时可切换 | 过度设计、两种模式不会切换 |

### 决策 2：ViewModel 创建方式

**选择**：在 `AttendedWeighingViewModel` 中根据 `WeighingMode` 直接创建

**理由**：
- `WeighingListItemDto` 在创建时已包含 `WeighingMode`
- 避免引入额外的工厂类
- 保持与现有代码风格一致

**实现**：

```csharp
// AttendedWeighingViewModel.cs
private AttendedWeighingDetailViewModelBase CreateDetailViewModel(WeighingListItemDto listItem)
{
    return listItem.WeighingMode switch
    {
        WeighingMode.SolidWaste => _serviceProvider.GetRequiredService<SolidWasteModeDetailViewModel>(),
        _ => _serviceProvider.GetRequiredService<StandardModeDetailViewModel>()
    };
}
```

### 决策 3：基类设计

**选择**：抽象基类包含共享逻辑 + 抽象方法

**基类结构**：

```
AttendedWeighingDetailViewModelBase (抽象类)
├── 共享依赖注入
│   └── IServiceProvider, ILogger, IRepository<WeighingRecord> 等
├── 共享属性
│   ├── WeighingRecordId, AllWeight, TruckWeight, GoodsWeight
│   ├── PlateNumber, PlateNumberError
│   ├── Remark, JoinTime, OutTime, Operator
│   ├── DeliveryTypeOptions, SelectedDeliveryType, DeliveryTypeDisplayText
│   ├── IsWeighingRecord, ProviderLabelText, CompleteButtonText
│   ├── IsMatchButtonVisible, IsCompleteButtonVisible
│   └── WeighingMode, IsSolidWasteMode
├── 共享命令
│   ├── CloseCommand
│   ├── AbolishCommand
│   └── MatchCommand
├── 抽象方法
│   ├── SaveCoreAsync() : Task
│   └── CompleteCoreAsync() : Task
└── 共享事件
    ├── SaveCompleted
    ├── CompleteCompleted
    └── CloseRequested 等
```

### 决策 4：View 绑定更新

**选择**：使用接口 `IAttendedWeighingDetailViewModel` 作为绑定类型

**理由**：
- 允许 XAML 编译时类型检查
- 避免使用 `object` 或 `dynamic`
- 支持编译器验证绑定路径

**备选方案**：

| 方案 | 优点 | 缺点 |
|-----|-----|-----|
| **接口绑定**（已选） | 编译时检查、清晰 | 需要定义接口 |
| 基类绑定 | 简单 | 派生类特有属性无法访问 |
| 无类型绑定 | 最灵活 | 失去编译时检查 |

## Risks / Trade-offs

### 风险 1：绑定路径变更导致运行时错误

**风险**：派生类中的属性名称与基类不一致，导致绑定失败

**缓解措施**：
- 保持所有公共属性名称与原 ViewModel 一致
- 在 View 中使用 `x:DataType` 进行编译时检查
- 在实现后进行完整的 UI 功能测试

### 风险 2：事件订阅丢失

**风险**：父 ViewModel 订阅的事件在重构后无法触发

**缓解措施**：
- 所有事件定义保留在基类中
- 事件触发逻辑保持不变
- 确保 `EventHandler` 签名一致

### 风险 3：DI 注册顺序问题

**风险**：派生类依赖的服务未正确注册

**缓解措施**：
- 两个派生类都注册为 `ITransientDependency`
- 在 `App.axaml.cs` 或模块中确保注册顺序正确

### 权衡

| 权衡 | 选择 | 代价 |
|-----|-----|-----|
| 代码清晰度 | 分离为三个文件 | 文件数量增加 |
| 继承 vs 组合 | 继承 | 派生类与基类耦合 |
| 接口定义 | 添加绑定接口 | 额外的类型定义 |

## Migration Plan

### 实施步骤

```
步骤 1：创建基类
├── 创建 AttendedWeighingDetailViewModelBase.cs
├── 提取共享属性和方法
└── 定义抽象方法 SaveCoreAsync、CompleteCoreAsync

步骤 2：创建派生类
├── 创建 StandardModeDetailViewModel.cs
│   ├── 继承基类
│   ├── 实现标准模式专用属性
│   └── 实现 SaveCoreAsync、CompleteCoreAsync
│
└── 创建 SolidWasteModeDetailViewModel.cs
    ├── 继承基类
    ├── 实现固废模式专用属性
    └── 实现 SaveCoreAsync、CompleteCoreAsync

步骤 3：更新 View 层
├── 更新 AttendedWeighingDetailView.axaml.cs
│   └── DataContext 类型改为基类或接口
│
├── 更新 StandardModeFormView.axaml
│   └── x:DataType 改为 StandardModeDetailViewModel
│
└── 更新 SolidWasteModeFormView.axaml
    └── x:DataType 改为 SolidWasteModeDetailViewModel

步骤 4：更新父 ViewModel
├── 更新 AttendedWeighingViewModel.cs
│   └── 根据 WeighingMode 创建对应派生类
│
└── 删除原 AttendedWeighingDetailViewModel.cs

步骤 5：测试验证
├── 标准模式：保存、完成、匹配、作废
└── 固废模式：保存、完成、匹配、作废
```

### 回滚策略

如果重构后发现问题：
1. 恢复 `AttendedWeighingDetailViewModel.cs`（从 Git 历史）
2. 恢复 View 文件的 `x:DataType` 绑定
3. 删除新创建的基类和派生类文件

## Open Questions

1. **是否需要定义 `IAttendedWeighingDetailViewModel` 接口？**
   - 如果 View 绑定可以接受基类类型，则不需要接口
   - 建议在实现时评估是否需要

2. **MaterialItemRow 类应该放在哪里？**
   - 当前在同一个文件中
   - 建议移动到独立文件 `MaterialItemRow.cs`，便于复用

3. **是否需要为两个派生类创建共享的扩展方法？**
   - 如果发现重复代码，可以提取为扩展方法
   - 建议在实现后评估
