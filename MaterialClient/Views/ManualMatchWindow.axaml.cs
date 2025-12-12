using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MaterialClient.Views;

public partial class ManualMatchWindow : Window
{
    public ManualMatchWindow()
    {
        InitializeComponent();
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        // 确定按钮点击处理
        Close();
    }
}
