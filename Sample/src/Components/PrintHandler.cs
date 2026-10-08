namespace BlueHeighliner.Comlink.Sample;

/// <summary>Prints an alert message twice and every other received message once.</summary>
public sealed class PrintHandler : IPrintHandler<MessagePriority, MessageLevel, MessageAspect>
{
    /// <inheritdoc />
    public int GetPrintCount(Message message) => message.IsAlert ? 2 : 1;
}
