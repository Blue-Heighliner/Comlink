namespace BlueHeighliner.Comlink.Engine.Views.Install;

/// <summary>User control for the initial site installation screen.</summary>
[ExcludeFromCodeCoverage]
public partial class InstallView : UserControl
{
    /// <summary>Initializes the control, loads the AXAML layout, and registers the site code text input handler.</summary>
    public InstallView()
    {
        InitializeComponent();
        SiteCodeInput.AddHandler(
            InputElement.TextInputEvent,
            OnSiteCodeTextInput,
            RoutingStrategies.Tunnel);
    }

    private static void OnSiteCodeTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not null)
            e.Text = e.Text.ToUpperInvariant();
    }
}
