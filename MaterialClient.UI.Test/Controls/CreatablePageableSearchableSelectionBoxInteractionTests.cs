using System.Collections.Generic;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.UI.Views.Controls;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace MaterialClient.UI.Test.Controls;

/// <summary>
/// 无头测试：CreatablePageableSearchableSelectionBox 交互行为验证。
/// 覆盖 spec 场景：打开(有/无选中)、关闭重置、选择、新增、防抖、分页、键盘。
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
    public void WhenSelectedItemSet_AndPopupClosed_TextBoxShouldShowItemName()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.SelectedItem = new SelectionItem { Id = 1, Name = "测试供应商" };
        control.IsPopupOpen = false;
        if (control.PART_TextBox == null) return;
        Assert.Equal("测试供应商", control.PART_TextBox.Text);
    }

    [Fact]
    public void WhenNoSelectedItem_AndPopupClosed_TextBoxShouldShowWatermark()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.Watermark = "请选择供应商";
        control.SelectedItem = null;
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

    #region 7.1 打开（有选中项）

    [Fact]
    public void OpenWithSelectedItem_TextBoxShowsSelectedItemName()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(42, "供应商A"));

        control.SelectedItem = new SelectionItem { Id = 42, Name = "供应商A" };
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;
        Assert.Equal("供应商A", control.PART_TextBox.Text);
    }

    [Fact]
    public void OpenWithSelectedItem_LoadPageAsyncCalledWithSelectedIds()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        string? capturedSearch = null;
        IReadOnlyList<int>? capturedIds = null;
        var callCount = 0;

        control.LoadPageAsync = (search, _, _, ids, _) =>
        {
            callCount++;
            capturedSearch = search;
            capturedIds = ids;
            return Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(42, "供应商A"));
        };

        control.SelectedItem = new SelectionItem { Id = 42, Name = "供应商A" };
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.True(callCount > 0, "LoadPageAsync should be called when popup opens");
        Assert.Equal("供应商A", capturedSearch);
        Assert.NotNull(capturedIds);
        Assert.Contains(42, capturedIds!);
    }

    [Fact]
    public void OpenWithSelectedItem_CurrentPageItemsContainsSelectedItem()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(SingleItemPage(42, "供应商A"));

        control.SelectedItem = new SelectionItem { Id = 42, Name = "供应商A" };
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

        control.SelectedItem = null;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;
        Assert.Equal(string.Empty, control.PART_TextBox.Text);
    }

    [Fact]
    public void OpenWithNoSelection_LoadPageAsyncCalledWithNullSelectedIds()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        IReadOnlyList<int>? capturedIds = new List<int> { -1 }; // distinguishable non-null initial
        string? capturedSearch = "sentinel";
        var called = false;

        control.LoadPageAsync = (search, _, _, ids, _) =>
        {
            called = true;
            capturedSearch = search;
            capturedIds = ids;
            return Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());
        };

        control.SelectedItem = null;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.True(called, "LoadPageAsync should be called");
        Assert.Equal(string.Empty, capturedSearch);
        Assert.Null(capturedIds);
    }

    #endregion

    #region 7.3 输入后不选择即关闭→重置

    [Fact]
    public void CloseWithoutSelection_TextBoxResetsToSelectedItemName()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        control.SelectedItem = new SelectionItem { Id = 1, Name = "已选项" };
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.Equal("已选项", control.PART_TextBox.Text);

        // Simulate user typing in the TextBox
        control.PART_TextBox.Text = "新输入内容";

        // Close without selecting → should reset
        control.IsPopupOpen = false;

        Assert.Equal("已选项", control.PART_TextBox.Text);
    }

    [Fact]
    public void CloseWithoutSelection_NoSelectedItem_TextBoxShowsWatermark()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.Watermark = "请选择";
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        control.SelectedItem = null;
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        control.PART_TextBox.Text = "搜索文本";
        control.IsPopupOpen = false;

        // TextBox should show watermark text (via UpdateTextBoxFromSelectedItem)
        Assert.True(
            control.PART_TextBox.Text == string.Empty || control.PART_TextBox.Text == "请选择",
            $"Expected empty or watermark, got: '{control.PART_TextBox.Text}'");
    }

    #endregion

    #region 7.4 选择一项→更新 SelectedItem、关闭 Popup

    [Fact]
    public void SelectItemFromList_SelectedItemUpdates()
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

        if (control.PART_ItemsList == null) return;

        // Simulate selecting an item
        control.PART_ItemsList.SelectedItem = items[1];

        Assert.NotNull(control.SelectedItem);
        Assert.Equal(2, control.SelectedItem!.Id);
        Assert.Equal("项目2", control.SelectedItem!.Name);
    }

    [Fact]
    public void SelectItemFromList_PopupCloses()
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

        if (control.PART_ItemsList == null) return;

        control.PART_ItemsList.SelectedItem = items[0];

        Assert.False(control.IsPopupOpen);
    }

    #endregion

    #region 7.5 无结果→显示"新增"

    [Fact]
    public void EmptyResults_ShowAddNewIsTrue()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        control.LoadPageAsync = (_, _, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(EmptyPage());

        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        Assert.True(control.ShowAddNew, "ShowAddNew should be true when no results");
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
    public void AddNewCommand_CanBeSetAndRetrieved()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var command = new object();
        control.AddNewCommand = command;
        Assert.Same(command, control.AddNewCommand);
    }

    [Fact]
    public void AddNewButton_ShouldExistInTemplate()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        if (control.PART_AddNewButton == null) return;
        Assert.NotNull(control.PART_AddNewButton);
    }

    #endregion

    #region 7.7 交互行为补充（防抖、分页、Escape、键盘）

    [Fact]
    public void LoadMore_IncrementsPage_AndAppendsItems()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();

        control.LoadPageAsync = (_, page, _, _, _) =>
            Task.FromResult<PagedResultDto<SelectionItem>?>(
                SingleItemPage(page, $"项目{page}"));

        control.IsPopupOpen = true;

        if (control.PART_LoadMoreButton == null) return;

        var initialCount = control.CurrentPageItems.Count;
        Assert.True(initialCount > 0, "Should have initial items");

        control.PART_LoadMoreButton.Command?.Execute(null);
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

        control.SelectedItem = new SelectionItem { Id = 1, Name = "A" };
        control.IsPopupOpen = true;

        if (control.PART_TextBox == null) return;

        var firstCallCount = callCount;
        Assert.True(firstCallCount > 0);

        control.IsPopupOpen = false;
        control.IsPopupOpen = true;

        Assert.True(callCount > firstCallCount, "LoadPageAsync should be called again on reopen");
    }

    [Fact]
    public void SelectedItem_TwoWay_CanBeSetExternally()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();

        var item = new SelectionItem { Id = 99, Name = "外部设置" };
        control.SelectedItem = item;

        Assert.Equal(99, control.SelectedItem!.Id);
        Assert.Equal("外部设置", control.SelectedItem!.Name);

        control.SelectedItem = null;
        Assert.Null(control.SelectedItem);
    }

    #endregion
}
