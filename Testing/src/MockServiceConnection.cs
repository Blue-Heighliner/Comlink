namespace BlueHeighliner.Comlink.Testing;

/// <summary>
/// A mock of the <see cref="IServiceConnection{TPriority, TLevel, TAspect}"/> a host resolves to drive the engine. It connects at once, answers the user and user names it is given, accepts every send and mark read, and
/// raises the engine's events on request, so a component built on it can be tested without the engine.
/// </summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public sealed class MockServiceConnection<TPriority, TLevel, TAspect> : Mock<IServiceConnection<TPriority, TLevel, TAspect>> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="userInfo">The user the node runs as, which <c>GetUserInfo</c> returns, or <see langword="null"/> for none.</param>
    public MockServiceConnection(UserInfo? userInfo = null)
    {
        UserInfo = userInfo;
        Setup(connection => connection.Connect(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Setup(connection => connection.GetUserInfo(It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(UserInfo));
        Setup(connection => connection.GetUserNames(It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(UserNames));
        Setup(connection => connection.GetConnectedUsers(It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(ConnectedUsers));
        Setup(connection => connection.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<TPriority?>(), It.IsAny<string>(), It.IsAny<TLevel?>(), It.IsAny<TAspect?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<SendMessageResult?>(SendMessageResult));
        Setup(connection => connection.MarkMessageRead(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
    }

    /// <summary>Gets or sets what <c>GetUserInfo</c> returns.</summary>
    public UserInfo? UserInfo { get; set; }

    /// <summary>Gets or sets what <c>GetUserNames</c> returns. Defaults to none.</summary>
    public List<string> UserNames { get; set; } = [];

    /// <summary>Gets or sets what <c>GetConnectedUsers</c> returns. Defaults to none.</summary>
    public List<string> ConnectedUsers { get; set; } = [];

    /// <summary>Gets or sets what <c>SendMessage</c> returns. Defaults to a result for a message with the identifier <c>MESSAGE</c>.</summary>
    public SendMessageResult? SendMessageResult { get; set; } = new() { MessageId = "MESSAGE" };

    /// <summary>Raises <see cref="IServiceConnection{TPriority, TLevel, TAspect}.MessageReceived"/>, as the engine does when a message is received.</summary>
    /// <param name="message">The message that was received.</param>
    public void RaiseMessageReceived(Message<TPriority, TLevel, TAspect> message) => this.Raise(connection => connection.MessageReceived += null, message);

    /// <summary>Raises <see cref="IServiceConnection{TPriority, TLevel, TAspect}.DeliveryStatusChanged"/>, as the engine does when a delivery status changes.</summary>
    /// <param name="change">What changed.</param>
    public void RaiseDeliveryStatusChanged(DeliveryStatusChangedEvent change) => this.Raise(connection => connection.DeliveryStatusChanged += null, change);
}
