namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the frames of the host's frame type <typeparamref name="TFrame"/> that are messages: the ones the user reads, which are stored in the Inbox when received
/// and in the Outbox when sent. A frame that is not a message is still routed and handed to the network processor, but never shown or stored. See <see cref="IFrameBuilder{TFrame}.Message{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IMessageHandler<TFrame> where TFrame : class
{
    /// <summary>Returns whether <paramref name="frame"/> is a message.</summary>
    /// <param name="frame">The frame to classify.</param>
    bool IsValid(TFrame frame);

    /// <summary>Creates a new message frame carrying <paramref name="context"/>, for which <see cref="IsValid"/> returns <see langword="true"/>. The engine sets the identifier, sender and addresses itself.</summary>
    /// <param name="context">The content of the message.</param>
    /// <returns>The new frame.</returns>
    TFrame Create(MessageCreateContext context);

    /// <summary>Gets the UTC time <paramref name="frame"/> was sent.</summary>
    DateTime GetSentAt(TFrame frame);

    /// <summary>Gets the body text of <paramref name="frame"/>.</summary>
    string GetBody(TFrame frame);

    /// <summary>Gets whether <paramref name="frame"/> is an alert, a message that makes a receiving Client-mode UI alarm until the user reads it.</summary>
    bool GetIsAlert(TFrame frame);

    /// <summary>Gets the priority number of <paramref name="frame"/>, used verbatim as its send priority (larger values are sent first).</summary>
    int GetPriority(TFrame frame);

    /// <summary>Gets the short tag identifying the type of message <paramref name="frame"/> is, or an empty string for none.</summary>
    string GetTag(TFrame frame);

    /// <summary>Gets the security level name <paramref name="frame"/> was sent at, or an empty string when none are configured.</summary>
    string GetSecurityLevel(TFrame frame);
}
