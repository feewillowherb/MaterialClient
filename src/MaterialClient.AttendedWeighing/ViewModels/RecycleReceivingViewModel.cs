using System;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

/// <summary>
///     收货确认结果：收货时间（完整 DateTime）+ 收货照片本地路径。
/// </summary>
public record RecycleReceivingResult(DateTime ReceivingTime, string ImagePath);

/// <summary>
///     收货确认对话框 ViewModel。
///     录入收货时间与收货照片（必填），确认/取消。
///     文件选择经 View 层注入的 handler（VM 不直接访问 StorageProvider）。
/// </summary>
public partial class RecycleReceivingViewModel : ViewModelBase
{
    private Func<Task<string?>>? _imagePickerHandler;

    /// <summary>运单号（仅展示）</summary>
    [Reactive] private string _orderNo = string.Empty;

    /// <summary>收货时间（必填，Ursa DateTimePicker 绑定 DateTime?）</summary>
    [Reactive] private DateTime? _receivingDateTime = DateTime.Now;

    /// <summary>收货照片本地路径（必填）</summary>
    [Reactive] private string? _selectedImagePath;

    /// <summary>校验错误提示</summary>
    [Reactive] private string? _errorMessage;

    public RecycleReceivingResult? Result { get; private set; }

    /// <summary>收货照片预览路径（View 绑定 Image.Source 用）</summary>
    public string? PreviewImagePath => SelectedImagePath;

    public void SetImagePickerHandler(Func<Task<string?>> handler) => _imagePickerHandler = handler;

    public void Initialize(string orderNo)
    {
        OrderNo = orderNo ?? string.Empty;
        ReceivingDateTime = DateTime.Now;
        SelectedImagePath = null;
        ErrorMessage = null;
        Result = null;
    }

    [ReactiveCommand]
    private async Task SelectImageAsync()
    {
        if (_imagePickerHandler == null) return;
        var path = await _imagePickerHandler();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SelectedImagePath = path;
            ErrorMessage = null;
            this.RaisePropertyChanged(nameof(PreviewImagePath));
        }
    }

    [ReactiveCommand]
    private void Confirm()
    {
        if (!ReceivingDateTime.HasValue)
        {
            ErrorMessage = "收货时间为必填";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedImagePath))
        {
            ErrorMessage = "收货照片为必填";
            return;
        }

        ErrorMessage = null;
        Result = new RecycleReceivingResult(ReceivingDateTime.Value, SelectedImagePath);
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Result = null;
    }
}
