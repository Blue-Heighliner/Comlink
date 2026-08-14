namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IPrintPolicy"/> that prints an alert message twice (so an operator has a spare copy
/// to route or post) and every other received message once; the starting "print received" state uses the
/// Engine default (disabled unless <c>config.json</c> overrides it, applied separately at the Engine level).
/// </summary>
public sealed class SamplePrintPolicy : DefaultPrintPolicy
{
    private readonly IMessageFormat _messageFormat;

    /// <summary>Initializes a new <see cref="SamplePrintPolicy"/> using the given message format to inspect the alert flag.</summary>
    /// <param name="messageFormat">Maps logical fields onto a message entity's stored message.</param>
    public SamplePrintPolicy(IMessageFormat messageFormat) => _messageFormat = messageFormat;

    /// <inheritdoc />
    public override int GetPrintCount(object message) => _messageFormat.GetIsAlert(message) ? 2 : 1;
}
