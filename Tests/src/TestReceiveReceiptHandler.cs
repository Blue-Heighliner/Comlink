namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IReceiveReceiptHandler{TFrame}"/> for <see cref="TestFrame"/>: a frame with a non-empty <see cref="TestFrame.ReceiveReceiptMessageId"/> is a receive receipt.</summary>
public sealed class TestReceiveReceiptHandler : IReceiveReceiptHandler<TestFrame>
{
    /// <inheritdoc />
    public Enum Priority { get; init; } = TestPriority.Normal;

    /// <inheritdoc />
    public bool IsValid(TestFrame frame) => !string.IsNullOrEmpty(frame.ReceiveReceiptMessageId);

    /// <inheritdoc />
    public TestFrame Create(ReceiptCreateContext context) => new() { IsHidden = true, ReceiveReceiptMessageId = context.MessageId, Addresses = [new TestAddressEntry { UserName = context.To }] };

    /// <inheritdoc />
    public string GetDestination(TestFrame frame) => frame.Addresses[0].UserName;

    /// <inheritdoc />
    public string GetSender(TestFrame frame) => frame.FromUser;

    /// <inheritdoc />
    public void SetSender(TestFrame frame, string sender) => frame.FromUser = sender;

    /// <inheritdoc />
    public string GetMessageId(TestFrame frame) => frame.ReceiveReceiptMessageId;
}
