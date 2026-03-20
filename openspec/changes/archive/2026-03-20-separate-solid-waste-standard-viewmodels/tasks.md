# 实现任务清单

## 1. 基础设施准备

- [x] 1.1 创建 `AttendedWeighingDetailViewModelBase.cs` 抽象基类文件
- [x] 1.2 从原 ViewModel 提取共享依赖注入字段（IServiceProvider、ILogger、IRepository 等）
- [x] 1.3 从原 ViewModel 提取共享属性（Weight、PlateNumber、Remark、DeliveryType 等）
- [x] 1.4 从原 ViewModel 提取共享命令（Close、Abolish、Match）
- [x] 1.5 从原 ViewModel 提取共享事件（SaveCompleted、CompleteCompleted 等）
- [x] 1.6 定义抽象方法 `SaveCoreAsync()` 和 `CompleteCoreAsync()`

## 2. 标准模式 ViewModel 实现

- [x] 2.1 创建 `StandardModeDetailViewModel.cs` 文件，继承基类
- [x] 2.2 实现标准模式专用属性（Providers、Materials、MaterialItems 等）
- [x] 2.3 实现 `MaterialsSelectionPopupViewModel` 初始化逻辑
- [x] 2.4 实现 `SaveCoreAsync()` 方法（调用 UpdateListItemAsync）
- [x] 2.5 实现 `CompleteCoreAsync()` 方法（验证并完成）
- [x] 2.6 实现材料选择相关命令（OpenMaterialSelection、SelectMaterial、AddMaterial）
- [x] 2.7 标记类为 `ITransientDependency`

## 3. 固废模式 ViewModel 实现

- [x] 3.1 创建 `SolidWasteModeDetailViewModel.cs` 文件，继承基类
- [x] 3.2 实现固废模式专用属性（SolidWasteOrderNumber、Streets、SolidWasteTypes 等）
- [x] 3.3 实现三个增强型选择弹窗初始化（Streets、Materials、Providers）
- [x] 3.4 实现 `SaveCoreAsync()` 方法（调用 UpdateSolidWasteModeAsync）
- [x] 3.5 实现 `CompleteCoreAsync()` 方法（验证并完成）
- [x] 3.6 实现固废模式特有的订阅逻辑（材料单位自动选择、运单数量同步）
- [x] 3.7 实现 `LoadSolidWasteDataAsync()` 方法
- [x] 3.8 标记类为 `ITransientDependency`

## 4. MaterialItemRow 提取

- [x] 4.1 将 `MaterialItemRow` 类移动到独立文件 `MaterialItemRow.cs`
- [x] 4.2 确保命名空间正确（`MaterialClient.ViewModels`）

## 5. View 层更新

- [x] 5.1 更新 `AttendedWeighingDetailView.axaml.cs` 的 DataContext 类型
- [x] 5.2 更新 `StandardModeFormView.axaml` 的 `x:DataType` 为 `StandardModeDetailViewModel`
- [x] 5.3 验证 `StandardModeFormView` 的所有绑定路径正确
- [x] 5.4 更新 `SolidWasteModeFormView.axaml` 的 `x:DataType` 为 `SolidWasteModeDetailViewModel`
- [x] 5.5 验证 `SolidWasteModeFormView` 的所有绑定路径正确

## 6. 父 ViewModel 更新

- [x] 6.1 更新 `AttendedWeighingViewModel.cs` 中的 ViewModel 创建逻辑
- [x] 6.2 实现 `CreateDetailViewModel` 方法，根据 `WeighingMode` 选择派生类
- [x] 6.3 确保事件订阅与基类定义一致

## 7. 清理工作

- [ ] 7.1 删除原 `AttendedWeighingDetailViewModel.cs` 文件
- [ ] 7.2 移除未使用的 using 语句
- [ ] 7.3 验证编译无错误

## 8. 功能验证

- [ ] 8.1 验证标准模式：打开详情窗口显示正确数据
- [ ] 8.2 验证标准模式：保存功能正常
- [ ] 8.3 验证标准模式：完成功能正常
- [ ] 8.4 验证标准模式：匹配功能正常
- [ ] 8.5 验证标准模式：作废功能正常
- [ ] 8.6 验证固废模式：打开详情窗口显示正确数据
- [ ] 8.7 验证固废模式：保存功能正常
- [ ] 8.8 验证固废模式：完成功能正常
- [ ] 8.9 验证固废模式：匹配功能正常
- [ ] 8.10 验证固废模式：作废功能正常
