namespace BlueHeighliner.Comlink;

/// <summary>
/// High-level client API for host code to interact with a running engine. The engine registers it, typed by the enums the host stated with
/// <see cref="IEngineBuilder.Types{TFrame, TPacket, TPriority, TLevel, TAspect}"/>, in both Client and Headless mode.
/// </summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public interface IServiceConnection<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Raised when the host's network processor records a message as received (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.ReceiveMessage"/>).</summary>
    event Func<Message<TPriority, TLevel, TAspect>, Task>? MessageReceived;

    /// <summary>Raised when the delivery status of an outbound message changes.</summary>
    event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

    /// <summary>Establishes the connection to the Engine service.</summary>
    /// <param name="cancellation">Cancels the wait.</param>
    Task Connect(CancellationToken cancellation = default);

    /// <summary>Returns this user's own <see cref="UserInfo"/>, or <see langword="null"/> if not yet registered.</summary>
    /// <param name="cancellation">Cancels the wait.</param>
    Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default);

    /// <summary>Returns the names of all known users in the messaging system.</summary>
    /// <param name="cancellation">Cancels the wait.</param>
    Task<List<string>> GetUserNames(CancellationToken cancellation = default);

    /// <summary>Returns the names of every user currently reachable over at least one live peer connection, unlike <see cref="GetUserNames"/>'s fixed configured directory.</summary>
    /// <param name="cancellation">Cancels the wait.</param>
    Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default);

    /// <summary>
    /// Registers this instance as the user named <paramref name="userName"/> and returns the resulting <see cref="UserInfo"/>, or <see langword="null"/> when the network has no user of that name.
    /// The user's certificate must be in place: the file <c>{userName}.pfx</c> in the network's certificate store, issued to that user and signed by the authority certificate.
    /// </summary>
    /// <param name="userName">The user to install.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <exception cref="InvalidOperationException">The certificate is missing, not issued to the user or not signed by the authority certificate; nothing is installed.</exception>
    Task<UserInfo?> InstallUser(string userName, CancellationToken cancellation = default);

    /// <summary>
    /// Sends a message with the given <paramref name="body"/> to the specified <paramref name="addresses"/>: the engine stores it in the Outbox and hands it to the host's network processor
    /// (see <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}.OnSent"/>), which sends it and reports each destination's outcome as delivery status changes. Whether the message is an alert
    /// is not chosen here: the host's draft handler decides it (see <see cref="SendMessageResult.IsAlert"/>). The priority, message level and message aspect must be configured ones or the call throws.
    /// </summary>
    /// <param name="body">The body text.</param>
    /// <param name="addresses">The addresses the message is for.</param>
    /// <param name="priority">The message's priority, or <see langword="null"/> for the lowest.</param>
    /// <param name="tag">The message's tag.</param>
    /// <param name="messageLevel">The message level the message is sent at, or <see langword="null"/> for none.</param>
    /// <param name="messageAspect">The message aspect the message carries, or <see langword="null"/> for none.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, TPriority? priority = null, string tag = "", TLevel? messageLevel = null, TAspect? messageAspect = null, CancellationToken cancellation = default);

    /// <summary>
    /// Marks the Inbox record for <paramref name="messageId"/> as read (no-op if already read or not found) and tells the host's network processor (see
    /// <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}.OnRead"/>). Returns <see langword="true"/> if the record's read state actually changed.
    /// </summary>
    /// <param name="messageId">The identifier of the received message.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
}
