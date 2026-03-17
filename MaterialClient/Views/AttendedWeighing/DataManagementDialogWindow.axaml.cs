using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.AttendedWeighing;

public partial class DataManagementDialogWindow : Window, ITransientDependency
{
    private readonly IDisposable? _closeSubscription;
    private readonly IDisposable? _confirmSubscription;

    public DataManagementDialogWindow(
        DataManagementDialogViewModel viewModel,
        WindowNotificationManager? notificationManager = null)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.SetBrowseHandler(async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "选择导出目录" });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        });

        if (notificationManager != null)
        {
            viewModel.SetNotifyHandler((title, message, isSuccess) =>
                notificationManager.Show(new Notification(title, message,
                    isSuccess ? NotificationType.Success : NotificationType.Error)));
        }

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
