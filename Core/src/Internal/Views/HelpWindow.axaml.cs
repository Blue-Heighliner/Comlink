namespace BlueHeighliner.Comlink;

/// <summary>The help window: a tabbed guide to using the application, driven by <see cref="IHelpViewModel"/>.</summary>
[ExcludeFromCodeCoverage]
internal partial class HelpWindow : Window
{
    /// <summary>Initializes the window and lets Escape close it.</summary>
    public HelpWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape)
            {
                Close();
            }
        };
    }
}
