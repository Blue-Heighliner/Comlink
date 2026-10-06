namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IPrintHandler{TFrame}"/> that starts the print manager printing every received message, as many times as the frame says.</summary>
public sealed class TestPrintHandler : IPrintHandler<TestFrame>
{
    /// <inheritdoc />
    public bool PrintReceivedByDefault => true;

    /// <inheritdoc />
    public int GetPrintCount(TestFrame frame) => frame.PrintCount;
}
