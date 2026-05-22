using Avalonia;
using Avalonia.Controls;

namespace MaterialClient.UI.Controls;

public partial class CameraStatusDetailPopup : UserControl
{
    public static readonly StyledProperty<IEnumerable<Models.CameraStatusDetailItem>?> CameraStatusDetailsProperty =
        AvaloniaProperty.Register<CameraStatusDetailPopup, IEnumerable<Models.CameraStatusDetailItem>?>(
            nameof(CameraStatusDetails));

    public static readonly StyledProperty<bool> HasCameraStatusDetailsProperty =
        AvaloniaProperty.Register<CameraStatusDetailPopup, bool>(nameof(HasCameraStatusDetails));

    public CameraStatusDetailPopup()
    {
        InitializeComponent();
    }

    public IEnumerable<Models.CameraStatusDetailItem>? CameraStatusDetails
    {
        get => GetValue(CameraStatusDetailsProperty);
        set => SetValue(CameraStatusDetailsProperty, value);
    }

    public bool HasCameraStatusDetails
    {
        get => GetValue(HasCameraStatusDetailsProperty);
        set => SetValue(HasCameraStatusDetailsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CameraStatusDetailsProperty)
        {
            var count = (change.NewValue as IEnumerable<Models.CameraStatusDetailItem>)?.Count() ?? 0;
            HasCameraStatusDetails = count > 0;
        }
    }
}
