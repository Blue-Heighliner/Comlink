namespace BlueHeighliner.Comlink.Engine.Views;

/// <summary>The application's main window, wired to <see cref="MainViewModel"/> and maximized on load.</summary>
[ExcludeFromCodeCoverage]
public partial class MainWindow : Window
{
    private readonly IMainViewModel _viewModel;

    /// <summary>Initializes the window, sets the data context, and starts async initialization.</summary>
    public MainWindow(IMainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        // Tunnel priority ensures this observes Space/Enter before any focused control (e.g. a Button) acts on it.
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        _ = viewModel.Initialize();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Quick-confirms the latest pending alert on Space/Enter, unless focus is in a text input — see
    /// <see cref="IAlertViewModel.ConfirmLatestCommand"/> and <c>Docs/ViewModels.md</c>.
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter)) return;
        if (IsTextInputFocused()) return;

        IAsyncRelayCommand command = _viewModel.Alert.ConfirmLatestCommand;
        if (!command.CanExecute(null)) return;

        command.Execute(null);
        e.Handled = true;
    }

    private bool IsTextInputFocused()
    {
        IInputElement? focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox) return true;
        return focused is Visual visual && visual.FindAncestorOfType<TextEditor>() is not null;
    }
}
