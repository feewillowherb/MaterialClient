using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

public partial class ImageViewerViewModel : ViewModelBase
{
    [Reactive] private string? _imagePath;

    [Reactive] private string? _imageTitle;

    [ReactiveCommand]
    private void Close()
    {
        // 关闭窗口的逻辑将在窗口代码中处理
        // 这里可以通过事件或消息来通知窗口关闭
    }

    public void SetImage(string? imagePath, string? title = null)
    {
        ImagePath = imagePath;
        ImageTitle = title ?? "图片查看";
    }
}