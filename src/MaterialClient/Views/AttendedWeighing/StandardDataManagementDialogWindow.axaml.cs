using System;
using Avalonia.Controls;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.AttendedWeighing;

public partial class StandardDataManagementDialogWindow : Window, ITransientDependency
{
    private readonly IDisposable? _closeSubscription;
    private readonly IDisposable? _confirmSubscription;

    public StandardDataManagementDialogWindow(
        StandardDataManagementDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        _closeSubscription = viewModel.CloseCommand.Subscribe(_ => Close(false));
        _confirmSubscription = viewModel.ConfirmCommand.Subscribe(_ => Close(true));
        viewModel.LoadDataCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closeSubscription?.Dispose();
        _confirmSubscription?.Dispose();
        base.OnClosed(e);
    }
}
