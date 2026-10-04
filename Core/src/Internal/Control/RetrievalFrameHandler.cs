namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's <see cref="IRetrievalHandler{TFrame, TPriority}"/>, working on frames as <see cref="object"/>.</summary>
internal interface IRetrievalFrameHandler
{
    /// <summary>Gets the priority level retrieval requests are sent with.</summary>
    Enum Priority { get; }
    /// <summary>Returns whether <paramref name="frame"/> is a retrieval request.</summary>
    bool IsValid(object frame);
    /// <summary>Creates a retrieval request frame asking for <paramref name="context"/>.</summary>
    object Create(RetrievalCreateContext context);
    /// <summary>Gets the user <paramref name="frame"/> is for.</summary>
    string GetDestination(object frame);
    /// <summary>Gets the sender of <paramref name="frame"/>.</summary>
    string GetSender(object frame);
    /// <summary>Sets the sender of <paramref name="frame"/>.</summary>
    void SetSender(object frame, string sender);
    /// <summary>Gets the earliest sent time <paramref name="frame"/> asks for.</summary>
    DateTime? GetFrom(object frame);
    /// <summary>Gets the latest sent time <paramref name="frame"/> asks for.</summary>
    DateTime? GetTo(object frame);
    /// <summary>Gets the sender user names <paramref name="frame"/> asks for.</summary>
    IReadOnlyList<string> GetAuthors(object frame);
    /// <summary>Gets the destination user names <paramref name="frame"/> asks for.</summary>
    IReadOnlyList<string> GetDestinations(object frame);
    /// <summary>Gets the message identifiers <paramref name="frame"/> asks for.</summary>
    IReadOnlyList<string> GetIds(object frame);
}

/// <summary>Adapts a typed <see cref="IRetrievalHandler{TFrame, TPriority}"/> to <see cref="IRetrievalFrameHandler"/>.</summary>
internal sealed class RetrievalFrameHandler<TFrame, TPriority>(IRetrievalHandler<TFrame, TPriority> handler) : IRetrievalFrameHandler where TFrame : class where TPriority : struct, Enum
{
    /// <inheritdoc />
    public Enum Priority => handler.Priority;

    /// <inheritdoc />
    public bool IsValid(object frame) => handler.IsValid((TFrame)frame);

    /// <inheritdoc />
    public object Create(RetrievalCreateContext context) => handler.Create(context);

    /// <inheritdoc />
    public string GetDestination(object frame) => handler.GetDestination((TFrame)frame);

    /// <inheritdoc />
    public string GetSender(object frame) => handler.GetSender((TFrame)frame);

    /// <inheritdoc />
    public void SetSender(object frame, string sender) => handler.SetSender((TFrame)frame, sender);

    /// <inheritdoc />
    public DateTime? GetFrom(object frame) => handler.GetFrom((TFrame)frame);

    /// <inheritdoc />
    public DateTime? GetTo(object frame) => handler.GetTo((TFrame)frame);

    /// <inheritdoc />
    public IReadOnlyList<string> GetAuthors(object frame) => handler.GetAuthors((TFrame)frame);

    /// <inheritdoc />
    public IReadOnlyList<string> GetDestinations(object frame) => handler.GetDestinations((TFrame)frame);

    /// <inheritdoc />
    public IReadOnlyList<string> GetIds(object frame) => handler.GetIds((TFrame)frame);
}
