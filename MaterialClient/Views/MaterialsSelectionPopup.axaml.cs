using System;
using Avalonia.Controls;
using MaterialClient.Common.Entities;
using MaterialClient.ViewModels;
using ReactiveUI;

namespace MaterialClient.Views;

public partial class MaterialsSelectionPopup : UserControl
{
    public MaterialsSelectionPopup()
    {
        InitializeComponent();
    }

    public MaterialsSelectionPopup(MaterialsSelectionPopupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    ///     材料选择完成事件
    /// </summary>
    public event EventHandler<Material?>? MaterialSelected;

    /// <summary>
    ///     选中的材料
    /// </summary>
    public Material? SelectedMaterial => (DataContext as MaterialsSelectionPopupViewModel)?.SelectedMaterial;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MaterialsSelectionPopupViewModel viewModel)
        {
            // 当选中材料变化时，触发选择事件
            viewModel.WhenAnyValue(x => x.SelectedMaterial)
                .Subscribe(material =>
                {
                    if (material != null) MaterialSelected?.Invoke(this, material);
                });
        }
    }
}

