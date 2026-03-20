using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MaterialClient.Views.Dialogs;

public partial class ConfirmTextDialog : Window
{
    public ConfirmTextDialog()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            InputTextBox?.Focus();
            InputTextBox?.SelectAll();
        };

        KeyDown += OnKeyDown;
    }

    public ConfirmTextDialog(string title, string message, string initialValue) : this()
    {
        Title = string.IsNullOrWhiteSpace(title) ? "确认" : title;
        MessageText.Text = message ?? string.Empty;
        InputTextBox.Text = initialValue ?? string.Empty;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(InputTextBox.Text);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            Close(InputTextBox.Text);
            e.Handled = true;
        }
    }
}

