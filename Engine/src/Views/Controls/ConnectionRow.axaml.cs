namespace BlueHeighliner.Comlink.Engine.Views.Controls;

/// <summary>
/// Displays one row of a <see cref="ViewModels.ConnectionRowViewModel"/>: user name, UP/DN status, and
/// last-connected/last-disconnected timestamps, with the row background colored by connection status.
/// Used both for each row of the Server mode connections table and for the single row pinned to the bottom
/// of the window in Client mode.
/// </summary>
[ExcludeFromCodeCoverage]
public partial class ConnectionRow : UserControl
{
    /// <summary>Initializes the control and loads its AXAML layout.</summary>
    public ConnectionRow() => InitializeComponent();
}
