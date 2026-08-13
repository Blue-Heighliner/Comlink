namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Decides how many times each received message should be automatically printed.</summary>
public interface IPrintReceivedRule
{
    /// <summary>
    /// Returns how many times <paramref name="message"/> should be automatically added to the print queue
    /// when it arrives — <c>0</c> to not print it, <c>1</c> to print it once, <c>2</c> to print two copies, and
    /// so on. Only consulted while the print manager's "print received" toggle is enabled; see
    /// <see cref="IPrintReceivedDefaultProvider"/>.
    /// </summary>
    /// <param name="message">The received message, in the host's own <see cref="IMessageFormat.MessageType"/>.</param>
    int GetPrintCount(object message);
}

/// <summary>Default <see cref="IPrintReceivedRule"/> that prints every received message exactly once.</summary>
internal sealed class PrintReceivedRule : IPrintReceivedRule
{
    /// <inheritdoc />
    public int GetPrintCount(object message) => 1;
}
