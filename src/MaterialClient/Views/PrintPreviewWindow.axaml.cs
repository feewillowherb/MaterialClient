using System;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views;

public partial class PrintPreviewWindow : Window, ITransientDependency
{
    private readonly IDisposable? _closeSubscription;
    private readonly IDisposable? _printSubscription;

    public PrintPreviewWindow(PrintPreviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (viewModel.CloseCommand != null)
            _closeSubscription = viewModel.CloseCommand.Subscribe(_ => Close());

        if (viewModel.PrintCommand != null)
            _printSubscription = viewModel.PrintCommand.Subscribe(_ => { });

        KeyDown += OnKeyDown;
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        KeyDown -= OnKeyDown;
        _closeSubscription?.Dispose();
        _printSubscription?.Dispose();

        if (DataContext is IDisposable disposable)
            disposable.Dispose();

        base.OnClosed(e);
    }
}

