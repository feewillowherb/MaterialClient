using System;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

public partial class ExportFilterDialogViewModel : ViewModelBase
{
    private Func<Task<string?>>? _browseHandler;

    [Reactive] private DateTime? _startDate;
    [Reactive] private DateTime? _endDate;
    [Reactive] private string? _plateNumber;
    [Reactive] private string? _savePath;
    [Reactive] private string? _savePathError;

    public bool Confirmed { get; private set; }

    public void SetBrowseHandler(Func<Task<string?>> handler) => _browseHandler = handler;

    [ReactiveCommand]
    private async Task BrowseFolder()
    {
        if (_browseHandler == null) return;
        var path = await _browseHandler();
        if (!string.IsNullOrEmpty(path))
        {
            SavePath = path;
            SavePathError = null;
        }
    }

    [ReactiveCommand]
    private void Export()
    {
        if (string.IsNullOrWhiteSpace(SavePath))
        {
            SavePathError = "请选择保存位置";
            return;
        }

        SavePathError = null;
        Confirmed = true;
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Confirmed = false;
    }
}
