namespace BlueHeighliner.Comlink.Testing;

/// <summary>
/// A stand-in for the <see cref="IServiceConnection{TPriority, TLevel, TAspect, TProfile}"/> a host component uses to talk to the engine, to test the component without it. It connects at once, answers what it is set to, accepts every send and
/// mark read (recording them), and raises the engine's events when the test calls <see cref="RaiseMessageReceived"/> or <see cref="RaiseDeliveryStatusChanged"/>.
/// </summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
/// <typeparam name="TProfile">The record type the host stated for user profiles in its <c>Types</c> call, or <see cref="NoProfile"/>.</typeparam>
/// <param name="userInfo">What <c>GetUserInfo</c> answers, or <see langword="null"/> for no user.</param>
public sealed class FakeServiceConnection<TPriority, TLevel, TAspect, TProfile>(UserInfo<TProfile>? userInfo = null) : IServiceConnection<TPriority, TLevel, TAspect, TProfile> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
{
    /// <inheritdoc />
    public event Func<Message<TPriority, TLevel, TAspect>, Task>? MessageReceived;

    /// <inheritdoc />
    public event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

    /// <summary>Gets how many times the component connected.</summary>
    public int ConnectCount { get; private set; }

    /// <summary>Gets every message the component sent, in order.</summary>
    public List<SentMessage> Sent { get; } = [];

    /// <summary>Gets the identifier of every message the component marked read, in order.</summary>
    public List<string> MarkedRead { get; } = [];

    /// <summary>Gets or sets what <c>GetUserInfo</c> answers.</summary>
    public UserInfo<TProfile>? UserInfo { get; set; } = userInfo;

    /// <summary>Gets or sets what <c>GetUserNames</c> answers. Defaults to none.</summary>
    public List<string> UserNames { get; set; } = [];

    /// <summary>Gets or sets what <c>GetConnectedUsers</c> answers. Defaults to none.</summary>
    public List<string> ConnectedUsers { get; set; } = [];

    /// <summary>Gets or sets what <c>SendMessage</c> answers, <see langword="null"/> for a send with no installed user. Defaults to a result with the identifier <c>MESSAGE</c>.</summary>
    public SendMessageResult? SendMessageResult { get; set; } = new() { MessageId = "MESSAGE" };

    /// <summary>Gets or sets what <c>MarkMessageRead</c> answers. Defaults to <see langword="true"/>.</summary>
    public bool MarkReadResult { get; set; } = true;

    /// <summary>Raises <see cref="MessageReceived"/> as if the engine received <paramref name="message"/>, and completes once every subscriber has.</summary>
    /// <param name="message">The message received.</param>
    public Task RaiseMessageReceived(Message<TPriority, TLevel, TAspect> message) => MessageReceived.InvokeAll(message);

    /// <summary>Raises <see cref="DeliveryStatusChanged"/> as if the engine saw <paramref name="change"/>, and completes once every subscriber has.</summary>
    /// <param name="change">The change in delivery status.</param>
    public Task RaiseDeliveryStatusChanged(DeliveryStatusChangedEvent change) => DeliveryStatusChanged.InvokeAll(change);

    /// <inheritdoc />
    public Task Connect(CancellationToken cancellation = default)
    {
        ConnectCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<UserInfo<TProfile>?> GetUserInfo(CancellationToken cancellation = default) => Task.FromResult(UserInfo);

    /// <inheritdoc />
    public Task<List<string>> GetUserNames(CancellationToken cancellation = default) => Task.FromResult(UserNames);

    /// <inheritdoc />
    public Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default) => Task.FromResult(ConnectedUsers);

    /// <inheritdoc />
    public Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, TPriority? priority = null, string tag = "", TLevel? messageLevel = null, TAspect? messageAspect = null, CancellationToken cancellation = default)
    {
        Sent.Add(new SentMessage(body, addresses, priority, tag, messageLevel, messageAspect));
        return Task.FromResult(SendMessageResult);
    }

    /// <inheritdoc />
    public Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default)
    {
        MarkedRead.Add(messageId);
        return Task.FromResult(MarkReadResult);
    }

    /// <summary>A message the component sent through <c>SendMessage</c>.</summary>
    /// <param name="Body">The body.</param>
    /// <param name="Addresses">The recipients.</param>
    /// <param name="Priority">The priority, or <see langword="null"/> for the default.</param>
    /// <param name="Tag">The tag, empty for none.</param>
    /// <param name="MessageLevel">The message level, or <see langword="null"/> for the default.</param>
    /// <param name="MessageAspect">The message aspect, or <see langword="null"/> for none.</param>
    public sealed record SentMessage(string Body, List<AddressRequest> Addresses, TPriority? Priority, string Tag, TLevel? MessageLevel, TAspect? MessageAspect);
}
