namespace BlueHeighliner.Comlink;

/// <summary>User control that displays a colored message level banner with a title and supports window drag.</summary>
[ExcludeFromCodeCoverage]
internal partial class MessageLevelBanner : UserControl
{
    /// <summary>Identifies the <see cref="Title"/> styled property.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MessageLevelBanner, string>(nameof(Title), string.Empty);

    /// <summary>Identifies the <see cref="BannerBackground"/> styled property.</summary>
    public static readonly StyledProperty<string> BannerBackgroundProperty =
        AvaloniaProperty.Register<MessageLevelBanner, string>(nameof(BannerBackground), "#1565C0");

    /// <summary>Identifies the <see cref="BannerForeground"/> styled property.</summary>
    public static readonly StyledProperty<string> BannerForegroundProperty =
        AvaloniaProperty.Register<MessageLevelBanner, string>(nameof(BannerForeground), "#FFFFFF");

    /// <summary>Initializes the control, loads the AXAML layout, and applies the initial banner colors.</summary>
    public MessageLevelBanner()
    {
        InitializeComponent();
        ApplyColor();
    }

    /// <summary>Gets or sets the banner title text.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the hex color string used for the banner background.</summary>
    public string BannerBackground
    {
        get => GetValue(BannerBackgroundProperty);
        set => SetValue(BannerBackgroundProperty, value);
    }

    /// <summary>Gets or sets the hex color string used for the banner's text.</summary>
    public string BannerForeground
    {
        get => GetValue(BannerForegroundProperty);
        set => SetValue(BannerForegroundProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
        {
            TextBlock? text = this.FindControl<TextBlock>("TitleText");
            if (text is not null)
            {
                text.Text = Title.ToUpperInvariant();
            }
        }

        if (change.Property == BannerBackgroundProperty || change.Property == BannerForegroundProperty)
        {
            ApplyColor();
        }
    }

    private void ApplyColor()
    {
        Border? border = this.FindControl<Border>("BannerBorder");
        if (border is not null && Color.TryParse(BannerBackground, out Color background))
        {
            border.Background = new SolidColorBrush(background);
        }

        TextBlock? text = this.FindControl<TextBlock>("TitleText");
        if (text is not null && Color.TryParse(BannerForeground, out Color foreground))
        {
            text.Foreground = new SolidColorBrush(foreground);
        }
    }

    private void OnBannerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (VisualRoot is Window window && e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
        {
            window.BeginMoveDrag(e);
        }
    }
}
