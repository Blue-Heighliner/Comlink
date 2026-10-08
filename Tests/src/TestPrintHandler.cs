namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IPrintHandler{TPriority, TLevel, TAspect}"/> that starts the print manager printing every received message, twice when its tag is <c>TWICE</c>.</summary>
public sealed class TestPrintHandler : IPrintHandler<TestMessagePriority, TestLevel, TestAspect>
{
    /// <inheritdoc />
    public bool PrintReceivedByDefault => true;

    /// <inheritdoc />
    public int GetPrintCount(Message<TestMessagePriority, TestLevel, TestAspect> message) => message.Tag == "TWICE" ? 2 : 1;
}
