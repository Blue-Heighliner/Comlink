namespace BlueHeighliner.Comlink;

/// <summary>The input the engine hands to <see cref="IReadReceiptHandler{TFrame}.Create"/> and <see cref="IReceiveReceiptHandler{TFrame}.Create"/> to build a receipt frame.</summary>
public sealed record ReceiptCreateContext
{
    /// <summary>Gets the identifier of the message the receipt is for.</summary>
    public required string MessageId { get; init; }
}
