namespace BlueHeighliner.Comlink;

/// <summary>
/// What a retrieval request asks the server a message is stored on for. It is read from
/// and handed to the host's retrieval handler (see <see cref="IRetrievalHandler{TFrame, TPriority}"/>). A stored message
/// fits when it satisfies every criterion that is set; each list criterion is satisfied by matching any one of its
/// entries, compared case-insensitively and exactly.
/// </summary>
internal sealed record RetrievalCriteria
{
    /// <summary>Inclusive lower bound on the message's original sent time, or <see langword="null"/> for no lower bound.</summary>
    public DateTime? From { get; init; }
    /// <summary>Inclusive upper bound on the message's original sent time, or <see langword="null"/> for no upper bound.</summary>
    public DateTime? To { get; init; }
    /// <summary>User names of the message's sender; empty for any sender.</summary>
    public List<string> Authors { get; init; } = [];
    /// <summary>User names the message is addressed to (any address type, unexpanded); empty for any addressee.</summary>
    public List<string> Destinations { get; init; } = [];
    /// <summary>Message identifiers; empty for any message.</summary>
    public List<string> Ids { get; init; } = [];
}
