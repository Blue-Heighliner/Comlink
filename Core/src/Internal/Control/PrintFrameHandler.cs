namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's <see cref="IPrintHandler{TFrame}"/>.</summary>
internal interface IPrintFrameHandler
{
    /// <summary>Gets a value indicating whether the print manager's "print received" toggle starts enabled.</summary>
    bool PrintReceivedByDefault { get; }
    /// <summary>Gets how many copies of <paramref name="frame"/> are printed when received.</summary>
    /// <param name="frame">The received message.</param>
    int GetPrintCount(object frame);
}

/// <summary>Adapts a typed <see cref="IPrintHandler{TFrame}"/> to <see cref="IPrintFrameHandler"/>.</summary>
internal sealed class PrintFrameHandler<TFrame>(IPrintHandler<TFrame> handler) : IPrintFrameHandler where TFrame : class, new()
{
    /// <inheritdoc />
    public bool PrintReceivedByDefault => handler.PrintReceivedByDefault;

    /// <inheritdoc />
    public int GetPrintCount(object frame) => handler.GetPrintCount((TFrame)frame);
}
