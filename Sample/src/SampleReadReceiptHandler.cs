namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="SampleFrame"/> as a read receipt when its <see cref="SampleFrame.IsReadReceipt"/> flag is set.</summary>
public sealed class SampleReadReceiptHandler : IReadReceiptHandler<SampleFrame>
{
    /// <inheritdoc />
    public string Priority => SamplePriorities.Receipt;

    /// <inheritdoc />
    public bool IsValid(SampleFrame frame) => frame.IsReadReceipt;

    /// <inheritdoc />
    public SampleFrame Create(ReceiptCreateContext context) => new() { IsReadReceipt = true, ReadMessageId = context.MessageId };

    /// <inheritdoc />
    public string GetMessageId(SampleFrame frame) => frame.ReadMessageId;
}
