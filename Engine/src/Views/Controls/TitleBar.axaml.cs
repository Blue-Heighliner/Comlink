namespace BlueHeighliner.Comlink.Engine.Views.Controls;

/// <summary>User control for the application title bar, providing window controls, site info, and action buttons.</summary>
[ExcludeFromCodeCoverage]
public partial class TitleBar : UserControl
{
    /// <summary>Identifies the <see cref="SiteName"/> styled property.</summary>
    public static readonly StyledProperty<string> SiteNameProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(SiteName), string.Empty);

    /// <summary>Identifies the <see cref="AppVersion"/> styled property.</summary>
    public static readonly StyledProperty<string> AppVersionProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(AppVersion), string.Empty);

    /// <summary>Identifies the <see cref="CreateDraftCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> CreateDraftCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(CreateDraftCommand));

    /// <summary>Identifies the <see cref="CreateNoteCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> CreateNoteCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(CreateNoteCommand));

    /// <summary>Identifies the <see cref="IsInstallScreenVisible"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsInstallScreenVisibleProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsInstallScreenVisible));

    /// <summary>Identifies the <see cref="IsKioskMode"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsKioskModeProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsKioskMode));

    /// <summary>Gets or sets the site name displayed in the title bar.</summary>
    public string SiteName
    {
        get => GetValue(SiteNameProperty);
        set => SetValue(SiteNameProperty, value);
    }

    /// <summary>Gets or sets the application version string displayed alongside the site name.</summary>
    public string AppVersion
    {
        get => GetValue(AppVersionProperty);
        set => SetValue(AppVersionProperty, value);
    }

    /// <summary>Gets or sets the command invoked when the user clicks the New Draft button.</summary>
    public ICommand? CreateDraftCommand
    {
        get => GetValue(CreateDraftCommandProperty);
        set => SetValue(CreateDraftCommandProperty, value);
    }

    /// <summary>Gets or sets the command invoked when the user clicks the New Note button.</summary>
    public ICommand? CreateNoteCommand
    {
        get => GetValue(CreateNoteCommandProperty);
        set => SetValue(CreateNoteCommandProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the install screen is currently visible, which hides action buttons.</summary>
    public bool IsInstallScreenVisible
    {
        get => GetValue(IsInstallScreenVisibleProperty);
        set => SetValue(IsInstallScreenVisibleProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether kiosk mode is active, which hides minimize and maximize controls.</summary>
    public bool IsKioskMode
    {
        get => GetValue(IsKioskModeProperty);
        set => SetValue(IsKioskModeProperty, value);
    }

    /// <summary>Initializes the control and loads the AXAML layout.</summary>
    public TitleBar()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SiteNameProperty || change.Property == AppVersionProperty)
            UpdateSiteInfo();
        if (change.Property == IsInstallScreenVisibleProperty)
        {
            var panel = this.FindControl<StackPanel>("ActionButtonsPanel");
            if (panel is not null) panel.IsVisible = !IsInstallScreenVisible;
        }
        if (change.Property == IsKioskModeProperty)
            ApplyKioskMode();

    }

    private void UpdateSiteInfo()
    {
        var tb = this.FindControl<TextBlock>("SiteInfoText");
        if (tb is null) return;
        tb.Text = string.IsNullOrEmpty(AppVersion) ? SiteName : $"{SiteName} v{AppVersion}";
    }

    private void OnDraftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        CreateDraftCommand?.Execute(null);

    private void OnNoteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        CreateNoteCommand?.Execute(null);

    private void ApplyKioskMode()
    {
        var minimize = this.FindControl<Button>("MinimizeButton");
        var maximize = this.FindControl<Button>("MaximizeButton");
        var close = this.FindControl<Button>("CloseButton");
        if (minimize is not null) minimize.IsVisible = !IsKioskMode;
        if (maximize is not null) maximize.IsVisible = !IsKioskMode;
        if (close is not null) close.Content = IsKioskMode ? "↺" : "✕";
    }

    private void OnDragAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender)) return;
        if (VisualRoot is Window window && e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
            window.BeginMoveDrag(e);
    }

    private void OnMinimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (VisualRoot is Window w) w.WindowState = WindowState.Minimized;
    }

    private void OnMaximize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (VisualRoot is Window w)
            w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (VisualRoot is Window w) w.Close();
    }
}
