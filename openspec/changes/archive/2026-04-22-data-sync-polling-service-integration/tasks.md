## 1. ViewModel 服务注入

- [x] 1.1 在 `AttendedWeighingViewModel` 构造函数中添加 `ISyncMaterialService`、`IWeighingMatchingService`、`IAttachmentService` 参数注入，并声明对应的 `private readonly` 字段

## 2. 同步命令实现

- [x] 2.1 在 `AttendedWeighingViewModel` 中添加 `IsSyncing` 布尔属性（`private set`），初始值为 `false`
- [x] 2.2 添加 `[ReactiveCommand] async Task SyncDataAsync()` 方法，实现以下逻辑：
  - 设置 `IsSyncing = true`
  - 创建 `List<string>` 收集失败步骤名称
  - 在 `try/finally` 中确保 `IsSyncing = false`
- [x] 2.3 在 `SyncDataAsync` 中依次调用 5 个同步步骤，每个步骤包裹在独立 `try/catch` 中，失败时将步骤名称添加到失败列表：
  - `ISyncMaterialService.SyncMaterialAsync()` → 失败记录 "物料同步"
  - `ISyncMaterialService.SyncMaterialTypeAsync()` → 失败记录 "物料类型同步"
  - `ISyncMaterialService.SyncProviderAsync()` → 失败记录 "供应商同步"
  - `IWeighingMatchingService.PushWaybillAsync(CancellationToken.None)` → 失败记录 "运单推送"
  - `IAttachmentService.SyncPendingAttachmentsToOssAsync()` → 失败记录 "附件上传"
- [x] 2.4 在所有步骤执行完毕后，根据失败列表构建结果摘要字符串并调用 `ShowMessageBoxAsync`：
  - 无失败：`"数据同步完成：5 项成功"`
  - 有失败：`"数据同步完成：{5-failCount} 项成功，{failCount} 项失败"`

## 3. XAML 按钮绑定

- [x] 3.1 在 `AttendedWeighingWindow.axaml` 的"数据同步"按钮上添加 `Command="{Binding SyncDataCommand}"` 属性
- [x] 3.2 验证 `IsEnabled` 默认行为：`[ReactiveCommand]` 在异步方法执行期间自动禁用关联控件，确认无需额外绑定 `IsEnabled`
