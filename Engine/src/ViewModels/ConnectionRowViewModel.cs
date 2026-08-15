namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>
/// ViewModel representing a single row in a connection status display: one configured peer connection's
/// live status, used both for each row of the Server mode connections table and for the single row shown
/// at the bottom of the window in Client mode.
/// </summary>
public sealed partial class ConnectionRowViewModel : ObservableObject
{
    /// <summary>Initializes a new connection row for the given remote user name.</summary>
    /// <param name="userName">The remote user name this connection is (or was) established with.</param>
    public ConnectionRowViewModel(string userName) => UserName = userName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColorHex))]
    private bool isConnected;
    [ObservableProperty] private DateTime? lastConnectedAt;
    [ObservableProperty] private DateTime? lastDisconnectedAt;

    /// <summary>Gets the remote user name this connection is (or was) established with.</summary>
    public string UserName { get; }

    /// <summary>Gets the status text: <c>"UP"</c> while connected, <c>"DN"</c> otherwise.</summary>
    public string StatusText => IsConnected ? "UP" : "DN";

    /// <summary>Gets the row color: green while connected, red otherwise.</summary>
    public string StatusColorHex => IsConnected ? "#98C379" : "#E06C75";

    /// <summary>Gets the formatted last-connected timestamp, or an em dash if it has never connected.</summary>
    public string LastConnectedText => FormatTimestamp(LastConnectedAt);

    /// <summary>Gets the formatted last-disconnected timestamp, or an em dash if it has never disconnected.</summary>
    public string LastDisconnectedText => FormatTimestamp(LastDisconnectedAt);

    partial void OnLastConnectedAtChanged(DateTime? value) => OnPropertyChanged(nameof(LastConnectedText));
    partial void OnLastDisconnectedAtChanged(DateTime? value) => OnPropertyChanged(nameof(LastDisconnectedText));

    private static string FormatTimestamp(DateTime? value)
        => value is { } timestamp ? timestamp.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant() : "—";
}
