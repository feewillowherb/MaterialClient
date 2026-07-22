using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using MaterialClient.ViewModels;
using MaterialClient.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.Views.Controls;

public partial class AttendedWeighingDetailView : UserControl
{
    private readonly CompositeDisposable _lifetimeDisposables = new();
    private CompositeDisposable _interactionDisposables = new();

    public AttendedWeighingDetailView()
        : this(null)
    {
    }

    public AttendedWeighingDetailView(IServiceProvider? serviceProvider)
    {
        InitializeComponent();
        DataContext = serviceProvider?.GetService<AttendedWeighingDetailViewModelBase>();
        this.GetObservable(DataContextProperty)
            .Subscribe(_ => WireInteractions(DataContext as AttendedWeighingDetailViewModelBase))
            .DisposeWith(_lifetimeDisposables);
        // if (DataContext is AttendedWeighingDetailViewModel viewModel)
        // {
        //     viewModel.WhenAnyValue(x => x.MaterialsSelectionPopupViewModel)
        //         .Subscribe(popupViewModel =>
        //         {
        //             if (popupViewModel != null)
        //             {
        //                 MaterialsSelectionPopupControl.DataContext = popupViewModel;
        //             }
        //         });
        // }

    }

    private void WireInteractions(AttendedWeighingDetailViewModelBase? viewModel)
    {
        _interactionDisposables.Dispose();
        _interactionDisposables = new CompositeDisposable();

        if (viewModel == null) return;

        viewModel.ConfirmTextInteraction.RegisterHandler(async ctx =>
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                // 没有 owner 时不弹窗，等价于取消（“像什么都没发生”）
                ctx.SetOutput(null);
                return;
            }

            var req = ctx.Input;
            var dialog = new ConfirmTextDialog(req.Title, req.Message, req.InitialValue);
            var result = await dialog.ShowDialog<string?>(owner);
            ctx.SetOutput(result);
        }).DisposeWith(_interactionDisposables);

        viewModel.CreateProviderInteraction.RegisterHandler(async ctx =>
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                ctx.SetOutput(null);
                return;
            }

            var req = ctx.Input;
            var dialog = new CreateProviderDialog(req.Title, req.Message, req.InitialName);
            var result = await dialog.ShowDialog<AttendedWeighingDetailViewModelBase.CreateProviderResult?>(owner);
            ctx.SetOutput(result);
        }).DisposeWith(_interactionDisposables);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _interactionDisposables.Dispose();
        _lifetimeDisposables.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

}