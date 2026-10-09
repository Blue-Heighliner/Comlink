namespace BlueHeighliner.Comlink;

/// <summary>
/// Whether the network indicator in the top bar of a client shows online or offline. Only the host's frame handler sets it (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.SetNetworkIndicator"/>):
/// the engine does not follow any connection itself.
/// </summary>
internal interface INetworkIndicator
{
    /// <summary>Raised with the new state whenever it changes.</summary>
    event Action<bool>? Changed;

    /// <summary>Gets a value indicating whether the indicator shows online. It starts offline.</summary>
    bool IsOnline { get; }

    /// <summary>Sets the indicator online or offline.</summary>
    /// <param name="isOnline"><see langword="true"/> for online.</param>
    void Set(bool isOnline);
}

/// <inheritdoc cref="INetworkIndicator" />
internal sealed class NetworkIndicator(ILoggerFactory loggerFactory) : INetworkIndicator
{
    private readonly ILogger logger = loggerFactory.CreateLogger(LogCategories.App);
    private readonly Lock gate = new();
    private bool isOnline;

    /// <inheritdoc />
    public event Action<bool>? Changed;

    /// <inheritdoc />
    public bool IsOnline
    {
        get
        {
            lock (gate) { return isOnline; }
        }
    }

    /// <inheritdoc />
    public void Set(bool isOnline)
    {
        lock (gate)
        {
            if (this.isOnline == isOnline)
            {
                return;
            }

            this.isOnline = isOnline;
        }

        logger.Record(LogEvents.NetworkStatusChanged, "Network {Status}", isOnline ? "online" : "offline");
        Changed?.Invoke(isOnline);
    }
}
