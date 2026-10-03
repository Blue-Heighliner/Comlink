namespace BlueHeighliner.Comlink.Control;

/// <summary>The engine's untyped view of the host's <see cref="IRetrievalHandler{TFrame}"/>, working on frames as <see cref="object"/>.</summary>
internal interface IRetrievalFrameHandler
{
    /// <summary>Gets the name of the priority retrieval requests are sent with.</summary>
    string Priority { get; }
    /// <summary>Returns whether <paramref name="frame"/> is a retrieval request.</summary>
    bool IsValid(object frame);
    /// <summary>Creates a retrieval request frame asking for <paramref name="context"/>.</summary>
    object Create(RetrievalCreateContext context);
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

/// <summary>Adapts a typed <see cref="IRetrievalHandler{TFrame}"/> to <see cref="IRetrievalFrameHandler"/>.</summary>
internal sealed class RetrievalFrameHandler<TFrame>(IRetrievalHandler<TFrame> handler) : IRetrievalFrameHandler where TFrame : class
{
    /// <inheritdoc />
    public string Priority => handler.Priority;

    /// <inheritdoc />
    public bool IsValid(object frame) => handler.IsValid((TFrame)frame);

    /// <inheritdoc />
    public object Create(RetrievalCreateContext context) => handler.Create(context);

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
