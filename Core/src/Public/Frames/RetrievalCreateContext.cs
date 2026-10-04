namespace BlueHeighliner.Comlink;

/// <summary>The criteria the engine hands to <see cref="IRetrievalHandler{TFrame, TPriority}.Create"/> to build a retrieval request frame. A criterion left empty or null does not narrow the request.</summary>
public sealed record RetrievalCreateContext
{
    /// <summary>Gets the earliest sent time to retrieve, or <see langword="null"/> for no lower bound.</summary>
    public DateTime? From { get; init; }

    /// <summary>Gets the latest sent time to retrieve, or <see langword="null"/> for no upper bound.</summary>
    public DateTime? To { get; init; }

    /// <summary>Gets the sender user names to retrieve messages from.</summary>
    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>Gets the destination user names to retrieve messages sent to.</summary>
    public IReadOnlyList<string> Destinations { get; init; } = [];

    /// <summary>Gets the message identifiers to retrieve.</summary>
    public IReadOnlyList<string> Ids { get; init; } = [];

    /// <summary>Gets the server the messages are stored on, which the request is for and which the handler stores as the request's destination.</summary>
    public required string Server { get; init; }
}
