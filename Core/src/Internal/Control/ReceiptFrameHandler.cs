namespace BlueHeighliner.Comlink.Control;

/// <summary>The engine's untyped view of the host's read or receive receipt handler, working on frames as <see cref="object"/>.</summary>
internal interface IReceiptFrameHandler
{
    /// <summary>Returns whether <paramref name="frame"/> is this kind of receipt.</summary>
    bool IsValid(object frame);
    /// <summary>Creates a receipt frame for <paramref name="context"/>.</summary>
    object Create(ReceiptCreateContext context);
    /// <summary>Gets the identifier of the message <paramref name="frame"/> is a receipt for.</summary>
    string GetMessageId(object frame);
}

/// <summary>Adapts a typed read or receive receipt handler to <see cref="IReceiptFrameHandler"/>, given the handler's own methods.</summary>
internal sealed class ReceiptFrameHandler<TFrame>(Func<TFrame, bool> isValid, Func<ReceiptCreateContext, TFrame> create, Func<TFrame, string> getMessageId) : IReceiptFrameHandler where TFrame : class
{
    /// <inheritdoc />
    public bool IsValid(object frame) => isValid((TFrame)frame);

    /// <inheritdoc />
    public object Create(ReceiptCreateContext context) => create(context);

    /// <inheritdoc />
    public string GetMessageId(object frame) => getMessageId((TFrame)frame);
}
