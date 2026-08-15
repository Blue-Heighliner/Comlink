namespace BlueHeighliner.Comlink.Engine.Views.Controls;

/// <summary>User control that displays a colored environment banner with a title and supports window drag.</summary>
[ExcludeFromCodeCoverage]
public partial class EnvironmentBanner : UserControl
{
    /// <summary>Identifies the <see cref="Title"/> styled property.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<EnvironmentBanner, string>(nameof(Title), string.Empty);

    /// <summary>Identifies the <see cref="BannerColor"/> styled property.</summary>
    public static readonly StyledProperty<string> BannerColorProperty =
        AvaloniaProperty.Register<EnvironmentBanner, string>(nameof(BannerColor), "#1565C0");

    /// <summary>Initializes the control, loads the AXAML layout, and applies the initial banner color.</summary>
    public EnvironmentBanner()
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
    public string BannerColor
    {
        get => GetValue(BannerColorProperty);
        set => SetValue(BannerColorProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
        {
            TextBlock? text = this.FindControl<TextBlock>("TitleText");
            if (text is not null) { text.Text = Title.ToUpperInvariant(); }
        }

        if (change.Property == BannerColorProperty)
        {
            ApplyColor();
        }
    }

    private void ApplyColor()
    {
        Border? border = this.FindControl<Border>("BannerBorder");
        if (border is not null && Color.TryParse(BannerColor, out Color color))
        {
            border.Background = new SolidColorBrush(color);
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
