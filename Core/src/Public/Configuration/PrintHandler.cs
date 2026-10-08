namespace BlueHeighliner.Comlink;

/// <summary>Controls how the print manager behaves. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Prints{THandler}"/>. Every member is optional.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public interface IPrintHandler<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Gets a value indicating whether the print manager's "print received" toggle starts enabled, adding every received message to the print queue from startup. Defaults to <see langword="false"/>. The user can still toggle it at any time.</summary>
    bool PrintReceivedByDefault => false;

    /// <summary>Gets how many copies of the received message <paramref name="message"/> are printed while the print manager's "print received" toggle is on: <c>0</c> to not print it, <c>1</c> (the default) to print it once, and so on.</summary>
    /// <param name="message">The received message.</param>
    int GetPrintCount(Message<TPriority, TLevel, TAspect> message) => 1;
}
