namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the frames of the host's frame type <typeparamref name="TFrame"/> that are receive receipts: sent back to the sender of a message as soon as its recipient's node receives it, so the
/// sender's delivery status for that recipient advances to received. A receipt is not a message. See <see cref="IFrameBuilder{TFrame}.ReceiveReceipt{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IReceiveReceiptHandler<TFrame> where TFrame : class
{
    /// <summary>Gets the member of the priority enum (see <see cref="IEngineBuilder.Priorities{TPriority}"/>) naming the level that receive receipts are sent with, which is how they are ordered against other traffic.</summary>
    Enum Priority { get; }

    /// <summary>Returns whether <paramref name="frame"/> is a receive receipt.</summary>
    /// <param name="frame">The frame to classify.</param>
    bool IsValid(TFrame frame);

    /// <summary>Creates a new receive receipt frame for <paramref name="context"/>, for which <see cref="IsValid"/> returns <see langword="true"/>. The engine sets the identifier, sender, addresses and sent time itself.</summary>
    /// <param name="context">The message the receipt is for.</param>
    /// <returns>The new frame.</returns>
    TFrame Create(ReceiptCreateContext context);

    /// <summary>Gets the user name of the sender of <paramref name="frame"/>.</summary>
    string GetSender(TFrame frame);

    /// <summary>Sets the user name of the sender of <paramref name="frame"/>, which the engine does for every receive receipt it sends.</summary>
    /// <param name="frame">The receive receipt.</param>
    /// <param name="sender">The sender's user name.</param>
    void SetSender(TFrame frame, string sender);

    /// <summary>Gets the user name <paramref name="frame"/> is for: where the engine sends this receive receipt, and where a server or relay hands it on to. The engine states it when it creates the frame (see <see cref="ReceiptCreateContext.To"/>).</summary>
    string GetDestination(TFrame frame);

    /// <summary>Gets the identifier of the message <paramref name="frame"/> is a receive receipt for.</summary>
    string GetMessageId(TFrame frame);
}
