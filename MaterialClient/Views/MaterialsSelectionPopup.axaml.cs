using System;
using Avalonia.Controls;
using MaterialClient.Common.Entities;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
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