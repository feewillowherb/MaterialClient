using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.Common.Entities;
using MaterialClient.UI.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Views.Controls;

public partial class MaterialsSelectionPopup : UserControl, ITransientDependency
{
    public MaterialsSelectionPopup()
    {
        InitializeComponent();
    }

    private void OnMaterialDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid && 
            dataGrid.SelectedItem is Material selectedMaterial &&
            DataContext is MaterialsSelectionPopupViewModel viewModel)
        {
            // 调用 ViewModel 的命令，遵循 MVVM 模式
            viewModel.SelectMaterialCommand.Execute(selectedMaterial);
        }
    }
}