namespace BlueHeighliner.Comlink.Views;

/// <summary>The help window: a tabbed guide to using the application, driven by <see cref="IHelpViewModel"/>.</summary>
[ExcludeFromCodeCoverage]
public partial class HelpWindow : Window
{
    /// <summary>Initializes the window and lets Escape close it.</summary>
    public HelpWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); }
        };
    }
}
