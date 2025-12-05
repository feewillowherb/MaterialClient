using System;
using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class AttendedWeighingWindow : Window
{
    public AttendedWeighingWindow(AttendedWeighingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnClosed(e);
    }
}
