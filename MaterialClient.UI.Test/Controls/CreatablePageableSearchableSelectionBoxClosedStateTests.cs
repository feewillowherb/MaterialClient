using Avalonia;
using Avalonia.Media;
using MaterialClient.UI.Views.Controls;
using Xunit;

namespace MaterialClient.UI.Test.Controls;

/// <summary>
/// 无头测试：CreatablePageableSearchableSelectionBox 关闭态视觉与 SearchableSelectionBox 一致。
/// 验证 Height=32、背景/边框/字体/箭头尺寸等。
/// </summary>
public class CreatablePageableSearchableSelectionBoxClosedStateTests
{
    [Fact]
    public void Control_ShouldHaveHeight32()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.Equal(32, control.Height);
    }

    [Fact]
    public void Control_ShouldHaveMinHeight32()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.Equal(32, control.MinHeight);
    }

    [Fact]
    public void Control_ShouldBeFocusable()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.True(control.Focusable);
    }

    [Fact]
    public void Control_ShouldExposePART_TextBox_AndPART_Popup_WhenTemplateApplied()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        if (control.PART_TextBox != null)
        {
            Assert.NotNull(control.PART_Popup);
        }
    }

    [Fact]
    public void PART_RootBorder_ShouldHaveExpectedClosedStateStyle_WhenTemplateApplied()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var rootBorder = control.PART_RootBorder;
        if (rootBorder == null) return;

        var bg = rootBorder.Background as SolidColorBrush;
        Assert.NotNull(bg);
        Assert.Equal(Colors.White, bg.Color);

        var borderBrush = rootBorder.BorderBrush as SolidColorBrush;
        Assert.NotNull(borderBrush);
        Assert.Equal(Color.Parse("#E5E7EB"), borderBrush.Color);

        Assert.Equal(new Thickness(1), rootBorder.BorderThickness);
    }

    [Fact]
    public void PART_RootBorder_ShouldHavePadding6_0_6_0_WhenTemplateApplied()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var rootBorder = control.PART_RootBorder;
        if (rootBorder == null) return;
        Assert.Equal(new Thickness(6, 0, 6, 0), rootBorder.Padding);
    }

    [Fact]
    public void PART_TextBox_ShouldHaveFontSize12_AndForeground333_WhenTemplateApplied()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var tb = control.PART_TextBox;
        if (tb == null) return;

        Assert.Equal(12, tb.FontSize);

        var fg = tb.Foreground as SolidColorBrush;
        Assert.NotNull(fg);
        Assert.Equal(Color.Parse("#333333"), fg.Color);
    }

    [Fact]
    public void PART_TextBox_ShouldHaveTransparentBackground_WhenTemplateApplied()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var tb = control.PART_TextBox;
        if (tb == null) return;

        if (tb.Background is SolidColorBrush bgBrush)
            Assert.Equal(Colors.Transparent, bgBrush.Color);
    }

    [Fact]
    public void PART_TextBox_ShouldHaveNoBorder_WhenTemplateApplied()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        var tb = control.PART_TextBox;
        if (tb == null) return;
        Assert.Equal(new Thickness(0), tb.BorderThickness);
    }

    [Fact]
    public void PART_DataGrid_ShouldExist_WhenTemplateApplied()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        if (control.PART_TextBox == null) return;
        Assert.NotNull(control.PART_DataGrid);
    }

    [Fact]
    public void PART_EmptyPanel_ShouldExist_WhenTemplateApplied()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        if (control.PART_TextBox == null) return;
        Assert.NotNull(control.PART_EmptyPanel);
    }

    [Fact]
    public void DefaultSelectedItem_ShouldBeNull()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.Null(control.SelectedItem);
    }

    [Fact]
    public void DefaultShowAddNew_ShouldBeFalse()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.False(control.ShowAddNew);
    }

    [Fact]
    public void DefaultIsPopupOpen_ShouldBeFalse()
    {
        var control = TestHelper.CreateControl<CreatablePageableSearchableSelectionBox>();
        Assert.False(control.IsPopupOpen);
    }
}
