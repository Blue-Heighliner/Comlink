namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="StagedSendViewModel"/>.</summary>
public sealed class StagedSendViewModelTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static StagedSendData MakeSend(string body) => new() { Body = body, Addresses = [new AddressRequest { UserName = "Bob" }] };

    private sealed class Setup
    {
        public Mock<IServiceConnection> Connection { get; } = new();
        public Mock<IEntryService> EntryService { get; } = new();

        public StagedSendViewModel Build() => new(Connection.Object, EntryService.Object, noLogger);
    }

    /// <summary>Enqueue adds every send to the queue as Pending, in order.</summary>
    [Fact]
    public void Enqueue_AddsEntriesAsPendingInOrder()
    {
        StagedSendViewModel vm = new Setup().Build();

        vm.Enqueue([MakeSend("A"), MakeSend("B")], StagedSendMode.Sequential, null);

        Assert.Equal(["A", "B"], vm.Queue.Select(e => e.Preview));
        Assert.All(vm.Queue, e => Assert.Equal(StagedSendStatus.Pending, e.Status));
        Assert.True(vm.HasQueue);
    }

    /// <summary>RemoveCommand removes a single entry from the queue.</summary>
    [Fact]
    public void RemoveCommand_RemovesEntry()
    {
        StagedSendViewModel vm = new Setup().Build();
        vm.Enqueue([MakeSend("A"), MakeSend("B")], StagedSendMode.Sequential, null);

        vm.RemoveCommand.Execute(vm.Queue[0]);

        Assert.Equal(["B"], vm.Queue.Select(e => e.Preview));
    }

    /// <summary>ClearCommand removes every entry from the queue.</summary>
    [Fact]
    public void ClearCommand_RemovesEveryEntry()
    {
        StagedSendViewModel vm = new Setup().Build();
        vm.Enqueue([MakeSend("A"), MakeSend("B")], StagedSendMode.Sequential, null);

        vm.ClearCommand.Execute(null);

        Assert.Empty(vm.Queue);
        Assert.False(vm.HasQueue);
    }

    /// <summary>SendAllCommand cannot execute with an empty queue.</summary>
    [Fact]
    public void SendAllCommand_EmptyQueue_CannotExecute()
    {
        StagedSendViewModel vm = new Setup().Build();

        Assert.False(vm.SendAllCommand.CanExecute(null));
    }

    /// <summary>A successful send-all marks every entry Sent, stores each as a sent message, and reports the outcome.</summary>
    [Fact]
    public async Task SendAllCommand_Success_MarksSentAndStoresEachMessage()
    {
        Setup s = new();
        s.Connection
            .Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResult { MessageId = "M1", UserResults = [new UserDeliveryResult { UserName = "Bob", Success = true, AddressedVia = [] }] });
        StagedSendViewModel vm = s.Build();
        vm.Enqueue([MakeSend("A"), MakeSend("B")], StagedSendMode.Sequential, null);

        await vm.SendAllCommand.ExecuteAsync(null);

        Assert.All(vm.Queue, e => Assert.Equal(StagedSendStatus.Sent, e.Status));
        Assert.Equal("Sent 2 of 2", vm.StatusMessage);
        s.EntryService.Verify(e => e.StoreSentMessage(
            "M1", It.IsAny<string>(), It.IsAny<List<AddressData>>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyList<UserDeliveryResult>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    /// <summary>Sequential mode sends entries one at a time, in queue order.</summary>
    [Fact]
    public async Task SendAllCommand_Sequential_SendsInQueueOrder()
    {
        Setup s = new();
        List<string> order = [];
        s.Connection
            .Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, List<AddressRequest>, bool, int, string, string, CancellationToken>((body, _, _, _, _, _, _) =>
            {
                order.Add(body);
                return Task.FromResult<SendMessageResult?>(new SendMessageResult { MessageId = "M", UserResults = [] });
            });
        StagedSendViewModel vm = s.Build();
        vm.Enqueue([MakeSend("A"), MakeSend("B"), MakeSend("C")], StagedSendMode.Sequential, null);

        await vm.SendAllCommand.ExecuteAsync(null);

        Assert.Equal(["A", "B", "C"], order);
    }

    /// <summary>Simultaneous mode sends every entry without waiting for the others to finish first.</summary>
    [Fact]
    public async Task SendAllCommand_Simultaneous_SendsEveryEntry()
    {
        Setup s = new();
        s.Connection
            .Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResult { MessageId = "M", UserResults = [] });
        StagedSendViewModel vm = s.Build();
        vm.Enqueue([MakeSend("A"), MakeSend("B")], StagedSendMode.Simultaneous, null);

        await vm.SendAllCommand.ExecuteAsync(null);

        Assert.All(vm.Queue, e => Assert.Equal(StagedSendStatus.Sent, e.Status));
        s.Connection.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>A send that returns null (no installed user) marks that entry Failed with an explanatory message.</summary>
    [Fact]
    public async Task SendAllCommand_SendMessageReturnsNull_MarksFailed()
    {
        Setup s = new();
        s.Connection
            .Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendMessageResult?)null);
        StagedSendViewModel vm = s.Build();
        vm.Enqueue([MakeSend("A")], StagedSendMode.Sequential, null);

        await vm.SendAllCommand.ExecuteAsync(null);

        StagedSendEntry entry = Assert.Single(vm.Queue);
        Assert.Equal(StagedSendStatus.Failed, entry.Status);
        Assert.Equal("Cannot send until a user is installed", entry.StatusMessage);
    }

    /// <summary>A send that throws marks that entry Failed with the exception message, without stopping the rest of the batch.</summary>
    [Fact]
    public async Task SendAllCommand_SendMessageThrows_MarksFailedAndContinuesBatch()
    {
        Setup s = new();
        s.Connection
            .SetupSequence(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk error"))
            .ReturnsAsync(new SendMessageResult { MessageId = "M", UserResults = [] });
        StagedSendViewModel vm = s.Build();
        vm.Enqueue([MakeSend("A"), MakeSend("B")], StagedSendMode.Sequential, null);

        await vm.SendAllCommand.ExecuteAsync(null);

        Assert.Equal(StagedSendStatus.Failed, vm.Queue[0].Status);
        Assert.Equal("disk error", vm.Queue[0].StatusMessage);
        Assert.Equal(StagedSendStatus.Sent, vm.Queue[1].Status);
        Assert.Equal("Sent 1 of 2", vm.StatusMessage);
    }

    /// <summary>After every entry is sent, SendAllCommand can no longer execute since none remain Pending.</summary>
    [Fact]
    public async Task SendAllCommand_AfterAllSent_CanNoLongerExecute()
    {
        Setup s = new();
        s.Connection
            .Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResult { MessageId = "M", UserResults = [] });
        StagedSendViewModel vm = s.Build();
        vm.Enqueue([MakeSend("A")], StagedSendMode.Sequential, null);

        await vm.SendAllCommand.ExecuteAsync(null);

        Assert.False(vm.SendAllCommand.CanExecute(null));
    }
}
