using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.AttendedWeighing;

public partial class ProviderManagementWindow : Window, ITransientDependency
{
    private readonly IDisposable? _closeSubscription;

    public ProviderManagementWindow(
        ProviderManagementViewModel viewModel,
        WindowNotificationManager? notificationManager = null)
    {
        InitializeComponent();
        DataContext = viewModel;

        _closeSubscription = viewModel.CloseCommand.Subscribe(_ => Close(false));
        viewModel.LoadDataCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closeSubscription?.Dispose();
        base.OnClosed(e);
    }
}
