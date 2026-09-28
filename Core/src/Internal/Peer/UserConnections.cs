namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Which established connections lead to which users. Connections are identified as they form (see
/// <see cref="IdentifyingPeerTransport"/>), so this is how a peer service finds the way to a user: not through anything
/// configured for that user, but through whichever connection is currently identified as them.
/// </summary>
internal interface IUserConnections
{
    /// <summary>Records <paramref name="connection"/> under the user it is identified as.</summary>
    /// <param name="connection">An established connection whose <see cref="PeerConnection.User"/> is set.</param>
    void Add(PeerConnection connection);

    /// <summary>Forgets <paramref name="connection"/>.</summary>
    /// <returns>The name of the user it was recorded under, or <see langword="null"/> if it was not recorded.</returns>
    string? Remove(PeerConnection connection);

    /// <summary>Whether <paramref name="connection"/> is recorded.</summary>
    bool Contains(PeerConnection connection);

    /// <summary>Returns the newest connection identified as <paramref name="userName"/>, or <see langword="null"/> if there is none.</summary>
    PeerConnection? Get(string userName);

    /// <summary>Returns every connection identified as <paramref name="userName"/>.</summary>
    IReadOnlyList<PeerConnection> GetAll(string userName);

    /// <summary>Whether any connection is identified as <paramref name="userName"/>.</summary>
    bool Has(string userName);
}

/// <inheritdoc />
internal sealed class UserConnections : IUserConnections
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, List<PeerConnection>> byUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<PeerConnection, string> byConnection = [];

    /// <inheritdoc />
    public void Add(PeerConnection connection)
    {
        string name = connection.User?.Name ?? throw new ArgumentException("The connection has not been identified", nameof(connection));
        lock (gate)
        {
            if (byConnection.ContainsKey(connection)) { return; }

            byConnection[connection] = name;
            if (!byUser.TryGetValue(name, out List<PeerConnection>? connections))
            {
                connections = [];
                byUser[name] = connections;
            }

            connections.Add(connection);
        }
    }

    /// <inheritdoc />
    public string? Remove(PeerConnection connection)
    {
        lock (gate)
        {
            if (!byConnection.Remove(connection, out string? name)) { return null; }

            if (byUser.TryGetValue(name, out List<PeerConnection>? connections))
            {
                connections.Remove(connection);
                if (connections.Count == 0) { byUser.Remove(name); }
            }

            return name;
        }
    }

    /// <inheritdoc />
    public bool Contains(PeerConnection connection)
    {
        lock (gate) { return byConnection.ContainsKey(connection); }
    }

    /// <inheritdoc />
    public PeerConnection? Get(string userName)
    {
        lock (gate) { return byUser.TryGetValue(userName, out List<PeerConnection>? connections) ? connections[^1] : null; }
    }

    /// <inheritdoc />
    public IReadOnlyList<PeerConnection> GetAll(string userName)
    {
        lock (gate) { return byUser.TryGetValue(userName, out List<PeerConnection>? connections) ? [.. connections] : []; }
    }

    /// <inheritdoc />
    public bool Has(string userName)
    {
        lock (gate) { return byUser.ContainsKey(userName); }
    }
}
