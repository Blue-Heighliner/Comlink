namespace BlueHeighliner.Comlink;

/// <summary>The application's main window, wired to <see cref="MainViewModel"/> and maximized on load.</summary>
[ExcludeFromCodeCoverage]
internal partial class MainWindow : Window
{
    /// <summary>Initializes the window, sets the data context, and starts async initialization.</summary>
    public MainWindow(IMainViewModel viewModel)
    {
        InitializeComponent();
        AddResizeGrips();
        DataContext = viewModel;
        this.viewModel = viewModel;
        // Tunnel priority ensures this observes Space/Enter before any focused control (e.g. a Button) acts on it.
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        _ = viewModel.Initialize();
    }

    private readonly IMainViewModel viewModel;
    private readonly List<Border> grips = [];
    private readonly Border frame = new() { BorderBrush = new SolidColorBrush(Color.Parse("#5A5A60")) };

    // The window has no system border, so it gets a thin one drawn here, and can only be resized by dragging the strips along its edges and corners.
    private void AddResizeGrips()
    {
        Control? content = Content as Control;
        Content = null;
        Grid host = new();
        if (content is not null) { host.Children.Add(content); }

        (WindowEdge Edge, HorizontalAlignment Horizontal, VerticalAlignment Vertical, double Width, double Height, StandardCursorType Cursor)[] layout =
        [
            (WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, double.NaN, 5, StandardCursorType.TopSide),
            (WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, 5, StandardCursorType.BottomSide),
            (WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, 5, double.NaN, StandardCursorType.LeftSide),
            (WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, 5, double.NaN, StandardCursorType.RightSide),
            (WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, 10, 10, StandardCursorType.TopLeftCorner),
            (WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, 10, 10, StandardCursorType.TopRightCorner),
            (WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, 10, 10, StandardCursorType.BottomLeftCorner),
            (WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, 10, 10, StandardCursorType.BottomRightCorner)
        ];

        foreach ((WindowEdge edge, HorizontalAlignment horizontal, VerticalAlignment vertical, double width, double height, StandardCursorType cursor) in layout)
        {
            Border grip = new()
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Width = width,
                Height = height,
                Cursor = new Cursor(cursor)
            };
            grip.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { BeginResizeDrag(edge, e); }
            };
            grips.Add(grip);
            host.Children.Add(grip);
        }

        frame.Child = host;
        Content = frame;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) { ApplyWindowState(); }
        };
        ApplyWindowState();
    }

    private void ApplyWindowState()
    {
        bool isNormal = WindowState == WindowState.Normal;
        frame.BorderThickness = new Thickness(isNormal ? 1 : 0);
        foreach (Border grip in grips) { grip.IsVisible = isNormal; }
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Opens the oldest unread alert on one of the message handler's alert quick read keys, unless focus is in a text input — see
    /// <see cref="IAlertViewModel.OpenOldestCommand"/> and <c>Docs/Components/ViewModels.md</c>.
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!viewModel.Alert.QuickReadKeys.Any(name => Enum.TryParse(name, ignoreCase: true, out Key key) && key == e.Key)) { return; }
        if (IsTextInputFocused()) { return; }

        IAsyncRelayCommand command = viewModel.Alert.OpenOldestCommand;
        if (!command.CanExecute(null)) { return; }

        command.Execute(null);
        e.Handled = true;
    }

    private bool IsTextInputFocused()
    {
        IInputElement? focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox) { return true; }
        return focused is Visual visual && visual.FindAncestorOfType<TextEditor>() is not null;
    }
}
