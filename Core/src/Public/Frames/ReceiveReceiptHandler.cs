namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the frames of the host's frame type <typeparamref name="TFrame"/> that are receive receipts: sent back to the sender of a message as soon as its recipient's node receives it, so the
/// sender's delivery status for that recipient advances to received. A receipt is not a message. See <see cref="IFrameBuilder{TFrame}.ReceiveReceipt{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IReceiveReceiptHandler<TFrame> where TFrame : class
{
    /// <summary>Returns whether <paramref name="frame"/> is a receive receipt.</summary>
    /// <param name="frame">The frame to classify.</param>
    bool IsValid(TFrame frame);

    /// <summary>Creates a new receive receipt frame for <paramref name="context"/>, for which <see cref="IsValid"/> returns <see langword="true"/>. The engine sets the identifier, sender, addresses and sent time itself.</summary>
    /// <param name="context">The message the receipt is for.</param>
    /// <returns>The new frame.</returns>
    TFrame Create(ReceiptCreateContext context);

    /// <summary>Gets the identifier of the message <paramref name="frame"/> is a receive receipt for.</summary>
    string GetMessageId(TFrame frame);
}
