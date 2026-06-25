using System;
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

    public UnauthorizedNoticeWindow(string? failureMessage) : this()
    {
        if (string.IsNullOrWhiteSpace(failureMessage))
        {
            return;
        }

        DetailTextBlock.Text = failureMessage;
        DetailTextBlock.IsVisible = true;
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
