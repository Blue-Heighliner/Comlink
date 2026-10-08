namespace BlueHeighliner.Comlink;

/// <summary>
/// What a user asks a server for when they submit a retrieval in the GUI: the stored messages fitting every criterion that is set, each list criterion satisfied by matching any one of its entries,
/// compared case-insensitively and exactly. It is handed to <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}.OnRetrieval"/>, and given back to <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.FindStoredMessages"/> by the server that answers.
/// </summary>
public sealed record RetrievalCriteria
{
    /// <summary>Gets the inclusive lower bound on the message's original sent time, or <see langword="null"/> for no lower bound.</summary>
    public DateTime? From { get; init; }

    /// <summary>Gets the inclusive upper bound on the message's original sent time, or <see langword="null"/> for no upper bound.</summary>
    public DateTime? To { get; init; }

    /// <summary>Gets the user names of the message's sender; empty for any sender.</summary>
    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>Gets the user names the message is addressed to (any address type, unexpanded); empty for any addressee.</summary>
    public IReadOnlyList<string> Destinations { get; init; } = [];

    /// <summary>Gets the message identifiers; empty for any message.</summary>
    public IReadOnlyList<string> Ids { get; init; } = [];
}
