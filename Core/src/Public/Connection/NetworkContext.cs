namespace BlueHeighliner.Comlink;

/// <summary>What every <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}"/> method is handed: the engine, and the ways a processor acts on it.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public interface INetworkContext<TFrame, TPriority, TLevel, TAspect> : IEngineContext where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>
    /// Sends <paramref name="frame"/> to <paramref name="userName"/> over the connection identified as them, and completes once the transport has
    /// fully acknowledged it. The user must be directly connected to the current user: the engine never routes or relays, and reaching anyone further away is the processor's to do by sending to the node that is directly connected. Nothing in the frame is changed or checked: the engine does not know what is in it.
    /// </summary>
    /// <param name="userName">The user to send it to.</param>
    /// <param name="priority">The priority to send it with.</param>
    /// <param name="frame">What to send.</param>
    /// <returns><see langword="true"/> if the frame was sent and accepted, <see langword="false"/> if the user is not directly connected or the frame was not accepted.</returns>
    /// <exception cref="ArgumentException"><paramref name="priority"/> is not one of the configured priorities.</exception>
    Task<bool> Send(string userName, TPriority priority, TFrame frame);

    /// <summary>
    /// Records <paramref name="message"/> as received: the engine stores it in the Inbox, shows it to the user and, for an alert, alarms. A message whose identifier is already in the Inbox is ignored, and an alert is
    /// subject to the alarm handler's history limit. This is how a received frame becomes a message the user sees; the engine does it for nothing else.
    /// </summary>
    /// <param name="message">The message that was received.</param>
    /// <exception cref="ArgumentException">The message's priority, message level or message aspect is not a configured one.</exception>
    Task ReceiveMessage(Message<TPriority, TLevel, TAspect> message);

    /// <summary>Changes <paramref name="userName"/>'s delivery status on the message the user sent with the identifier <paramref name="messageId"/>. A status that would move backward, such as <see cref="DestinationStatus.Sent"/> after <see cref="DestinationStatus.Received"/>, is ignored, and a destination the message did not list yet is added. Nothing happens for a message that is not stored.</summary>
    /// <param name="messageId">The identifier of the sent message.</param>
    /// <param name="userName">The destination whose status changed.</param>
    /// <param name="status">The new status.</param>
    Task SetSentStatus(string messageId, string userName, DestinationStatus status);

    /// <summary>Changes the delivery status of every user in <paramref name="userNames"/> on the message the user sent with the identifier <paramref name="messageId"/>, as <see cref="SetSentStatus(string, string, DestinationStatus)"/> does for one.</summary>
    /// <param name="messageId">The identifier of the sent message.</param>
    /// <param name="userNames">The destinations whose status changed.</param>
    /// <param name="status">The new status.</param>
    Task SetSentStatus(string messageId, IEnumerable<string> userNames, DestinationStatus status);

    /// <summary>Changes the status of the message this user received with the identifier <paramref name="messageId"/>, to <see cref="DestinationStatus.Received"/> or <see cref="DestinationStatus.Read"/>. Nothing happens for a message that is not stored, and a status that would move backward is ignored.</summary>
    /// <param name="messageId">The identifier of the received message.</param>
    /// <param name="status">The new status.</param>
    Task SetReceivedStatus(string messageId, DestinationStatus status);

    /// <summary>Sets the network indicator in the top bar of a client to online or offline, with the labels and colors the display handler states. The engine never sets it itself.</summary>
    /// <param name="isOnline"><see langword="true"/> to show online, <see langword="false"/> to show offline.</param>
    void SetNetworkIndicator(bool isOnline);

    /// <summary>Sends <paramref name="frame"/> to every external system, each of which takes only the frames it handles.</summary>
    /// <param name="frame">What to send.</param>
    Task SendToExternalSystems(TFrame frame);

    /// <summary>Keeps a copy of <paramref name="message"/> in the server's own message storage, unless one with the same identifier is already kept. This is for a server that answers retrievals; it has nothing to do with the Inbox and the Outbox.</summary>
    /// <param name="message">The message to keep.</param>
    Task StoreMessage(Message<TPriority, TLevel, TAspect> message);

    /// <summary>Finds the messages kept with <see cref="StoreMessage"/> that fit <paramref name="criteria"/>, whoever sent or received them, ordered by sent time.</summary>
    /// <param name="criteria">What the messages must fit.</param>
    Task<IReadOnlyList<Message<TPriority, TLevel, TAspect>>> FindStoredMessages(RetrievalCriteria criteria);

    /// <summary>
    /// Gets the users that <paramref name="targets"/> stand for: each target that is the name of a group is replaced by the users that belong to it, groups within it expanded, and every other target is taken to be a user.
    /// A user whose own message level ranks below <paramref name="minimumLevel"/> is left out. 
    /// </summary>
    /// <param name="minimumLevel">The lowest message level a user may run at to be included, or <see langword="null"/> to include every user. Ignored when message levels are not in use.</param>
    /// <param name="excluded">The users that were left out for ranking below <paramref name="minimumLevel"/>, for the processor to report as failed.</param>
    /// <param name="targets">User and group names.</param>
    /// <exception cref="ArgumentException"><paramref name="minimumLevel"/> is not one of the configured message levels.</exception>
    IReadOnlySet<string> GetDestinations(TLevel? minimumLevel, out IReadOnlySet<string> excluded, params IEnumerable<string> targets);

    /// <summary>Gets the users <paramref name="message"/> is for: the distinct user names of all its addresses that are not <see cref="AddressType.External"/>, with groups expanded and users whose message level ranks below the message's left out, as <see cref="GetDestinations(TLevel?, out IReadOnlySet{string}, IEnumerable{string})"/> does.</summary>
    /// <param name="message">The message.</param>
    /// <param name="excluded">The users that were left out for ranking below the message's level, for the processor to report as failed.</param>
    IReadOnlySet<string> GetDestinations(Message<TPriority, TLevel, TAspect> message, out IReadOnlySet<string> excluded);

    /// <summary>Gets the users the current user has put on the target list of the auto forwarder named <paramref name="controllerName"/>, which the processor forwards what that auto forwarder accepts to, leaving out the current user so a forward never comes back to its sender. Empty when there is no such auto forwarder or the list is empty.</summary>
    /// <param name="controllerName">The auto forwarder's name (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.AutoForwarder"/>).</param>
    Task<IReadOnlyList<string>> GetAutoForwardTargets(string controllerName);
}
