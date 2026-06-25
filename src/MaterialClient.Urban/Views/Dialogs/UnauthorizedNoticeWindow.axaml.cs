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
        Close();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        UserChoice = UnauthorizedNoticeResult.Exit;
        Close();
    }
}
