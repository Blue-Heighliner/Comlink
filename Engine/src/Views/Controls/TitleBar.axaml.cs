namespace BlueHeighliner.Comlink.Engine.Views.Controls;

/// <summary>User control for the application title bar, providing window controls, user info, and action buttons.</summary>
[ExcludeFromCodeCoverage]
public partial class TitleBar : UserControl
{
    /// <summary>Identifies the <see cref="UserName"/> styled property.</summary>
    public static readonly StyledProperty<string> UserNameProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(UserName), string.Empty);

    /// <summary>Identifies the <see cref="AppVersion"/> styled property.</summary>
    public static readonly StyledProperty<string> AppVersionProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(AppVersion), string.Empty);

    /// <summary>Identifies the <see cref="CreateDraftCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> CreateDraftCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(CreateDraftCommand));

    /// <summary>Identifies the <see cref="CreateNoteCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> CreateNoteCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(CreateNoteCommand));

    /// <summary>Identifies the <see cref="ShowExportCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ShowExportCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(ShowExportCommand));

    /// <summary>Identifies the <see cref="ShowImportCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ShowImportCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(ShowImportCommand));

    /// <summary>Identifies the <see cref="ShowPrintManagerCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ShowPrintManagerCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(ShowPrintManagerCommand));

    /// <summary>Identifies the <see cref="IsInstallScreenVisible"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsInstallScreenVisibleProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsInstallScreenVisible));

    /// <summary>Identifies the <see cref="IsKioskMode"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsKioskModeProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsKioskMode));

    /// <summary>Identifies the <see cref="IsAlerting"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsAlertingProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsAlerting));

    /// <summary>Identifies the <see cref="AlertText"/> styled property.</summary>
    public static readonly StyledProperty<string> AlertTextProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(AlertText), "ALERT");

    /// <summary>Identifies the <see cref="QuickConfirmationEnabled"/> styled property.</summary>
    public static readonly StyledProperty<bool> QuickConfirmationEnabledProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(QuickConfirmationEnabled), true);

    /// <summary>Identifies the <see cref="AlertCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> AlertCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(AlertCommand));

    /// <summary>Initializes the control and loads the AXAML layout.</summary>
    public TitleBar()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the user name displayed in the title bar.</summary>
    public string UserName
    {
        get => GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    /// <summary>Gets or sets the application version string displayed alongside the user name.</summary>
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

    /// <summary>Gets or sets the command invoked when the user clicks the Export button.</summary>
    public ICommand? ShowExportCommand
    {
        get => GetValue(ShowExportCommandProperty);
        set => SetValue(ShowExportCommandProperty, value);
    }

    /// <summary>Gets or sets the command invoked when the user clicks the Import button.</summary>
    public ICommand? ShowImportCommand
    {
        get => GetValue(ShowImportCommandProperty);
        set => SetValue(ShowImportCommandProperty, value);
    }

    /// <summary>Gets or sets the command invoked when the user clicks the Prints button.</summary>
    public ICommand? ShowPrintManagerCommand
    {
        get => GetValue(ShowPrintManagerCommandProperty);
        set => SetValue(ShowPrintManagerCommandProperty, value);
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

    /// <summary>Gets or sets a value indicating whether one or more alert messages are pending, showing the alert box.</summary>
    public bool IsAlerting
    {
        get => GetValue(IsAlertingProperty);
        set => SetValue(IsAlertingProperty, value);
    }

    /// <summary>Gets or sets the text displayed in the alert box.</summary>
    public string AlertText
    {
        get => GetValue(AlertTextProperty);
        set => SetValue(AlertTextProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether clicking the alert box quick-confirms the latest pending alert.</summary>
    public bool QuickConfirmationEnabled
    {
        get => GetValue(QuickConfirmationEnabledProperty);
        set => SetValue(QuickConfirmationEnabledProperty, value);
    }

    /// <summary>Gets or sets the command invoked when the user clicks the alert box (subject to <see cref="QuickConfirmationEnabled"/>).</summary>
    public ICommand? AlertCommand
    {
        get => GetValue(AlertCommandProperty);
        set => SetValue(AlertCommandProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UserNameProperty || change.Property == AppVersionProperty)
        {
            UpdateUserInfo();
        }
        if (change.Property == IsInstallScreenVisibleProperty)
        {
            var panel = this.FindControl<StackPanel>("ActionButtonsPanel");
            if (panel is not null) { panel.IsVisible = !IsInstallScreenVisible; }
        }
        if (change.Property == IsKioskModeProperty)
        {
            ApplyKioskMode();
        }
        if (change.Property == IsAlertingProperty)
        {
            Border? box = this.FindControl<Border>("AlertBox");
            if (box is not null) { box.IsVisible = IsAlerting; }
        }
        if (change.Property == AlertTextProperty)
        {
            TextBlock? tb = this.FindControl<TextBlock>("AlertBoxText");
            if (tb is not null) { tb.Text = AlertText; }
        }
    }

    private void UpdateUserInfo()
    {
        var tb = this.FindControl<TextBlock>("UserInfoText");
        if (tb is null) { return; }
        tb.Text = string.IsNullOrEmpty(AppVersion) ? UserName : $"{UserName} v{AppVersion}";
    }

    private void OnDraftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CreateDraftCommand?.Execute(null);

    private void OnNoteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CreateNoteCommand?.Execute(null);

    private void OnExportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowExportCommand?.Execute(null);

    private void OnImportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowImportCommand?.Execute(null);

    private void OnPrintManagerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowPrintManagerCommand?.Execute(null);

    private void OnAlertBoxPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!QuickConfirmationEnabled) { return; }
        if (AlertCommand?.CanExecute(null) == true)
        {
            AlertCommand.Execute(null);
        }
    }

    private void ApplyKioskMode()
    {
        var minimize = this.FindControl<Button>("MinimizeButton");
        var maximize = this.FindControl<Button>("MaximizeButton");
        var close = this.FindControl<Button>("CloseButton");
        if (minimize is not null) { minimize.IsVisible = !IsKioskMode; }
        if (maximize is not null) { maximize.IsVisible = !IsKioskMode; }
        if (close is not null) { close.Content = IsKioskMode ? "↺" : "✕"; }
    }

    private void OnDragAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender)) { return; }
        if (VisualRoot is Window window && e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
        {
            window.BeginMoveDrag(e);
        }
    }

    private void OnMinimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (VisualRoot is Window w) { w.WindowState = WindowState.Minimized; }
    }

    private void OnMaximize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (VisualRoot is Window w)
        {
            w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (VisualRoot is Window w) { w.Close(); }
    }
}
