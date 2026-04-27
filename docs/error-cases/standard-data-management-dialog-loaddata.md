# Error Case: StandardDataManagementDialogViewModel.LoadDataAsync

> Source: `MaterialClient/ViewModels/StandardDataManagementDialogViewModel.cs`

## Error 1: Constructor fire-and-forget triggers async before initialization completes

**Location**: Line 55

```csharp
public StandardDataManagementDialogViewModel(...)
    : base(logger)
{
    _waybillRepository = waybillRepository;   // Line 51
    _materialRepository = materialRepository; // Line 52
    _providerRepository = providerRepository; // Line 53
    Records = new ObservableCollection<StandardExportRow>(); // Line 54
    CurrentPage = 1;  // Line 55 <-- triggers _ = LoadDataAsync() fire-and-forget
    TotalPages = 1;   // Line 56
    SelectedDeliveryType = DeliveryTypeFilterOptions[0]; // Line 57
    SelectedOrderType = OrderTypeFilterOptions[0];       // Line 58
    LoadDataCommand = ReactiveCommand.CreateFromTask(LoadDataAsync); // Line 60
}
```

**Problem**: Setting `CurrentPage = 1` in the constructor triggers the property setter (lines 80-92), which calls `_ = LoadDataAsync()` as fire-and-forget. At this point `LoadDataCommand` (line 60) has not been assigned yet, and the object is not fully constructed. While the fields used by `LoadDataAsync` are already assigned, this pattern is fragile and violates the principle that constructors should not start asynchronous workflows.

**Fix**: Remove the `CurrentPage = 1` trigger from the constructor. Initialize `_currentPage = 1` via the backing field directly, or defer the first load to a dedicated initialization method.

---

## Error 2: Recursive fire-and-forget via property setter

**Location**: Lines 80-92 + Lines 127-130

```csharp
public int CurrentPage
{
    get => _currentPage;
    set
    {
        if (_currentPage != value)
        {
            _currentPage = value;
            this.RaisePropertyChanged();
            _ = LoadDataAsync();  // Line 89 <-- fire-and-forget
        }
    }
}
```

```csharp
// Inside LoadDataAsync (lines 127-130):
if (CurrentPage > TotalPages && TotalPages > 0)
    CurrentPage = TotalPages;  // <-- triggers setter -> LoadDataAsync() again
if (CurrentPage < 1)
    CurrentPage = 1;           // <-- triggers setter -> LoadDataAsync() again
```

**Problem**: The `CurrentPage` setter unconditionally calls `_ = LoadDataAsync()`. When `LoadDataAsync` itself corrects `CurrentPage` (lines 127-130), it triggers the setter again, causing a recursive fire-and-forget call chain. Although bounded by the page correction logic (eventually `CurrentPage` stabilizes), this creates:

1. Unnecessary duplicate database queries
2. Race conditions between concurrent `LoadDataAsync` executions
3. Unpredictable ordering of results (the last fire-and-forget to complete wins)

**Fix**: Either use a flag to suppress re-entrancy during correction, or perform page correction before setting `CurrentPage`, or separate the "page changed" notification from the "load data" action.

---

## Error 3: Bypassing ReactiveCommand concurrency control

**Location**: Line 89 + Line 60

```csharp
// Line 60: ReactiveCommand has built-in concurrency protection
LoadDataCommand = ReactiveCommand.CreateFromTask(LoadDataAsync);

// Line 89: Direct call bypasses all ReactiveCommand protection
_ = LoadDataAsync();
```

**Problem**: `LoadDataAsync` is registered as a `ReactiveCommand`, which provides `CanExecute` tracking and concurrent execution prevention. However, the `CurrentPage` setter calls `LoadDataAsync()` directly, bypassing all of this. Multiple concurrent executions can overlap, corrupting `Records` (one clears it while another is populating it).

**Fix**: Replace `_ = LoadDataAsync()` with `_ = LoadDataCommand.Execute()` to respect ReactiveCommand's execution semantics.

---

## Error 4: Fire-and-forget exception swallowing

**Location**: Line 89

```csharp
_ = LoadDataAsync();
```

**Problem**: The discard (`_`) pattern means any exception thrown by `LoadDataAsync` in this code path is silently lost. Even though `LoadDataAsync` has a try-catch internally, exceptions in the `async void`-equivalent fire-and-forget pattern can still cause unobserved task exceptions or application instability.

**Fix**: Either await the call (requires making the setter async-aware) or use proper error handling for the discarded task.

---

## Error 5: Silent exception swallowing with test data fallback

**Location**: Lines 147-156

```csharp
catch (Exception ex)
{
    Logger?.LogError(ex, "加载标准台账分页数据失败，回退到测试数据。");
    Records.Clear();
    Records.Add(CreateTestRow());
    TotalCount = 1;
    TotalPages = 1;
    CurrentPage = 1;  // <-- triggers setter -> another LoadDataAsync() call
}
```

**Problem**:
1. Every exception is silently caught and replaced with hardcoded test data, masking real issues (database connection failures, schema mismatches, etc.) in production.
2. `CurrentPage = 1` in the catch block triggers the setter, which fires another `LoadDataAsync()`, potentially causing another failure and another catch -> infinite loop risk (mitigated by `CurrentPage` already being 1 on the second call, so the setter guard `if (_currentPage != value)` prevents it).

**Fix**: Remove the test data fallback. Notify the user of the error (e.g., via a toast or error message in the UI). Only use test data in development/debug builds.
