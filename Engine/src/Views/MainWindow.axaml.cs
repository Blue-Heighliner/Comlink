namespace BlueHeighliner.Comlink.Engine.Views;

/// <summary>The application's main window, wired to <see cref="MainViewModel"/> and maximized on load.</summary>
[ExcludeFromCodeCoverage]
public partial class MainWindow : Window
{
    /// <summary>Initializes the window, sets the data context, and starts async initialization.</summary>
    public MainWindow(IMainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _ = viewModel.Initialize();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        WindowState = WindowState.Maximized;
    }
}
