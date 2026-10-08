namespace BlueHeighliner.Comlink;

/// <summary>The engine's own connection to itself, with messages as the engine holds them. Hosts see it as an <see cref="IServiceConnection{TPriority, TLevel, TAspect}"/>, typed by their own enums.</summary>
internal interface IEngineConnection
{
    /// <summary>Raised when the host's network processor records a message as received (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.ReceiveMessage"/>).</summary>
    event Func<Message, Task>? MessageReceived;
    /// <summary>Raised when the delivery status of an outbound message changes.</summary>
    event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;
    /// <summary>Establishes the connection to the Engine service.</summary>
    Task Connect(CancellationToken cancellation = default);
    /// <summary>Returns this user's own <see cref="UserInfo"/>, or <see langword="null"/> if not yet registered.</summary>
    Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default);
    /// <summary>Returns the names of all known users in the messaging system.</summary>
    Task<List<string>> GetUserNames(CancellationToken cancellation = default);
    /// <summary>Returns the names of every user currently reachable over at least one live peer connection, unlike <see cref="GetUserNames"/>'s fixed configured directory.</summary>
    Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default);
    /// <summary>
    /// Registers this instance as the user named <paramref name="userName"/> and returns the resulting <see cref="UserInfo"/>, or <see langword="null"/> when the network has no user of that name.
    /// The user's certificate must be in place: the file <c>{userName}.pfx</c> in the network's certificate store, issued to that user and signed by the authority certificate.
    /// </summary>
    /// <exception cref="InvalidOperationException">The certificate is missing, not issued to the user or not signed by the authority certificate; nothing is installed.</exception>
    Task<UserInfo?> InstallUser(string userName, CancellationToken cancellation = default);
    /// <summary>
    /// Sends a message with the given <paramref name="body"/> to the specified
    /// <paramref name="addresses"/>: the engine stores it in the Outbox and hands it to the host's network processor (see <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}.OnSent"/>), which sends it and reports each
    /// destination's outcome as delivery status changes. Whether the message is an alert, which alarms recipients' Client-mode UI until it is read (see <c>Docs/Components/ViewModels.md</c>), is not
    /// chosen here: the host's draft handler decides it from the message's other properties (see <see cref="SendMessageResult.IsAlert"/>). <paramref name="priority"/>
    /// is the message's priority, which the processor sends it with (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>). <paramref name="tag"/>
    /// is stored in the message's tag field (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>). <paramref name="messageLevel"/>
    /// is the message level this message is sent at, a member of the enum the host stated for its message levels (or <see langword="null"/> for none), which must be a configured one or the call throws; a destination user whose own assigned level ranks lower
    /// is never sent the message (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>). <paramref name="messageAspect"/> is the message aspect the message carries, a member of the enum the host stated with
    /// <c>MessageAspects</c> (or <see langword="null"/> for none), which must be a configured one or the call throws.
    /// </summary>
    Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, Enum? priority = null, string tag = "", Enum? messageLevel = null, Enum? messageAspect = null, CancellationToken cancellation = default);
    /// <summary>
    /// Marks the Inbox record for <paramref name="messageId"/> as read (no-op if already read or not
    /// found) and tells the host's network processor (see <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}.OnRead"/>), which can
    /// tell the original sender so it can advance that message's Outbox delivery status to <see cref="DestinationStatus.Read"/>. Returns
    /// <see langword="true"/> if the record's read state actually changed.
    /// </summary>
    Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
}
