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
    private static readonly IBrush ActiveCheckedForegroundBrush = new SolidColorBrush(Colors.Red);

    private readonly DispatcherTimer _pulseTimer;
    private bool _pulseOn;

    private IBrush _animatedForeground = UncheckedForegroundBrush;
    private double _animatedFontSize = 14;
    private FontWeight _animatedFontWeight = FontWeight.Normal;

    public static readonly DirectProperty<AnimatedDeliveryTypeRadioButton, IBrush> AnimatedForegroundProperty =
        AvaloniaProperty.RegisterDirect<AnimatedDeliveryTypeRadioButton, IBrush>(
            nameof(AnimatedForeground),
            o => o.AnimatedForeground);

    public static readonly DirectProperty<AnimatedDeliveryTypeRadioButton, double> AnimatedFontSizeProperty =
        AvaloniaProperty.RegisterDirect<AnimatedDeliveryTypeRadioButton, double>(
            nameof(AnimatedFontSize),
            o => o.AnimatedFontSize);

    public static readonly DirectProperty<AnimatedDeliveryTypeRadioButton, FontWeight> AnimatedFontWeightProperty =
        AvaloniaProperty.RegisterDirect<AnimatedDeliveryTypeRadioButton, FontWeight>(
            nameof(AnimatedFontWeight),
            o => o.AnimatedFontWeight);

    public AnimatedDeliveryTypeRadioButton()
    {
        InitializeComponent();

        _pulseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _pulseTimer.Tick += (_, _) =>
        {
            _pulseOn = !_pulseOn;
            ApplyVisualState();
        };

        // React to state changes to update animated target values.
        this.GetObservable(IsCheckedProperty).Subscribe(_ => UpdateAnimationState());
        this.GetObservable(IsWeighingActiveProperty).Subscribe(_ => UpdateAnimationState());

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

    public IBrush AnimatedForeground
    {
        get => _animatedForeground;
        private set => SetAndRaise(AnimatedForegroundProperty, ref _animatedForeground, value);
    }

    public double AnimatedFontSize
    {
        get => _animatedFontSize;
        private set => SetAndRaise(AnimatedFontSizeProperty, ref _animatedFontSize, value);
    }

    public FontWeight AnimatedFontWeight
    {
        get => _animatedFontWeight;
        private set => SetAndRaise(AnimatedFontWeightProperty, ref _animatedFontWeight, value);
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
            AnimatedForeground = UncheckedForegroundBrush;
            AnimatedFontSize = 14;
            AnimatedFontWeight = FontWeight.Normal;
            return;
        }

        if (!shouldPulse)
        {
            AnimatedForeground = CheckedForegroundBrush;
            AnimatedFontSize = 14;
            AnimatedFontWeight = FontWeight.Normal;
            return;
        }

        // Pulsing: base(checked) <-> target(active checked)
        AnimatedForeground = _pulseOn ? ActiveCheckedForegroundBrush : CheckedForegroundBrush;
        AnimatedFontSize = _pulseOn ? 16 : 14;
        AnimatedFontWeight = _pulseOn ? FontWeight.Bold : FontWeight.Normal;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_pulseTimer.IsEnabled)
            _pulseTimer.Stop();
    }
}

