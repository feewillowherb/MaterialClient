using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace MaterialClient.Views;

public partial class AnimatedDeliveryTypeRadioButton : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, string?>(nameof(Text));

    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, string?>(nameof(GroupName));

    public static readonly StyledProperty<bool?> IsCheckedProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, bool?>(nameof(IsChecked));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<bool> IsWeighingActiveProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, bool>(nameof(IsWeighingActive));

    public new static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, CornerRadius>(nameof(CornerRadius), new CornerRadius(0));

    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, Thickness>(nameof(ContentPadding), new Thickness(20, 6));

    private static readonly IBrush UncheckedForegroundBrush = Brushes.White;
    private static readonly IBrush CheckedForegroundBrush = new SolidColorBrush(Color.Parse("#5A7FE6"));

    private readonly DispatcherTimer _pulseTimer;
    private bool _pulseOn;

    private IBrush _baseForeground = UncheckedForegroundBrush;
    private double _baseOpacity = 1d;
    private double _targetOpacity;

    public static readonly DirectProperty<AnimatedDeliveryTypeRadioButton, IBrush> BaseForegroundProperty =
        AvaloniaProperty.RegisterDirect<AnimatedDeliveryTypeRadioButton, IBrush>(
            nameof(BaseForeground),
            o => o.BaseForeground);

    // PulseProgress must be StyledProperty (not DirectProperty) to support transitions
    public static readonly StyledProperty<double> PulseProgressProperty =
        AvaloniaProperty.Register<AnimatedDeliveryTypeRadioButton, double>(nameof(PulseProgress), defaultValue: 0d);

    public static readonly DirectProperty<AnimatedDeliveryTypeRadioButton, double> BaseOpacityProperty =
        AvaloniaProperty.RegisterDirect<AnimatedDeliveryTypeRadioButton, double>(
            nameof(BaseOpacity),
            o => o.BaseOpacity);

    public static readonly DirectProperty<AnimatedDeliveryTypeRadioButton, double> TargetOpacityProperty =
        AvaloniaProperty.RegisterDirect<AnimatedDeliveryTypeRadioButton, double>(
            nameof(TargetOpacity),
            o => o.TargetOpacity);

    public AnimatedDeliveryTypeRadioButton()
    {
        InitializeComponent();

        _pulseTimer = new DispatcherTimer
        {
            // 2000ms per leg: base -> target -> base ...
            Interval = TimeSpan.FromMilliseconds(2000)
        };
        _pulseTimer.Tick += (_, _) =>
        {
            _pulseOn = !_pulseOn;
            ApplyVisualState();
        };

        // React to state changes to update animated target values.
        this.GetObservable(IsCheckedProperty).Subscribe(_ => UpdateAnimationState());
        this.GetObservable(IsWeighingActiveProperty).Subscribe(_ => UpdateAnimationState());
        
        // Update complementary opacities when PulseProgress changes (via transition)
        this.GetObservable(PulseProgressProperty).Subscribe(progress =>
        {
            BaseOpacity = 1d - progress;
            TargetOpacity = progress;
        });

        // Initial state.
        UpdateAnimationState();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? GroupName
    {
        get => GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public bool? IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public bool IsWeighingActive
    {
        get => GetValue(IsWeighingActiveProperty);
        set => SetValue(IsWeighingActiveProperty, value);
    }

    public new CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public IBrush BaseForeground
    {
        get => _baseForeground;
        private set => SetAndRaise(BaseForegroundProperty, ref _baseForeground, value);
    }

    public double PulseProgress
    {
        get => GetValue(PulseProgressProperty);
        set => SetValue(PulseProgressProperty, value);
    }

    public double BaseOpacity
    {
        get => _baseOpacity;
        private set => SetAndRaise(BaseOpacityProperty, ref _baseOpacity, value);
    }

    public double TargetOpacity
    {
        get => _targetOpacity;
        private set => SetAndRaise(TargetOpacityProperty, ref _targetOpacity, value);
    }

    private void UpdateAnimationState()
    {
        var shouldPulse = IsChecked == true && IsWeighingActive;

        if (shouldPulse)
        {
            // Start from base state, then pulse to target and back.
            if (!_pulseTimer.IsEnabled)
            {
                _pulseOn = false;
                ApplyVisualState();
                _pulseTimer.Start();
            }
        }
        else
        {
            if (_pulseTimer.IsEnabled)
                _pulseTimer.Stop();

            _pulseOn = false;
            ApplyVisualState();
        }
    }

    private void ApplyVisualState()
    {
        var isChecked = IsChecked == true;
        var shouldPulse = isChecked && IsWeighingActive;

        if (!isChecked)
        {
            BaseForeground = UncheckedForegroundBrush;
            PulseProgress = 0d;
            return;
        }

        if (!shouldPulse)
        {
            BaseForeground = CheckedForegroundBrush;
            PulseProgress = 0d;
            return;
        }

        // Pulsing (layout-stable): crossfade between base and target layers.
        // PulseProgress controls opacity: 0 = base visible, 1 = target visible.
        // BaseOpacity = 1 - PulseProgress, TargetOpacity = PulseProgress (always sum to 1).
        BaseForeground = CheckedForegroundBrush;
        PulseProgress = _pulseOn ? 1d : 0d;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_pulseTimer.IsEnabled)
            _pulseTimer.Stop();
    }
}

