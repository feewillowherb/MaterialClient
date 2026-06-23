using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MaterialClient.Urban.Views.Dialogs;

public partial class UnauthorizedNoticeWindow : Window
{
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

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close();
}
