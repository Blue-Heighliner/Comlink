namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IReadReceiptHandler{TFrame}"/> for <see cref="TestFrame"/>: a frame with a non-empty <see cref="TestFrame.ReadReceiptMessageId"/> is a read receipt.</summary>
public sealed class TestReadReceiptHandler : IReadReceiptHandler<TestFrame>
{
    /// <inheritdoc />
    public bool IsValid(TestFrame frame) => !string.IsNullOrEmpty(frame.ReadReceiptMessageId);

    /// <inheritdoc />
    public TestFrame Create(ReceiptCreateContext context) => new() { IsHidden = true, ReadReceiptMessageId = context.MessageId };

    /// <inheritdoc />
    public string GetMessageId(TestFrame frame) => frame.ReadReceiptMessageId;
}
