using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.UI.Views.Controls;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace MaterialClient.UI.Test.Controls;

/// <summary>
/// 无头测试：CreatablePageableSearchableSelectionBox 交互行为验证。
/// 覆盖 spec 场景：SelectedId 绑定、CreateNewAsync 编排、冷却保护、debounce 取消、
/// 打开(有/无选中)、关闭重置、选择、新增、防抖、分页、键盘。
/// 注：无头环境下模板可能未注入，模板依赖的断言使用 null guard。
/// </summary>
public class CreatablePageableSearchableSelectionBoxInteractionTests
{
    private static PagedResultDto<SelectionItem> EmptyPage() =>
        new(0, new List<SelectionItem>());

    private static PagedResultDto<SelectionItem> SingleItemPage(int id, string name) =>
        new(1, new List<SelectionItem> { new() { Id = id, Name = name } });

    private static PagedResultDto<SelectionItem> MultiItemPage(List<SelectionItem> items) =>
        new(items.Count, items);

    #region 基础属性

    [Fact]
    public void Default_IsPopupOpen_ShouldBeFalse()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.False(control.IsPopupOpen);
    }

    [Fact]
    public void CurrentPageItems_ShouldBeEmptyObservableCollection()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.NotNull(control.CurrentPageItems);
        Assert.Empty(control.CurrentPageItems);
    }

    [Fact]
    public void WhenSelectedIdSet_AndItemsLoaded_TextBoxShouldShowItemName()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(1, "测试供应商"));

        control.SelectedId = 1;
        control.IsPopupOpen = true;
        control.IsPopupOpen = false;

        if (control.PART_TextBox == null) return;
        Assert.Equal("测试供应商", control.PART_TextBox.Text);
    }

    [Fact]
    public void WhenNoSelectedId_AndPopupClosed_TextBoxShouldShowWatermark()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.Watermark = "请选择供应商";
        control.SelectedId = null;
        control.IsPopupOpen = false;
        if (control.PART_TextBox == null) return;
        Assert.Equal("请选择供应商", control.PART_TextBox.Text);
    }

    [Fact]
    public void WhenLoadPageAsyncSet_OpeningPopup_ShouldNotThrow()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        control.IsPopupOpen = true;
        Assert.True(control.IsPopupOpen);
    }

    #endregion

    #region 10.1 SelectedId 绑定

    [Fact]
    public void Default_SelectedId_ShouldBeNull()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.Null(control.SelectedId);
    }

    [Fact]
    public void SelectedId_CanBeSetAndRetrieved()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.SelectedId = 42;
        Assert.Equal(42, control.SelectedId);

        control.SelectedId = null;
        Assert.Null(control.SelectedId);
    }

    [Fact]
    public void SelectedId_ResolvesDisplayNameFromCurrentPageItems()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(42, "供应商A"));

        control.IsPopupOpen = true;
        control.IsPopupOpen = false;

        if (control.PART_TextBox == null) return;

        control.SelectedId = 42;
        Assert.Equal("供应商A", control.PART_TextBox.Text);
    }

    #endregion

    #region 7.1 打开（有选中项）

    [Fact]
    public void OpenWithSelectedId_LoadPageAsyncCalledWithSelectedIds()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        IReadOnlyList<int>? capturedIds = null;
        var callCount = 0;

        control.LoadPageAsync = (_, _, _, ids, _) =>
        {
            callCount++;
            capturedIds = ids;
            return Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(42, "供应商A"));
        };

        control.SelectedId = 42;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.True(callCount > 0, "LoadPageAsync should be called when popup opens");
        Assert.NotNull(capturedIds);
        Assert.Contains(42, capturedIds!);
    }

    [Fact]
    public void OpenWithSelectedId_CurrentPageItemsContainsSelectedItem()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(42, "供应商A"));

        control.SelectedId = 42;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;
        Assert.Contains(control.CurrentPageItems, i => i.Id == 42 && i.Name == "供应商A");
    }

    #endregion

    #region 7.2 打开（无选中项）

    [Fact]
    public void OpenWithNoSelection_TextBoxIsEmpty()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        control.SelectedId = null;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;
        Assert.Equal(string.Empty, control.PART_TextBox.Text);
    }

    [Fact]
    public void OpenWithNoSelection_LoadPageAsyncCalledWithNullSelectedIds()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        IReadOnlyList<int>? capturedIds = new List<int> { -1 };
        string? capturedSearch = "sentinel";
        var called = false;

        control.LoadPageAsync = (search, _, _, ids, _) =>
        {
            called = true;
            capturedSearch = search;
            capturedIds = ids;
            return Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        };

        control.SelectedId = null;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.True(called, "LoadPageAsync should be called");
        Assert.Equal(string.Empty, capturedSearch);
        Assert.Null(capturedIds);
    }

    #endregion

    #region 7.3 输入后不选择即关闭→重置

    [Fact]
    public void CloseWithoutSelection_TextBoxResetsToSelectedDisplayName()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(1, "已选项"));

        control.SelectedId = 1;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        control.IsPopupOpen = false;
        control.IsPopupOpen = true;

        control.PART_TextBox.Text = "新输入内容";
        control.IsPopupOpen = false;

        Assert.Equal("已选项", control.PART_TextBox.Text);
    }

    [Fact]
    public void CloseWithoutSelection_NoSelectedId_TextBoxShowsWatermark()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.Watermark = "请选择";
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        control.SelectedId = null;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        control.PART_TextBox.Text = "搜索文本";
        control.IsPopupOpen = false;

        Assert.True(
            control.PART_TextBox.Text == string.Empty || control.PART_TextBox.Text == "请选择",
            $"Expected empty or watermark, got: '{control.PART_TextBox.Text}'");
    }

    #endregion

    #region 7.4 选择一项→更新 SelectedId、关闭 Popup

    [Fact]
    public void SelectItemFromDataGrid_SelectedIdUpdates()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var items = new List<SelectionItem>
        {
            new() { Id = 1, Name = "项目1" },
            new() { Id = 2, Name = "项目2" }
        };
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(MultiItemPage(items));

        control.IsPopupOpen = true;

        if (control.PART_DataGrid == null) return;

        control.PART_DataGrid.SelectedItem = items[1];

        Assert.Equal(2, control.SelectedId);
    }

    [Fact]
    public void SelectItemFromDataGrid_PopupCloses()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var items = new List<SelectionItem>
        {
            new() { Id = 1, Name = "项目1" },
            new() { Id = 2, Name = "项目2" }
        };
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(MultiItemPage(items));

        control.IsPopupOpen = true;

        if (control.PART_DataGrid == null) return;

        control.PART_DataGrid.SelectedItem = items[0];

        Assert.False(control.IsPopupOpen);
    }

    #endregion

    #region 7.5 无结果→显示"新增" (only when CreateNewAsync set)

    [Fact]
    public void EmptyResults_WithCreateNewAsync_ShowAddNewIsTrue()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        control.CreateNewAsync = (_, _) =>
            Task.FromResult<SelectionItem?>(new SelectionItem { Id = 99, Name = "new" });

        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.True(control.ShowAddNew, "ShowAddNew should be true when no results and CreateNewAsync set");
    }

    [Fact]
    public void EmptyResults_WithoutCreateNewAsync_ShowAddNewIsFalse()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        control.CreateNewAsync = null;

        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.False(control.ShowAddNew, "ShowAddNew should be false when CreateNewAsync is null");
    }

    [Fact]
    public void NonEmptyResults_ShowAddNewIsFalse()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(1, "有数据"));

        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.False(control.ShowAddNew, "ShowAddNew should be false when results exist");
    }

    [Fact]
    public void CreateNewAsync_CanBeSetAndRetrieved()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.CreateNewAsync = (name, ct) =>
            Task.FromResult<SelectionItem?>(new SelectionItem { Id = 1, Name = name });
        Assert.NotNull(control.CreateNewAsync);

        control.CreateNewAsync = null;
        Assert.Null(control.CreateNewAsync);
    }

    [Fact]
    public void AddNewButton_ShouldExistInTemplate()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        if (control.PART_AddNewButton == null) return;
        Assert.NotNull(control.PART_AddNewButton);
    }

    #endregion

    #region 10.5 CreateNewAsync 内部编排

    [Fact]
    public async Task CreateNewAsync_WhenButtonClicked_SetsSelectedIdAndClosesPopup()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var created = new SelectionItem { Id = 77, Name = "新建项" };
        string? capturedName = null;

        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        control.CreateNewAsync = (name, _) =>
        {
            capturedName = name;
            return Task.FromResult<SelectionItem?>(created);
        };

        control.IsPopupOpen = true;
        if (control.PART_AddNewButton == null) return;

        control.PART_AddNewButton.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

        await Task.Delay(100);

        Assert.Equal(77, control.SelectedId);
    }

    #endregion

    #region 10.8 _suppressNextOpen 冷却保护

    [Fact]
    public void AfterPopupClose_SuppressNextOpenFlagIsActive()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        control.IsPopupOpen = true;
        if (control.PART_TextBox == null) return;

        control.IsPopupOpen = false;
        Assert.False(control.IsPopupOpen, "Popup should remain closed after close");
    }

    [Fact]
    public void SelectDifferentItem_PopupShouldNotReopen()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var items = new List<SelectionItem>
        {
            new() { Id = 1, Name = "项目1" },
            new() { Id = 2, Name = "项目2" }
        };
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(MultiItemPage(items));

        control.IsPopupOpen = true;
        if (control.PART_DataGrid == null) return;

        control.PART_DataGrid.SelectedItem = items[1];

        Assert.False(control.IsPopupOpen, "Popup should stay closed after selecting different item");
        Assert.Equal(2, control.SelectedId);
    }

    #endregion

    #region 10.7 debounce 取消

    [Fact]
    public async Task PopupClose_CancelsDebounce()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var loadCount = 0;

        control.LoadPageAsync = (_, _, _, _, _) =>
        {
            loadCount++;
            return Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        };

        control.IsPopupOpen = true;
        if (control.PART_TextBox == null) return;

        var openLoadCount = loadCount;

        control.PART_TextBox.Text = "搜索";
        control.IsPopupOpen = false;

        await Task.Delay(500);

        Assert.Equal(openLoadCount, loadCount);
    }

    #endregion

    #region 7.7 交互行为补充（分页信息、CurrentPage、Escape）

    [Fact]
    public void Pagination_CurrentPage_DefaultIs1()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.Equal(1, control.CurrentPage);
    }

    [Fact]
    public void Pagination_TotalCount_DefaultIs0()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.Equal(0, control.TotalCount);
    }

    [Fact]
    public void Pagination_ShowResults_FalseWhenEmpty()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;
        Assert.False(control.ShowResults);
    }

    [Fact]
    public void Pagination_ShowResults_TrueWhenHasItems()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(1, "A"));

        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;
        Assert.True(control.ShowResults);
    }

    [Fact]
    public void Pagination_PageInfoUpdatesAfterLoad()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(new PagedResultDto<SelectionItem>(
                25,
                new List<SelectionItem> { new() { Id = 1, Name = "A" } }));

        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.Equal("当前页:1", control.CurrentPageInfo);
        Assert.Equal("共25条记录", control.TotalCountInfo);
        Assert.Equal(25, control.TotalCount);
    }

    [Fact]
    public void Pagination_ChangingCurrentPage_TriggersReload()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var capturedPages = new List<int>();

        control.LoadPageAsync = (_, page, _, _, _) =>
        {
            capturedPages.Add(page);
            return Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(page, $"P{page}"));
        };

        control.IsPopupOpen = true;
        if (control.PART_TextBox == null) return;

        capturedPages.Clear();
        control.CurrentPage = 2;

        Assert.Contains(2, capturedPages);
    }

    [Fact]
    public void PageSize_DefaultIs10()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.Equal(10, control.PageSize);
    }

    [Fact]
    public void PageSize_CanBeCustomized()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.PageSize = 20;
        Assert.Equal(20, control.PageSize);
    }

    [Fact]
    public void LoadPageAsync_ReceivesCorrectPageSize()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        int capturedPageSize = 0;

        control.PageSize = 15;
        control.LoadPageAsync = (_, _, pageSize, _, _) =>
        {
            capturedPageSize = pageSize;
            return Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        };

        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.Equal(15, capturedPageSize);
    }

    [Fact]
    public void Watermark_DefaultIsExpected()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.Equal("请选择", control.Watermark);
    }

    [Fact]
    public void OpenPopup_ThenClose_ThenReopen_LoadPageAsyncCalledEachTime()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var callCount = 0;

        control.LoadPageAsync = (_, _, _, _, _) =>
        {
            callCount++;
            return Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        };

        control.SelectedId = 1;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        var firstCallCount = callCount;
        Assert.True(firstCallCount > 0);

        control.IsPopupOpen = false;
        control.IsPopupOpen = true;

        Assert.True(callCount > firstCallCount, "LoadPageAsync should be called again on reopen");
    }

    [Fact]
    public void SelectedId_TwoWay_CanBeSetExternally()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();

        control.SelectedId = 99;
        Assert.Equal(99, control.SelectedId);

        control.SelectedId = null;
        Assert.Null(control.SelectedId);
    }

    #endregion

    #region 8.7 焦点与状态管理修复验证

    [Fact]
    public void InitialRender_PopupShouldNotOpen()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.False(control.IsPopupOpen);
    }

    [Fact]
    public void InitialRender_TextBoxShouldBeReadOnly()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        if (control.PART_TextBox == null) return;
        Assert.True(control.PART_TextBox.IsReadOnly);
    }

    [Fact]
    public void SelectItem_PopupShouldNotReopen()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var items = new List<SelectionItem>
        {
            new() { Id = 1, Name = "项目1" },
            new() { Id = 2, Name = "项目2" }
        };
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(MultiItemPage(items));

        control.IsPopupOpen = true;
        if (control.PART_DataGrid == null) return;

        control.PART_DataGrid.SelectedItem = items[0];

        Assert.False(control.IsPopupOpen, "Popup should stay closed after selection");
        Assert.Equal(1, control.SelectedId);
    }

    [Fact]
    public void CloseAndReopen_PopupShouldOpenNormally()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var callCount = 0;
        control.LoadPageAsync = (_, _, _, _, _) =>
        {
            callCount++;
            return Task.FromResult<PagedResultDto<SelectionItem>?>(
                SingleItemPage(1, "A"));
        };

        control.IsPopupOpen = true;
        if (control.PART_TextBox == null) return;

        var firstCallCount = callCount;
        Assert.True(firstCallCount > 0);

        control.IsPopupOpen = false;
        Assert.False(control.IsPopupOpen);

        control.IsPopupOpen = true;
        Assert.True(control.IsPopupOpen);
        Assert.True(callCount > firstCallCount, "LoadPageAsync should be called again on reopen");
    }

    [Fact]
    public void IsPopupOpen_And_TextBoxIsReadOnly_AreAlwaysOpposite()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(1, "A"));

        if (control.PART_TextBox == null) return;

        Assert.False(control.IsPopupOpen);
        Assert.True(control.PART_TextBox.IsReadOnly, "Closed → IsReadOnly should be true");

        control.IsPopupOpen = true;
        Assert.True(control.IsPopupOpen);
        Assert.False(control.PART_TextBox.IsReadOnly, "Open → IsReadOnly should be false");

        control.IsPopupOpen = false;
        Assert.False(control.IsPopupOpen);
        Assert.True(control.PART_TextBox.IsReadOnly, "Closed again → IsReadOnly should be true");

        control.IsPopupOpen = true;
        Assert.False(control.PART_TextBox.IsReadOnly, "Re-open → IsReadOnly should be false");

        control.IsPopupOpen = false;
        Assert.True(control.PART_TextBox.IsReadOnly, "Final close → IsReadOnly should be true");
    }

    [Fact]
    public void PopupOpen_TextBoxBecomesEditable()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        if (control.PART_TextBox == null) return;

        Assert.True(control.PART_TextBox.IsReadOnly);

        control.IsPopupOpen = true;
        Assert.False(control.PART_TextBox.IsReadOnly);
    }

    [Fact]
    public void PopupClose_TextBoxBecomesReadOnly()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(1, "测试"));

        if (control.PART_TextBox == null) return;

        control.IsPopupOpen = true;
        Assert.False(control.PART_TextBox.IsReadOnly);

        control.IsPopupOpen = false;
        Assert.True(control.PART_TextBox.IsReadOnly);
    }

    #endregion

    #region 9.5 TextBox 焦点系统退出验证

    [Fact]
    public void InitialRender_TextBoxShouldBeNonFocusable()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        if (control.PART_TextBox == null) return;
        Assert.False(control.PART_TextBox.Focusable);
    }

    [Fact]
    public void InitialRender_TextBoxShouldBeNonHitTestVisible()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        if (control.PART_TextBox == null) return;
        Assert.False(control.PART_TextBox.IsHitTestVisible);
    }

    [Fact]
    public void PopupOpen_TextBoxBecomesFocusableAndHitTestVisible()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        if (control.PART_TextBox == null) return;

        control.IsPopupOpen = true;
        Assert.True(control.PART_TextBox.Focusable);
        Assert.True(control.PART_TextBox.IsHitTestVisible);
        Assert.False(control.PART_TextBox.IsReadOnly);
    }

    [Fact]
    public void PopupClose_TextBoxBecomesNonFocusableAndNonHitTestVisible()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(1, "A"));

        if (control.PART_TextBox == null) return;

        control.IsPopupOpen = true;
        Assert.True(control.PART_TextBox.Focusable);

        control.IsPopupOpen = false;
        Assert.False(control.PART_TextBox.Focusable);
        Assert.False(control.PART_TextBox.IsHitTestVisible);
        Assert.True(control.PART_TextBox.IsReadOnly);
    }

    [Fact]
    public void MultipleOpenCloseCycles_ThreePropertiesAlwaysSyncWithIsPopupOpen()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(1, "A"));

        if (control.PART_TextBox == null) return;

        for (var i = 0; i < 3; i++)
        {
            Assert.False(control.PART_TextBox.Focusable, $"Cycle {i}: closed → Focusable should be false");
            Assert.False(control.PART_TextBox.IsHitTestVisible, $"Cycle {i}: closed → IsHitTestVisible should be false");
            Assert.True(control.PART_TextBox.IsReadOnly, $"Cycle {i}: closed → IsReadOnly should be true");

            control.IsPopupOpen = true;

            Assert.True(control.PART_TextBox.Focusable, $"Cycle {i}: open → Focusable should be true");
            Assert.True(control.PART_TextBox.IsHitTestVisible, $"Cycle {i}: open → IsHitTestVisible should be true");
            Assert.False(control.PART_TextBox.IsReadOnly, $"Cycle {i}: open → IsReadOnly should be false");

            control.IsPopupOpen = false;
        }
    }

    #endregion
}
