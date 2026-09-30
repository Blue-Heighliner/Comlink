namespace BlueHeighliner.Comlink.Views.Controls;

/// <summary>
/// User control for the application title bar, providing window controls, user info, and action buttons —
/// the normal drafts/notes/export/import/print actions in Peer/Client mode (<see cref="IsServerMode"/> is
/// <see langword="false"/>), or the CONNECTIONS/ACTIVITY view-switching buttons in Server mode
/// (<see cref="IsServerMode"/> is <see langword="true"/>).
/// </summary>
[ExcludeFromCodeCoverage]
internal partial class TitleBar : UserControl
{
    /// <summary>Identifies the <see cref="UserName"/> styled property.</summary>
    public static readonly StyledProperty<string> UserNameProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(UserName), string.Empty);

    /// <summary>Identifies the <see cref="AppVersion"/> styled property.</summary>
    public static readonly StyledProperty<string> AppVersionProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(AppVersion), string.Empty);

    /// <summary>Identifies the <see cref="AppName"/> styled property.</summary>
    public static readonly StyledProperty<string> AppNameProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(AppName), string.Empty);

    /// <summary>Identifies the <see cref="Help"/> styled property.</summary>
    public static readonly StyledProperty<IHelpViewModel?> HelpProperty =
        AvaloniaProperty.Register<TitleBar, IHelpViewModel?>(nameof(Help));

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

    /// <summary>Identifies the <see cref="ShowRetrieveCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ShowRetrieveCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(ShowRetrieveCommand));

    /// <summary>Identifies the <see cref="CanRetrieve"/> styled property.</summary>
    public static readonly StyledProperty<bool> CanRetrieveProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(CanRetrieve));

    /// <summary>Identifies the <see cref="ShowAutoForwardCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ShowAutoForwardCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(ShowAutoForwardCommand));

    /// <summary>Identifies the <see cref="HasAutoForwardAccess"/> styled property.</summary>
    public static readonly StyledProperty<bool> HasAutoForwardAccessProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(HasAutoForwardAccess));

    /// <summary>Identifies the <see cref="RefreshCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> RefreshCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(RefreshCommand));

    /// <summary>Identifies the <see cref="ShowPrintManagerCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ShowPrintManagerCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(ShowPrintManagerCommand));

    /// <summary>Identifies the <see cref="IsInstallScreenVisible"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsInstallScreenVisibleProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsInstallScreenVisible));

    /// <summary>Identifies the <see cref="IsServerMode"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsServerModeProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsServerMode));

    /// <summary>Identifies the <see cref="ShowConnectionsCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ShowConnectionsCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(ShowConnectionsCommand));

    /// <summary>Identifies the <see cref="ShowActivityCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ShowActivityCommandProperty =
        AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(ShowActivityCommand));

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

    private HelpWindow? helpWindow;

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

    /// <summary>Gets or sets the application name shown in the info popup.</summary>
    public string AppName
    {
        get => GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    /// <summary>Gets or sets the help ViewModel the help button opens a <see cref="HelpWindow"/> for.</summary>
    public IHelpViewModel? Help
    {
        get => GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
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

    /// <summary>Gets or sets the command invoked when the user clicks the Retrieve button.</summary>
    public ICommand? ShowRetrieveCommand
    {
        get => GetValue(ShowRetrieveCommandProperty);
        set => SetValue(ShowRetrieveCommandProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the Retrieve button is shown.</summary>
    public bool CanRetrieve
    {
        get => GetValue(CanRetrieveProperty);
        set => SetValue(CanRetrieveProperty, value);
    }

    /// <summary>Gets or sets the command invoked when the user clicks the Auto Forward button.</summary>
    public ICommand? ShowAutoForwardCommand
    {
        get => GetValue(ShowAutoForwardCommandProperty);
        set => SetValue(ShowAutoForwardCommandProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the current user has access to at least one auto forward controller, showing the Auto Forward button.</summary>
    public bool HasAutoForwardAccess
    {
        get => GetValue(HasAutoForwardAccessProperty);
        set => SetValue(HasAutoForwardAccessProperty, value);
    }

    /// <summary>Gets or sets the command invoked by the user name label's right-click "Refresh" option, which re-reads the network configuration file.</summary>
    public ICommand? RefreshCommand
    {
        get => GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
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

    /// <summary>
    /// Gets or sets a value indicating whether this instance is running as a <see cref="UserRole.Server"/>,
    /// which hides the left-side action buttons and shows the CONNECTIONS/ACTIVITY view-switching buttons instead.
    /// </summary>
    public bool IsServerMode
    {
        get => GetValue(IsServerModeProperty);
        set => SetValue(IsServerModeProperty, value);
    }

    /// <summary>Gets or sets the command invoked when the user clicks the CONNECTIONS button (<see cref="IsServerMode"/> only).</summary>
    public ICommand? ShowConnectionsCommand
    {
        get => GetValue(ShowConnectionsCommandProperty);
        set => SetValue(ShowConnectionsCommandProperty, value);
    }

    /// <summary>Gets or sets the command invoked when the user clicks the ACTIVITY button (<see cref="IsServerMode"/> only).</summary>
    public ICommand? ShowActivityCommand
    {
        get => GetValue(ShowActivityCommandProperty);
        set => SetValue(ShowActivityCommandProperty, value);
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
        if (change.Property == IsInstallScreenVisibleProperty || change.Property == IsServerModeProperty)
        {
            UpdateActionButtonsVisibility();
        }
        if (change.Property == CanRetrieveProperty)
        {
            Button? retrieveButton = this.FindControl<Button>("RetrieveButton");
            if (retrieveButton is not null) { retrieveButton.IsVisible = CanRetrieve; }
            Border? retrieveLeadingSeparator = this.FindControl<Border>("RetrieveLeadingSeparator");
            if (retrieveLeadingSeparator is not null) { retrieveLeadingSeparator.IsVisible = CanRetrieve; }
        }
        if (change.Property == HasAutoForwardAccessProperty)
        {
            Button? autoForwardButton = this.FindControl<Button>("AutoForwardButton");
            if (autoForwardButton is not null) { autoForwardButton.IsVisible = HasAutoForwardAccess; }
            Border? autoForwardLeadingSeparator = this.FindControl<Border>("AutoForwardLeadingSeparator");
            if (autoForwardLeadingSeparator is not null) { autoForwardLeadingSeparator.IsVisible = HasAutoForwardAccess; }
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

    private void UpdateActionButtonsVisibility()
    {
        StackPanel? actionButtons = this.FindControl<StackPanel>("ActionButtonsPanel");
        if (actionButtons is not null) { actionButtons.IsVisible = !IsServerMode && !IsInstallScreenVisible; }
        StackPanel? serverViewButtons = this.FindControl<StackPanel>("ServerViewButtonsPanel");
        if (serverViewButtons is not null) { serverViewButtons.IsVisible = IsServerMode && !IsInstallScreenVisible; }
    }

    private void UpdateUserInfo()
    {
        TextBlock? userNameText = this.FindControl<TextBlock>("UserNameText");
        if (userNameText is not null) { userNameText.Text = UserName; }

        bool hasVersion = !string.IsNullOrEmpty(AppVersion);
        Border? separator = this.FindControl<Border>("UserInfoSeparator");
        if (separator is not null) { separator.IsVisible = hasVersion; }
        TextBlock? versionText = this.FindControl<TextBlock>("VersionText");
        if (versionText is not null)
        {
            versionText.IsVisible = hasVersion;
            versionText.Text = hasVersion ? $"v{AppVersion}" : string.Empty;
        }
    }

    private void OnInfoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.Control anchor) { return; }

        StackPanel content = new() { Spacing = 2, Margin = new Thickness(6), MinWidth = 160 };
        content.Children.Add(new TextBlock { Text = AppName, FontSize = 14, FontWeight = FontWeight.SemiBold });
        if (!string.IsNullOrEmpty(AppVersion))
        {
            content.Children.Add(new TextBlock { Text = $"Version {AppVersion}", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")) });
        }

        new Flyout { Content = content, Placement = PlacementMode.BottomEdgeAlignedRight }.ShowAt(anchor);
    }

    private void OnHelpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (helpWindow is not null)
        {
            helpWindow.Activate();
            return;
        }

        if (Help is null || VisualRoot is not Window owner) { return; }

        helpWindow = new HelpWindow { DataContext = Help };
        helpWindow.Closed += (_, _) => helpWindow = null;
        helpWindow.Show(owner);
    }

    private void OnDraftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CreateDraftCommand?.Execute(null);

    private void OnNoteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CreateNoteCommand?.Execute(null);

    private void OnExportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowExportCommand?.Execute(null);

    private void OnImportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowImportCommand?.Execute(null);

    private void OnRetrieveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowRetrieveCommand?.Execute(null);

    private void OnAutoForwardClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowAutoForwardCommand?.Execute(null);

    private void OnRefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => RefreshCommand?.Execute(null);

    private void OnPrintManagerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowPrintManagerCommand?.Execute(null);

    private void OnShowConnectionsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowConnectionsCommand?.Execute(null);

    private void OnShowActivityClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ShowActivityCommand?.Execute(null);

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
        Button? minimize = this.FindControl<Button>("MinimizeButton");
        Button? maximize = this.FindControl<Button>("MaximizeButton");
        Button? close = this.FindControl<Button>("CloseButton");
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
