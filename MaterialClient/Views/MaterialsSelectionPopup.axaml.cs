using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.Common.Entities;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views;

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