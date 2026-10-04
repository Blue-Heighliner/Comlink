namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's read or receive receipt handler, working on frames as <see cref="object"/>.</summary>
internal interface IReceiptFrameHandler
{
    /// <summary>Gets the priority level this kind of receipt is sent with.</summary>
    Enum Priority { get; }
    /// <summary>Returns whether <paramref name="frame"/> is this kind of receipt.</summary>
    bool IsValid(object frame);
    /// <summary>Creates a receipt frame for <paramref name="context"/>.</summary>
    object Create(ReceiptCreateContext context);
    /// <summary>Gets the user <paramref name="frame"/> is for.</summary>
    string GetDestination(object frame);
    /// <summary>Gets the sender of <paramref name="frame"/>.</summary>
    string GetSender(object frame);
    /// <summary>Sets the sender of <paramref name="frame"/>.</summary>
    void SetSender(object frame, string sender);
    /// <summary>Gets the identifier of the message <paramref name="frame"/> is a receipt for.</summary>
    string GetMessageId(object frame);
}

/// <summary>Adapts a typed read or receive receipt handler to <see cref="IReceiptFrameHandler"/>, given the handler's own methods.</summary>
internal sealed class ReceiptFrameHandler<TFrame>(Enum priority, Func<TFrame, bool> isValid, Func<ReceiptCreateContext, TFrame> create, Func<TFrame, string> getMessageId, Func<TFrame, string> getSender, Action<TFrame, string> setSender, Func<TFrame, string> getDestination) : IReceiptFrameHandler where TFrame : class
{
    /// <inheritdoc />
    public Enum Priority => priority;

    /// <inheritdoc />
    public bool IsValid(object frame) => isValid((TFrame)frame);

    /// <inheritdoc />
    public object Create(ReceiptCreateContext context) => create(context);

    /// <inheritdoc />
    public string GetMessageId(object frame) => getMessageId((TFrame)frame);

    /// <inheritdoc />
    public string GetDestination(object frame) => getDestination((TFrame)frame);

    /// <inheritdoc />
    public string GetSender(object frame) => getSender((TFrame)frame);

    /// <inheritdoc />
    public void SetSender(object frame, string sender) => setSender((TFrame)frame, sender);
}
