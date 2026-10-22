namespace BlueHeighliner.Comlink;

/// <summary>Presents the engine's own connection as an <see cref="IServiceConnection{TPriority, TLevel, TAspect, TProfile}"/>, typed by the host's enums.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its aspects.</typeparam>
/// <typeparam name="TProfile">The record type the host stated for user profiles.</typeparam>
internal sealed class ServiceConnection<TPriority, TLevel, TAspect, TProfile> : IServiceConnection<TPriority, TLevel, TAspect, TProfile> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
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
    public async Task<UserInfo<TLevel, TProfile>?> GetUserInfo(CancellationToken cancellation = default) => await engineConnection.GetUserInfo(cancellation) is { } info ? UserInfo<TLevel, TProfile>.From(info) : null;

    /// <inheritdoc />
    public Task<List<string>> GetUserNames(CancellationToken cancellation = default) => engineConnection.GetUserNames(cancellation);

    /// <inheritdoc />
    public Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default) => engineConnection.GetConnectedUsers(cancellation);

    /// <inheritdoc />
    public Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, TPriority? priority = null, string tag = "", TLevel? level = null, TAspect? aspect = null, CancellationToken cancellation = default)
        => engineConnection.SendMessage(body, addresses, priority, tag, level, aspect, cancellation);

    /// <inheritdoc />
    public Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default) => engineConnection.MarkMessageRead(messageId, cancellation);
}
