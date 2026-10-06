namespace BlueHeighliner.Comlink;

/// <summary>Controls how the print manager behaves. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.Prints{THandler}"/>. Every member is optional.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IPrintHandler<TFrame> where TFrame : class, new()
{
    /// <summary>Gets a value indicating whether the print manager's "print received" toggle starts enabled, adding every received message to the print queue from startup. Defaults to <see langword="false"/>. The user can still toggle it at any time, and the current user's entry in the network configuration file can override it.</summary>
    bool PrintReceivedByDefault => false;

    /// <summary>Gets how many copies of the received message <paramref name="frame"/> are printed while the print manager's "print received" toggle is on: <c>0</c> to not print it, <c>1</c> (the default) to print it once, and so on.</summary>
    /// <param name="frame">The received message.</param>
    int GetPrintCount(TFrame frame) => 1;
}
