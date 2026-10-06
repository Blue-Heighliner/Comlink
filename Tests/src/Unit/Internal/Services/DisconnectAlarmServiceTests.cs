namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="DisconnectAlarmService"/>.</summary>
public sealed class DisconnectAlarmServiceTests : IAsyncLifetime
{
    private sealed class FakePeerService : IPeerService
    {
#pragma warning disable CS0067
        public event Func<object, Task>? FrameDelivered;
        public event Func<string, string, Task>? ReadReceiptReceived;
        public event Func<string, string, Task>? ReceiveReceiptReceived;
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067
        public event Func<string, Task>? UserConnected;
        public event Func<string, Task>? UserDisconnected;

        public bool HasSubscribers => UserConnected is not null && UserDisconnected is not null;

        public IReadOnlyList<string> GetConnectedUsers() => [];
        public bool IsUserConnected(string userName) => false;
        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default) => Task.FromResult(true);
        public Task DeliverLocal(object payload) => Task.CompletedTask;
        public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default) => Task.FromResult(true);

        public Task Disconnect(string userName) => UserDisconnected is null ? Task.CompletedTask : UserDisconnected(userName);
        public Task Connect(string userName) => UserConnected is null ? Task.CompletedTask : UserConnected(userName);
    }

    private readonly FakePeerService peer = new();
    private readonly Mock<IEngineController> controller = new();
    private readonly Mock<IDisconnectAlarmPlayer> player = new();
    private readonly CancellationTokenSource cancellation = new();
    private Task start = Task.CompletedTask;

    /// <summary>Starts the service with a disconnect alarm duration of the given length.</summary>
    public Task InitializeAsync()
    {
        controller.Setup(c => c.DisconnectAlarmDuration).Returns(TimeSpan.FromMilliseconds(300));
        DisconnectAlarmService service = new(peer, controller.Object, player.Object);
        start = service.Start(cancellation.Token);
        return WaitUntil(() => peer.HasSubscribers);
    }

    /// <summary>Stops the service.</summary>
    public async Task DisposeAsync()
    {
        await cancellation.CancelAsync();
        await start;
    }

    private Task Drop(string user) => peer.Disconnect(user);

    private Task Restore(string user) => peer.Connect(user);

    private async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline) { await Task.Delay(10); }
    }

    /// <summary>A dropped connection starts the alarm, and it stops by itself after the duration.</summary>
    [Fact]
    public async Task Drop_PlaysThenStopsAfterTheDuration()
    {
        await Drop("ALICE");

        player.Verify(p => p.Play(), Times.Once);
        player.Verify(p => p.Stop(), Times.Never);

        await WaitUntil(() => player.Invocations.Any(i => i.Method.Name == nameof(IDisconnectAlarmPlayer.Stop)));
        player.Verify(p => p.Stop(), Times.Once);
    }

    /// <summary>A connection dropping while the alarm sounds starts the time again.</summary>
    [Fact]
    public async Task SecondDropWhileSounding_RestartsTheDuration()
    {
        await Drop("ALICE");
        await Task.Delay(200);
        await Drop("BOB");
        await Task.Delay(200);

        player.Verify(p => p.Stop(), Times.Never);

        await WaitUntil(() => player.Invocations.Any(i => i.Method.Name == nameof(IDisconnectAlarmPlayer.Stop)));
        player.Verify(p => p.Stop(), Times.Once);
    }

    /// <summary>The alarm stops early once every connection that dropped is back, and not before.</summary>
    [Fact]
    public async Task AllDroppedConnectionsBack_StopsTheAlarmEarly()
    {
        await Drop("ALICE");
        await Drop("BOB");

        await Restore("ALICE");
        player.Verify(p => p.Stop(), Times.Never);

        await Restore("BOB");
        player.Verify(p => p.Stop(), Times.Once);
    }

    /// <summary>A connection coming up that did not drop during the alarm does not stop it.</summary>
    [Fact]
    public async Task UnrelatedConnectionComingUp_DoesNotStopTheAlarm()
    {
        await Drop("ALICE");

        await Restore("CAROL");

        player.Verify(p => p.Stop(), Times.Never);
    }
}
