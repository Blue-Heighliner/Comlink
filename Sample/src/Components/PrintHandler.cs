namespace BlueHeighliner.Comlink.Sample;

/// <summary>Prints an alert message twice and every other received message once.</summary>
public sealed class PrintHandler : IPrintHandler<Frame>
{
    /// <inheritdoc />
    public int GetPrintCount(Frame frame) => string.Equals(frame.Category, "ALERT", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
}
