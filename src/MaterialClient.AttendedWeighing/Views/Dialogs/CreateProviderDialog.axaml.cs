using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.Dialogs;

public partial class CreateProviderDialog : Window
{
    public CreateProviderDialog()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            NameTextBox?.Focus();
            NameTextBox?.SelectAll();
        };

        KeyDown += OnKeyDown;
    }

    public CreateProviderDialog(string title, string message, string initialName) : this()
    {
        Title = string.IsNullOrWhiteSpace(title) ? "新增供应商" : title;
        MessageText.Text = message ?? string.Empty;
        NameTextBox.Text = initialName ?? string.Empty;
        AddressTextBox.Text = string.Empty;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Confirm();
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
            Confirm();
            e.Handled = true;
        }
    }

    private void Confirm()
    {
        var name = NameTextBox.Text;
        var rawAddress = AddressTextBox.Text;
        var address = string.IsNullOrWhiteSpace(rawAddress) ? null : rawAddress.Trim();
        Close(new AttendedWeighingDetailViewModelBase.CreateProviderResult(name ?? string.Empty, address));
    }
}
