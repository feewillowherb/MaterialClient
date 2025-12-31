using System;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.Common.Entities;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace MaterialClient.Views.AttendedWeighing;

public partial class AttendedWeighingDetailView : UserControl
{
    private readonly IServiceProvider? _serviceProvider;

    public AttendedWeighingDetailView()
        : this(null)
    {
    }

    public AttendedWeighingDetailView(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        DataContext = serviceProvider?.GetService<AttendedWeighingDetailViewModel>();

        // 初始化弹窗
        InitializePopup();
    }

    private void InitializePopup()
    {
        // if (DataContext is AttendedWeighingDetailViewModel viewModel && _serviceProvider != null)
        // {
        //     // 创建并设置 ViewModel
        //     var popupViewModel = _serviceProvider.GetRequiredService<MaterialsSelectionPopupViewModel>();
        //     MaterialsSelectionPopupControl.DataContext = popupViewModel;
        //
        //     // 订阅材料选择事件
        //     MaterialsSelectionPopupControl.MaterialSelected += (sender, material) =>
        //     {
        //         if (material != null)
        //         {
        //             viewModel.SelectMaterialCommand.Execute(material);
        //         }
        //     };
        //
        //     // 当弹窗打开时刷新数据
        //     viewModel.WhenAnyValue(x => x.IsMaterialPopupOpen)
        //         .Subscribe(isOpen =>
        //         {
        //             if (isOpen)
        //             {
        //                 _ = popupViewModel.RefreshAsync();
        //             }
        //         });
        // }
    }

    private void OnMaterialNameCellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // if (sender is Border border && border.DataContext is MaterialItemRow row)
        // {
        //     if (DataContext is AttendedWeighingDetailViewModel viewModel)
        //     {
        //         // 设置弹窗的 PlacementTarget
        //         if (MaterialSelectionPopup != null && border.Parent != null)
        //         {
        //             MaterialSelectionPopup.PlacementTarget = border;
        //         }
        //
        //         // 打开弹窗
        //         viewModel.OpenMaterialSelectionCommand.Execute(row);
        //     }
        // }
    }
}