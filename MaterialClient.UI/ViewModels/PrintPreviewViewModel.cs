using System;
using System.IO;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.Logging;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.ViewModels;

public partial class PrintPreviewViewModel : ViewModelBase, IDisposable, ITransientDependency
{
    private readonly ITicketPrintingService _ticketPrintingService;
    private readonly ILogger<PrintPreviewViewModel>? _logger;

    [Reactive] private string? _previewImagePath;
    [Reactive] private string? _statusText;
    [Reactive] private bool _canPrint = true;

    public PrintPreviewViewModel(
        ITicketPrintingService ticketPrintingService,
        ILogger<PrintPreviewViewModel>? logger = null) : base(logger)
    {
        _ticketPrintingService = ticketPrintingService;
        _logger = logger;
    }

    public WeighingTicketDto? TicketDto { get; private set; }

    public void SetTicket(WeighingTicketDto ticketDto, string? previewImagePath)
    {
        TicketDto = ticketDto;
        PreviewImagePath = previewImagePath;
        StatusText = string.Empty;
        CanPrint = ticketDto != null;
    }

    [ReactiveCommand]
    private void Close()
    {
        // window handles CloseCommand subscription
    }

    [ReactiveCommand]
    private void Print()
    {
        if (!CanPrint)
            return;

        if (TicketDto == null)
        {
            StatusText = "未找到可打印的数据";
            return;
        }

        try
        {
            StatusText = "正在发送打印任务...";
            CanPrint = false;

            _ticketPrintingService.PrintToEpsonLq630K(TicketDto);

            StatusText = "已发送到打印机";
            CloseCommand.Execute().Subscribe();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Print preview print failed.");
            StatusText = $"打印失败：{ex.Message}";
            CanPrint = true;
        }
    }

    public void Dispose()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(PreviewImagePath) && File.Exists(PreviewImagePath))
                File.Delete(PreviewImagePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete preview image: {Path}", PreviewImagePath);
        }
    }
}

