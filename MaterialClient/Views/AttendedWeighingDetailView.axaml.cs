using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using MaterialClient.Common.Entities;
using System;
using System.Globalization;

namespace MaterialClient.Views;

public partial class AttendedWeighingDetailView : Window
{
    private WeighingRecord? _weighingRecord;

    public AttendedWeighingDetailView()
    {
        InitializeComponent();
        InitializeValidation();
    }

    public AttendedWeighingDetailView(WeighingRecord weighingRecord) : this()
    {
        _weighingRecord = weighingRecord;
        LoadData();
    }

    private void InitializeValidation()
    {
        // 失去焦点时验证运单数量
        TxtWaybillQuantity.LostFocus += (sender, e) =>
        {
            ValidateWaybillQuantity();
        };
    }

    private bool IsValidWaybillQuantity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true; // 允许为空

        // 验证是否为有效的十进制数
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal result))
            return false;

        // 验证是否大于0
        if (result <= 0)
            return false;

        // 验证小数位数（最多两位）
        var parts = value.Split('.');
        if (parts.Length > 1 && parts[1].Length > 2)
            return false;

        return true;
    }

    private void ValidateWaybillQuantity()
    {
        var value = TxtWaybillQuantity.Text;
        if (!string.IsNullOrWhiteSpace(value) && !IsValidWaybillQuantity(value))
        {
            // TODO: 显示错误提示
            TxtWaybillQuantity.Text = string.Empty;
        }
    }

    private void LoadData()
    {
        if (_weighingRecord == null) return;

        // 根据MatchedId控制匹配按钮的可见性
        BtnMatch.IsVisible = _weighingRecord.MatchedId == null;

        // 更新重量显示
        UpdateWeightDisplay(LblAllWeight, _weighingRecord.Weight, "#427FF9");
        UpdateWeightDisplay(LblTruckWeight, 0, "#427FF9"); // TODO: 从数据获取皮重
        UpdateWeightDisplay(LblGoodsWeight, _weighingRecord.Weight, "#F5A000"); // TODO: 计算净重

        // TODO: 加载并绑定数据到界面
        // TODO: 加载供应商列表到CbProvider
        // TODO: 加载材料列表到CbMaterial
        // TODO: 加载材料单位列表到CbMaterialUnit（根据选择的材料）
        // TODO: 绑定WeighingRecord的数据到各个控件
    }

    /// <summary>
    /// 更新重量显示（保持颜色格式）
    /// </summary>
    private void UpdateWeightDisplay(TextBlock textBlock, decimal weight, string colorHex)
    {
        textBlock.Inlines?.Clear();
        textBlock.Inlines?.Add(new Run
        {
            Text = weight.ToString("F2"),
            Foreground = new SolidColorBrush(Color.Parse(colorHex)),
            FontWeight = FontWeight.Bold
        });
        textBlock.Inlines?.Add(new Run
        {
            Text = " 吨",
            Foreground = new SolidColorBrush(Color.Parse("#333333"))
        });
    }

    private void Btn_Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    // TODO: 实现匹配功能
    private void BtnMatch_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: 实现匹配逻辑
    }

    // TODO: 实现废单功能（软删除）
    private void BtnAbolish_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: 实现软删除逻辑
    }

    // TODO: 实现保存功能
    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        // 验证运单数量：大于0，两位小数
        if (!string.IsNullOrWhiteSpace(TxtWaybillQuantity.Text))
        {
            if (!IsValidWaybillQuantity(TxtWaybillQuantity.Text))
            {
                // TODO: 显示错误提示消息框
                return;
            }
        }

        // TODO: 验证其他输入数据
        // TODO: 保存WeighingRecord数据
    }

    // TODO: 实现完成本次收货功能
    private void BtnComplete_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: 实现完成本次收货逻辑
    }

    // TODO: 实现新增材料功能
    private void BtnAddGoods_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: 实现新增材料逻辑
    }
}
