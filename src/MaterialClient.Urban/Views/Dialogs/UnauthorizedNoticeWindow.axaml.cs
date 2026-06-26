using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MaterialClient.Urban.Views.Dialogs;

public partial class UnauthorizedNoticeWindow : Window
{
    public enum UnauthorizedNoticeResult
    {
        Exit,
        OnlineActivate
    }

    public UnauthorizedNoticeResult UserChoice { get; private set; } = UnauthorizedNoticeResult.Exit;

    public UnauthorizedNoticeWindow()
    {
        InitializeComponent();
    }

    public UnauthorizedNoticeWindow(string? failureMessage, string machineCode) : this()
    {
        MachineCodeTextBox.Text = machineCode;

        if (string.IsNullOrWhiteSpace(failureMessage))
        {
            return;
        }

        DetailTextBlock.Text = failureMessage;
        DetailTextBlock.IsVisible = true;
    }

    private async void OnCopyMachineCodeClick(object? sender, RoutedEventArgs e)
    {
        var machineCode = MachineCodeTextBox.Text;
        if (string.IsNullOrWhiteSpace(machineCode))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            return;
        }

        await clipboard.SetTextAsync(machineCode);
        CopyMachineCodeButton.Content = "已复制";
        await Task.Delay(1500);
        CopyMachineCodeButton.Content = "复制";
    }

    private void OnOnlineActivateClick(object? sender, RoutedEventArgs e)
    {
        UserChoice = UnauthorizedNoticeResult.OnlineActivate;
        // 保持窗口打开作对话框父级；由 App 在激活流程结束后再关闭，避免关闭 MainWindow 导致应用退出
        OnlineActivateRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OnlineActivateRequested;

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        UserChoice = UnauthorizedNoticeResult.Exit;
        Close();
    }
}
