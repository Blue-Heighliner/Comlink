namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="AutoForwardService"/> running the host's auto forward controllers.</summary>
public sealed class AutoForwardServiceTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? FrameDelivered;
#pragma warning disable CS0067
        public event Func<string, string, Task>? ReadReceiptReceived;
        public event Func<string, string, Task>? ReceiveReceiptReceived;
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
        public event Func<string, Task>? UserConnected;
        public event Func<string, Task>? UserDisconnected;
#pragma warning restore CS0067

        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default) => Task.FromResult(true);
        public Task DeliverLocal(object payload) => Task.CompletedTask;
        public IReadOnlyList<string> GetConnectedUsers() => [];
        public bool IsUserConnected(string userName) => false;
        public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default) => Task.FromResult(true);

        public bool HasMessageDeliveredSubscribers => FrameDelivered is not null;
        public Task FireMessageDelivered(object payload) => FrameDelivered is null ? Task.CompletedTask : FrameDelivered(payload);
    }

    private sealed class FakeMessageRoutingService : IMessageRoutingService
    {
#pragma warning disable CS0067
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067

        public List<(string FromUser, object Message)> RoutedMessages { get; } = [];

        public Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> Route(string fromUser, SendMessagePayload payload, CancellationToken cancellation)
            => Task.FromResult<(string, IReadOnlyList<UserDeliveryResult>)>(("M", []));

        public Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteFrame(string fromUser, object message, CancellationToken cancellation)
        {
            RoutedMessages.Add((fromUser, message));
            return Task.FromResult<(string, IReadOnlyList<UserDeliveryResult>)>(("M", []));
        }
    }

    private static (AutoForwardService Service, FakePeerService Peer, Mock<TestEngineController> EngineController, Mock<IUserService> UserService, FakeMessageRoutingService Routing, Mock<IAutoForwardTargetsRepository> Targets) Build()
    {
        FakePeerService peer = new();
        Mock<TestEngineController> engineController = new() { CallBase = true };
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[]);
        Mock<IUserService> userService = new();
        userService.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        FakeMessageRoutingService routing = new();
        Mock<IAutoForwardTargetsRepository> targets = new();
        AutoForwardService service = new(peer, engineController.Object, userService.Object, routing, targets.Object, noLogger);
        return (service, peer, engineController, userService, routing, targets);
    }

    private static AutoForwardControllerDefinition MakeController(string name, IReadOnlyList<string> users, Func<object, bool>? filter = null)
        => new() { Name = name, Users = users, Filter = filter ?? (_ => true) };

    /// <summary>With no controllers configured at all, Start returns immediately without subscribing to FrameDelivered.</summary>
    [Fact]
    public async Task Start_NoControllersConfigured_ReturnsImmediatelyWithoutSubscribing()
    {
        (AutoForwardService service, FakePeerService peer, _, _, _, _) = Build();

        await service.Start(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(peer.HasMessageDeliveredSubscribers);
    }

    /// <summary>With at least one controller configured, Start subscribes and blocks until cancelled.</summary>
    [Fact]
    public async Task Start_ControllersConfigured_SubscribesAndBlocksUntilCancelled()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, _, _) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("A", ["ME"])]);
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        Assert.True(peer.HasMessageDeliveredSubscribers);
        Assert.False(startTask.IsCompleted);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(peer.HasMessageDeliveredSubscribers);
    }

    /// <summary>A message matching a controller the current user has access to, with a non-empty target list, is forwarded to every target.</summary>
    [Fact]
    public async Task MessageDelivered_MatchingControllerWithTargets_ForwardsToEveryTarget()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing, Mock<IAutoForwardTargetsRepository> targets) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("Alerts", ["ME"])]);
        targets.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["ALICE", "BOB"] });
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);
        TestFrame message = new() { MessageId = "M1", FromUser = "SENDER", Body = "Hello" };

        await peer.FireMessageDelivered(message);

        (string fromUser, object forwarded) = Assert.Single(routing.RoutedMessages);
        Assert.Equal("ME", fromUser);
        TestFrame sent = Assert.IsType<TestFrame>(forwarded);
        Assert.Equal("Hello", sent.Body);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A frame that is not a message is never auto-forwarded, and what is forwarded is a message.</summary>
    [Fact]
    public async Task FrameDelivered_NotAMessage_IsNotForwarded_AndForwardsAreMessages()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing, Mock<IAutoForwardTargetsRepository> targets) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("Alerts", ["ME"])]);
        targets.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["ALICE"] });
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "F1", FromUser = "SENDER", IsHidden = true });
        Assert.Empty(routing.RoutedMessages);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "M1", FromUser = "SENDER" });
        (_, object forwarded) = Assert.Single(routing.RoutedMessages);
        Assert.False(Assert.IsType<TestFrame>(forwarded).IsHidden);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A message the controller's filter rejects is never forwarded.</summary>
    [Fact]
    public async Task MessageDelivered_FilterRejects_DoesNotForward()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing, Mock<IAutoForwardTargetsRepository> targets) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("Alerts", ["ME"], _ => false)]);
        targets.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["ALICE"] });
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "M1" });

        Assert.Empty(routing.RoutedMessages);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A controller the current user has no access to is never checked, even if its filter would otherwise match.</summary>
    [Fact]
    public async Task MessageDelivered_NoAccess_DoesNotForward()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing, Mock<IAutoForwardTargetsRepository> targets) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("Alerts", ["SOMEONE-ELSE"])]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "M1" });

        Assert.Empty(routing.RoutedMessages);
        targets.Verify(t => t.Get(It.IsAny<string>()), Times.Never);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A matching controller with no saved target list (or an empty one) forwards nothing.</summary>
    [Fact]
    public async Task MessageDelivered_NoTargetsSaved_DoesNotForward()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing, Mock<IAutoForwardTargetsRepository> targets) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("Alerts", ["ME"])]);
        targets.Setup(t => t.Get("Alerts")).ReturnsAsync((AutoForwardTargetsEntity?)null);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "M1" });

        Assert.Empty(routing.RoutedMessages);
        cts.Cancel();
        await startTask;
    }

    /// <summary>The current user's own name is excluded from the forwarded address list, even if present in the saved target list, to avoid a self-forward loop.</summary>
    [Fact]
    public async Task MessageDelivered_TargetsIncludeCurrentUser_ExcludesSelfButForwardsOthers()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing, Mock<IAutoForwardTargetsRepository> targets) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("Alerts", ["ME"])]);
        targets.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["ME", "ALICE"] });
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "M1" });

        (string _, object forwarded) = Assert.Single(routing.RoutedMessages);
        List<MessageAddress> addresses = engineController.Object.GetAddresses(forwarded);
        Assert.Single(addresses);
        Assert.Equal("ALICE", addresses[0].UserName);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A message matching two different accessible controllers is forwarded once per controller.</summary>
    [Fact]
    public async Task MessageDelivered_MatchesMultipleControllers_ForwardsOncePerController()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing, Mock<IAutoForwardTargetsRepository> targets) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)
        [
            MakeController("Alerts", ["ME"]),
            MakeController("Backups", ["ME"])
        ]);
        targets.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["ALICE"] });
        targets.Setup(t => t.Get("Backups")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Backups", Targets = ["BOB"] });
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "M1" });

        Assert.Equal(2, routing.RoutedMessages.Count);
        cts.Cancel();
        await startTask;
    }

    /// <summary>A filter that throws is logged and does not stop other controllers from being checked.</summary>
    [Fact]
    public async Task MessageDelivered_FilterThrows_OtherControllersStillChecked()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, _, FakeMessageRoutingService routing, Mock<IAutoForwardTargetsRepository> targets) = Build();
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)
        [
            MakeController("Failing", ["ME"], _ => throw new InvalidOperationException("boom")),
            MakeController("Working", ["ME"])
        ]);
        targets.Setup(t => t.Get("Working")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Working", Targets = ["ALICE"] });
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "M1" });

        Assert.Single(routing.RoutedMessages);
        cts.Cancel();
        await startTask;
    }

    /// <summary>FrameDelivered firing with no installed user is a no-op, rather than throwing.</summary>
    [Fact]
    public async Task MessageDelivered_NoInstalledUser_DoesNothing()
    {
        (AutoForwardService service, FakePeerService peer, Mock<TestEngineController> engineController, Mock<IUserService> userService, FakeMessageRoutingService routing, _) = Build();
        userService.Setup(u => u.GetCurrentUserInfo()).Returns((UserInfo?)null);
        engineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("Alerts", ["ME"])]);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        await peer.FireMessageDelivered(new TestFrame { MessageId = "M1" });

        Assert.Empty(routing.RoutedMessages);
        cts.Cancel();
        await startTask;
    }
}
