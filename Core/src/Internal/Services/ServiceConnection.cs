namespace BlueHeighliner.Comlink;

/// <summary>Presents the engine's own connection as an <see cref="IServiceConnection{TPriority, TLevel, TAspect}"/>, typed by the host's enums.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
internal sealed class ServiceConnection<TPriority, TLevel, TAspect> : IServiceConnection<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Creates a connection over <paramref name="engineConnection"/>.</summary>
    /// <param name="engineConnection">The engine's own connection.</param>
    public ServiceConnection(IEngineConnection engineConnection)
    {
        this.engineConnection = engineConnection;
        engineConnection.MessageReceived += message => MessageReceived?.Invoke(message.ToTyped<TPriority, TLevel, TAspect>()) ?? Task.CompletedTask;
        engineConnection.DeliveryStatusChanged += status => DeliveryStatusChanged?.Invoke(status) ?? Task.CompletedTask;
    }

    private readonly IEngineConnection engineConnection;

    /// <inheritdoc />
    public event Func<Message<TPriority, TLevel, TAspect>, Task>? MessageReceived;

    /// <inheritdoc />
    public event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

    /// <inheritdoc />
    public Task Connect(CancellationToken cancellation = default) => engineConnection.Connect(cancellation);

    /// <inheritdoc />
    public Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default) => engineConnection.GetUserInfo(cancellation);

    /// <inheritdoc />
    public Task<List<string>> GetUserNames(CancellationToken cancellation = default) => engineConnection.GetUserNames(cancellation);

    /// <inheritdoc />
    public Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default) => engineConnection.GetConnectedUsers(cancellation);

    /// <inheritdoc />
    public Task<UserInfo?> InstallUser(string userName, CancellationToken cancellation = default) => engineConnection.InstallUser(userName, cancellation);

    /// <inheritdoc />
    public Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, TPriority? priority = null, string tag = "", TLevel? messageLevel = null, TAspect? messageAspect = null, CancellationToken cancellation = default)
        => engineConnection.SendMessage(body, addresses, priority, tag, messageLevel, messageAspect, cancellation);

    /// <inheritdoc />
    public Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default) => engineConnection.MarkMessageRead(messageId, cancellation);
}
