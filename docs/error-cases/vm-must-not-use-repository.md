# 错误样例：ViewModel 中禁止直接注入和使用 Repository

## 规则

ViewModel **禁止**直接注入 `IRepository<T>`，所有数据访问必须通过 Service 层完成。

**为什么**：ViewModel 直接持有 Repository 会导致：
- 数据访问逻辑（查询构建、关联查询、分页、过滤）泄漏到表现层
- 违反单一职责原则——ViewModel 不应同时负责 UI 状态管理和数据查询编排
- 难以复用和测试——相同的查询逻辑无法被其他 ViewModel 或 Service 复用
- 丢失 `[UnitOfWork]` 事务管理——Service 层统一管理工作单元边界

---

## 错误用例

**文件**：`MaterialClient/ViewModels/StandardDataManagementDialogViewModel.cs`

```csharp
public partial class StandardDataManagementDialogViewModel : ViewModelBase, ITransientDependency
{
    private readonly IRepository<Waybill, long> _waybillRepository;   // 违规
    private readonly IRepository<Material, int> _materialRepository;   // 违规
    private readonly IRepository<Provider, int> _providerRepository;   // 违规

    public StandardDataManagementDialogViewModel(
        IRepository<Waybill, long> waybillRepository,
        IRepository<Material, int> materialRepository,
        IRepository<Provider, int> providerRepository,
        ILogger<StandardDataManagementDialogViewModel>? logger = null)
        : base(logger)
    {
        _waybillRepository = waybillRepository;
        _materialRepository = materialRepository;
        _providerRepository = providerRepository;
        // ...
    }

    private async Task LoadDataAsync()
    {
        // ViewModel 中直接构建 EF Core 查询
        var queryable = await _waybillRepository.GetQueryableAsync();
        queryable = queryable.Where(w =>
            w.WeighingMode == WeighingMode.Standard && !w.IsDeleted);
        queryable = await ApplyFiltersAsync(queryable);  // 更多查询逻辑
        var totalCount = await queryable.CountAsync();
        // ...
        var providerDict = await BuildProviderDictAsync(pagedWaybills);
        var materialDict = await BuildMaterialDictAsync(pagedWaybills);
    }
}
```

**问题清单**：
1. 构造函数注入了 3 个 `IRepository`，ViewModel 承担了数据访问职责
2. `LoadDataAsync`、`ApplyFiltersAsync`、`BuildProviderDictAsync`、`BuildMaterialDictAsync` 全部在 ViewModel 中直接编写 EF Core 查询逻辑
3. 没有 `[UnitOfWork]` 保护，数据库操作不在工作单元管理之下
4. 查询逻辑无法被其他 ViewModel 复用（Standard 模式的导出、统计等功能会重复编写）

---

## 正确用例

**文件**：`MaterialClient.Common/Services/SolidWasteService.cs`

```csharp
public interface ISolidWasteService
{
    Task<IReadOnlyList<SolidWasteExportRow>> GetExportRowsAsync(SolidWasteExportFilter filter);
    Task<PagedResultDto<SolidWasteExportRow>> GetPagedExportRowsAsync(
        SolidWasteExportFilter filter, int pageIndex, int pageSize);
}

[AutoConstructor]
public partial class SolidWasteService : ISolidWasteService, ITransientDependency
{
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<Material, int> _materialRepository;

    [UnitOfWork]
    public virtual async Task<PagedResultDto<SolidWasteExportRow>> GetPagedExportRowsAsync(
        SolidWasteExportFilter filter, int pageIndex, int pageSize)
    {
        var waybills = await SolidWasteQueryWaybillsAsync(filter);
        var totalCount = waybills.Count;
        var page = waybills.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        var providerDict = await SolidWasteBuildProviderDictAsync(page);
        var materialDict = await SolidWasteBuildMaterialDictAsync(page);
        var items = page
            .Select(w => SolidWasteMapToExportRow(w, providerDict, materialDict))
            .ToList();
        return new PagedResultDto<SolidWasteExportRow>(totalCount, items);
    }
}
```

**正确之处**：
1. 数据访问逻辑封装在 `SolidWasteService` 中，ViewModel 只调用 `GetPagedExportRowsAsync`
2. 使用 `[UnitOfWork]` 确保数据库操作在工作单元内执行
3. 过滤条件通过 `SolidWasteExportFilter` DTO 传递，ViewModel 不接触查询构建
4. Service 可被任意 ViewModel 或其他 Service 复用
5. 查询、映射、分页逻辑集中管理，修改只需改一处

---

## 修复方向

为 Standard 模式创建 `IStandardDataManagementService`，将 `LoadDataAsync` 中的全部查询、过滤、关联字典构建、映射逻辑迁移到 Service 层。ViewModel 只保留 UI 状态管理（筛选条件绑定、分页状态、Records 集合）。
