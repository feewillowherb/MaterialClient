using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MaterialClient.Views.AttendedWeighing;

public partial class DataManagementDialogWindow : Window
{
    public ObservableCollection<LedgerRecord> Records { get; } = new();

    public int CurrentPage { get; set; } = 1;

    public int TotalCount { get; set; } = 1;

    public int TotalPages { get; set; } = 1;

    public DataManagementDialogWindow()
    {
        InitializeComponent();
        DataContext = this;

        // 添加一条用于样式验收的测试数据
        Records.Add(new LedgerRecord
        {
            OrderNo = "sl-20251118153228-001",
            TruckNo = "浙A12345",
            TypeName = "收料",
            GoodsNameSize = "混泥土",
            OrderTypeName = "收货中",
            GoodsPlanOnPcsDesc = "1 m³",
            GoodsPlanOnWeightDesc = "2.28 吨",
            GoodsTakeWeightDesc = "0 吨",
            GoodsPcsDesc = "0.943 m³",
            GoodsWeightDesc = "2 吨",
            UnitNameRate = "2.28 吨/m³",
            JoinTime = "2025-11-18 09:32:01",
            OutTime = "2025-11-18 10:01:23",
            ProviderName = "测试供应商",
            DispatchNo = "FH20251118001",
            Remark = "样式验收测试数据"
        });
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}

public class LedgerRecord
{
    public string OrderNo { get; set; } = string.Empty;
    public string TruckNo { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string GoodsNameSize { get; set; } = string.Empty;
    public string OrderTypeName { get; set; } = string.Empty;
    public string GoodsPlanOnPcsDesc { get; set; } = string.Empty;
    public string GoodsPlanOnWeightDesc { get; set; } = string.Empty;
    public string GoodsTakeWeightDesc { get; set; } = string.Empty;
    public string GoodsPcsDesc { get; set; } = string.Empty;
    public string GoodsWeightDesc { get; set; } = string.Empty;
    public string UnitNameRate { get; set; } = string.Empty;
    public string JoinTime { get; set; } = string.Empty;
    public string OutTime { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string DispatchNo { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}

