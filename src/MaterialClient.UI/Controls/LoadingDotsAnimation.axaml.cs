using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MaterialClient.UI.Controls;

public partial class LoadingDotsAnimation : UserControl
{
    private DispatcherTimer? _animationTimer;
    private int _animationStep;

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<LoadingDotsAnimation, bool>(nameof(IsActive), defaultValue: false);

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public LoadingDotsAnimation()
    {
        InitializeComponent();

        this.GetObservable(IsActiveProperty)
            .Subscribe(OnIsActiveChanged);
    }

    private void OnIsActiveChanged(bool isActive)
    {
        if (isActive)
        {
            StartAnimation();
            IsVisible = true;
        }
        else
        {
            StopAnimation();
            IsVisible = false;
        }
    }

    private void StartAnimation()
    {
        _animationTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer.Tick += OnAnimationTick;

        _animationStep = 0;
        UpdateDotsText();

        if (!_animationTimer.IsEnabled)
            _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_animationTimer is { IsEnabled: true })
            _animationTimer.Stop();

        _animationStep = 0;
        UpdateDotsText();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        _animationStep = (_animationStep + 1) % 3;
        UpdateDotsText();
    }

    private void UpdateDotsText()
    {
        if (DotsTextBlock != null)
        {
            DotsTextBlock.Text = _animationStep switch
            {
                0 => ".",
                1 => ". .",
                _ => ". . ."
            };
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_animationTimer != null)
        {
            _animationTimer.Stop();
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer = null;
        }
    }
}
