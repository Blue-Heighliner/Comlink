namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>
/// ViewModel interface for the connection status display: two tables — one for connections to other
/// servers, one for connections from own child clients — in <see cref="NodeRole.Server"/> mode (see
/// <c>ConnectionsTable</c>), or the single row tracking the connection to the configured server in
/// <see cref="NodeRole.Client"/> mode (see <c>ConnectionRow</c> pinned to the bottom of the window).
/// Registered as a DI singleton so it reflects live connection status regardless of whether it is
/// currently shown.
/// </summary>
public interface IConnectionStatusViewModel
{
    /// <summary>Gets a value indicating whether <see cref="ServerRows"/> has any rows — the server connections table is hidden while this is <see langword="false"/>.</summary>
    bool HasServerRows { get; }
    /// <summary>Gets a value indicating whether <see cref="ClientRows"/> has any rows — the client connections table is hidden while this is <see langword="false"/>.</summary>
    bool HasClientRows { get; }
    /// <summary>Gets the current connections-to-other-servers rows: one entry in <see cref="NodeRole.Client"/> mode (the connection to its server), one per other server in the cluster in <see cref="NodeRole.Server"/> mode.</summary>
    ObservableCollection<ConnectionRowViewModel> ServerRows { get; }
    /// <summary>Gets the current connections-from-child-clients rows — one per own child client in <see cref="NodeRole.Server"/> mode; always empty in <see cref="NodeRole.Client"/> mode.</summary>
    ObservableCollection<ConnectionRowViewModel> ClientRows { get; }
}

/// <inheritdoc cref="IConnectionStatusViewModel" />
internal sealed partial class ConnectionStatusViewModel : ObservableObject, IConnectionStatusViewModel
{
    /// <summary>Initializes a new <see cref="ConnectionStatusViewModel"/> and subscribes to live status updates.</summary>
    /// <param name="statusService">Source of live connection status; a no-op source in <see cref="NodeRole.Peer"/> mode.</param>
    public ConnectionStatusViewModel(IConnectionStatusService statusService)
    {
        this.statusService = statusService;
        statusService.StatusesChanged += Refresh;
        Refresh();
    }

    private readonly IConnectionStatusService statusService;

    /// <inheritdoc />
    public bool HasServerRows => ServerRows.Count > 0;
    /// <inheritdoc />
    public bool HasClientRows => ClientRows.Count > 0;
    /// <inheritdoc />
    public ObservableCollection<ConnectionRowViewModel> ServerRows { get; } = [];
    /// <inheritdoc />
    public ObservableCollection<ConnectionRowViewModel> ClientRows { get; } = [];

    private void Refresh()
    {
        // StatusesChanged can fire from a background connection thread (e.g. a Server's per-remote-server
        // retry loop, or its inbound child-connect handler) — ServerRows/ClientRows are bound to live
        // Avalonia ItemsControls, so they must only ever be mutated on the UI thread.
        // Dispatcher.UIThread.CheckAccess() is also true with no Avalonia dispatcher loop running at all
        // (e.g. in a unit test), so this still refreshes synchronously there instead of posting to a queue
        // nothing will ever pump.
        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshRows();
        }
        else
        {
            Dispatcher.UIThread.Post(RefreshRows);
        }
    }

    private void RefreshRows()
    {
        ServerRows.Clear();
        ClientRows.Clear();
        foreach (PeerConnectionStatus status in statusService.GetStatuses())
        {
            ConnectionRowViewModel row = new(status.UserName)
            {
                IsConnected = status.IsConnected,
                LastConnectedAt = status.LastConnectedAt,
                LastDisconnectedAt = status.LastDisconnectedAt
            };
            (status.Kind == PeerConnectionKind.Server ? ServerRows : ClientRows).Add(row);
        }
        OnPropertyChanged(nameof(HasServerRows));
        OnPropertyChanged(nameof(HasClientRows));
    }
}
