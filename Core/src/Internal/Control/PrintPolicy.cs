namespace BlueHeighliner.Comlink;

/// <summary>The engine's view of the host's print handler, with messages as the engine holds them.</summary>
internal interface IPrintPolicy
{
    /// <summary>Gets whether the print manager's "print received" toggle starts enabled.</summary>
    bool PrintReceivedByDefault { get; }

    /// <summary>Gets how many copies of the received message <paramref name="message"/> are printed.</summary>
    /// <param name="message">The received message.</param>
    int GetPrintCount(Message message);
}

/// <summary>Presents a host's <see cref="IPrintHandler{TPriority, TLevel, TAspect}"/> as an <see cref="IPrintPolicy"/>.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
/// <param name="handler">The host's handler.</param>
internal sealed class PrintPolicy<TPriority, TLevel, TAspect>(IPrintHandler<TPriority, TLevel, TAspect> handler) : IPrintPolicy where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public bool PrintReceivedByDefault => handler.PrintReceivedByDefault;

    /// <inheritdoc />
    public int GetPrintCount(Message message) => handler.GetPrintCount(message.ToTyped<TPriority, TLevel, TAspect>());
}
