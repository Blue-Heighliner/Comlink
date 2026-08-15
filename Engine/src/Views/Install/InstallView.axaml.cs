namespace BlueHeighliner.Comlink.Engine.Views.Install;

/// <summary>User control for the initial user installation screen.</summary>
[ExcludeFromCodeCoverage]
public partial class InstallView : UserControl
{
    private static void OnUserCodeTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not null)
        {
            e.Text = e.Text.ToUpperInvariant();
        }
    }

    /// <summary>Initializes the control, loads the AXAML layout, and registers the user code text input handler.</summary>
    public InstallView()
    {
        InitializeComponent();
        UserCodeInput.AddHandler(
            InputElement.TextInputEvent,
            OnUserCodeTextInput,
            RoutingStrategies.Tunnel);
    }
}
