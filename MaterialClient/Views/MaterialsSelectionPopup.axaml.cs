using System;
using Avalonia.Controls;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views;

public partial class MaterialsSelectionPopup : UserControl, ITransientDependency
{
    public MaterialsSelectionPopup()
    {
        InitializeComponent();
    }

    public MaterialsSelectionPopup(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        DataContext = serviceProvider?.GetRequiredService<MaterialsSelectionPopupViewModel>();
    }
}