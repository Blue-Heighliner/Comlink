namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>
/// ViewModel representing a single row in a connection status display: one configured peer connection's
/// live status, used both for each row of the Server mode connections table and for the single row shown
/// at the bottom of the window in Client mode.
/// </summary>
public sealed partial class ConnectionRowViewModel : ObservableObject
{
    /// <summary>Initializes a new connection row for the given remote user name.</summary>
    /// <param name="userName">The remote user name this connection is (or was) established with.</param>
    /// <param name="setClosed">Closes (<see langword="true"/>) or reopens (<see langword="false"/>) this connection; when <see langword="null"/> the row's commands do nothing.</param>
    /// <param name="refresh">Drops and re-forms this connection; when <see langword="null"/> the row's refresh command does nothing.</param>
    public ConnectionRowViewModel(string userName, Action<bool>? setClosed = null, Action? refresh = null)
    {
        UserName = userName;
        this.setClosed = setClosed;
        this.refresh = refresh;
    }

    private readonly Action<bool>? setClosed;
    private readonly Action? refresh;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColorHex))]
    private bool isConnected;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColorHex))]
    [NotifyPropertyChangedFor(nameof(ToggleClosedText))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool isClosed;
    [ObservableProperty] private DateTime? lastConnectedAt;
    [ObservableProperty] private DateTime? lastDisconnectedAt;

    /// <summary>Gets the remote user name this connection is (or was) established with.</summary>
    public string UserName { get; }

    /// <summary>Gets the status text: <c>"CLOSED"</c> while the user has closed the connection, otherwise <c>"UP"</c> while connected and <c>"DN"</c> when not.</summary>
    public string StatusText => IsClosed ? "CLOSED" : IsConnected ? "UP" : "DN";

    /// <summary>Gets the row color: grey while closed, otherwise green while connected and red when not.</summary>
    public string StatusColorHex => IsClosed ? "#ABB2BF" : IsConnected ? "#98C379" : "#E06C75";

    /// <summary>Gets the context menu text for <see cref="ToggleClosedCommand"/>: <c>"Open"</c> while closed, <c>"Close"</c> otherwise.</summary>
    public string ToggleClosedText => IsClosed ? "Open" : "Close";

    /// <summary>Gets the formatted last-connected timestamp, or an em dash if it has never connected.</summary>
    public string LastConnectedText => FormatTimestamp(LastConnectedAt);

    /// <summary>Gets the formatted last-disconnected timestamp, or an em dash if it has never disconnected.</summary>
    public string LastDisconnectedText => FormatTimestamp(LastDisconnectedAt);

    [RelayCommand]
    private void ToggleClosed() => setClosed?.Invoke(!IsClosed);

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private void Refresh()
    {
        if (CanRefresh()) { refresh?.Invoke(); }
    }

    private bool CanRefresh() => !IsClosed;

    partial void OnLastConnectedAtChanged(DateTime? value) => OnPropertyChanged(nameof(LastConnectedText));
    partial void OnLastDisconnectedAtChanged(DateTime? value) => OnPropertyChanged(nameof(LastDisconnectedText));

    private static string FormatTimestamp(DateTime? value)
        => value is { } timestamp ? timestamp.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant() : "—";
}
