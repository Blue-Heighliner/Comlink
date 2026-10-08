namespace BlueHeighliner.Comlink.Tests;

/// <summary>An <see cref="IPeerService"/> for tests that records what is sent and lets the test raise what a peer would.</summary>
internal sealed class FakePeerService : IPeerService
{
    /// <inheritdoc />
    public event Func<ReceivedFrame, Task>? FrameReceived;

    /// <inheritdoc />
    public event Func<string, Task>? UserConnected;

    /// <inheritdoc />
    public event Func<string, Task>? UserDisconnected;

    /// <summary>Gets whether something is listening to both connection events.</summary>
    public bool HasSubscribers => UserConnected is not null && UserDisconnected is not null;

    /// <summary>Gets whether something is listening to received frames.</summary>
    public bool HasFrameSubscribers => FrameReceived is not null;

    /// <summary>Gets every frame sent, with who it was sent to and the priority.</summary>
    public List<(string User, object Frame, int Priority)> Sent { get; } = [];

    /// <summary>Gets or sets what every send reports.</summary>
    public bool SendResult { get; set; } = true;

    /// <summary>Gets the names of the users reported connected.</summary>
    public HashSet<string> Connected { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<string> GetConnectedUsers() => [.. Connected];

    /// <inheritdoc />
    public bool IsUserConnected(string userName) => Connected.Contains(userName);

    /// <inheritdoc />
    public Task Start(CancellationToken cancellation) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> Send(string userName, object frame, int priority, CancellationToken cancellation = default)
    {
        Sent.Add((userName, frame, priority));
        return Task.FromResult(SendResult);
    }

    /// <inheritdoc />
    public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default) => Task.FromResult(true);

    /// <summary>Raises <see cref="FrameReceived"/> as a peer delivering <paramref name="frame"/>.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="sourceUser">The user it arrived from.</param>
    public Task Receive(object frame, string sourceUser = "PEER") => FrameReceived is null ? Task.CompletedTask : FrameReceived(new ReceivedFrame(frame, sourceUser));

    /// <summary>Raises <see cref="UserDisconnected"/>.</summary>
    /// <param name="userName">The user.</param>
    public Task Disconnect(string userName) => UserDisconnected is null ? Task.CompletedTask : UserDisconnected(userName);

    /// <summary>Raises <see cref="UserConnected"/>.</summary>
    /// <param name="userName">The user.</param>
    public Task Connect(string userName) => UserConnected is null ? Task.CompletedTask : UserConnected(userName);
}
