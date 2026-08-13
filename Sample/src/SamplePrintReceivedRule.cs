namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IPrintReceivedRule"/> that prints an alert message twice (so an operator has a spare
/// copy to route or post) and every other received message once.
/// </summary>
public sealed class SamplePrintReceivedRule : IPrintReceivedRule
{
    private readonly IMessageFormat _messageFormat;

    /// <summary>Initializes a new <see cref="SamplePrintReceivedRule"/> using the given message format to inspect the alert flag.</summary>
    /// <param name="messageFormat">Maps logical fields onto a message entity's stored message.</param>
    public SamplePrintReceivedRule(IMessageFormat messageFormat) => _messageFormat = messageFormat;

    /// <inheritdoc />
    public int GetPrintCount(object message) => _messageFormat.GetIsAlert(message) ? 2 : 1;
}
