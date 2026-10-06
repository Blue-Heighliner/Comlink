namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="AlertViewModel"/>.</summary>
public sealed class AlertViewModelTests
{
    private static readonly IEngineController format = new TestEngineController();

    private sealed class Setup
    {
        public Setup()
        {
            AlertSettings.Setup(c => c.AlertLabel).Returns("ALERT");
            AlertSettings.Setup(c => c.AlarmSoundDuration).Returns(TimeSpan.FromMinutes(10));
        }

        public Mock<IEntryService> EntryService { get; } = new();
        public Mock<IServiceConnection> Connection { get; } = new();
        public Mock<TestEngineController> AlertSettings { get; } = new() { CallBase = true };
        public Mock<IAlertSoundPlayer> SoundPlayer { get; } = new();

        public AlertViewModel Build()
            => new(EntryService.Object, Connection.Object, AlertSettings.Object, SoundPlayer.Object);
    }

    private static MessageEntity MakeMessage(string messageId, bool isAlert)
    {
        object message = format.CreateFrame();
        ((TestFrame)message).MessageId = messageId;
        ((TestFrame)message).IsAlert = isAlert;
        return new MessageEntity { MessageId = messageId, Message = message };
    }

    /// <summary>A freshly constructed ViewModel has no pending alerts.</summary>
    [Fact]
    public void Ctor_InitialState_NotAlerting()
    {
        AlertViewModel vm = new Setup().Build();

        Assert.False(vm.IsAlerting);
        Assert.Equal(0, vm.PendingCount);
    }

    /// <summary>AlertText and ConfirmationKeys are read from IEngineController.</summary>
    [Fact]
    public void Ctor_ExposesConfigurationValues()
    {
        Setup s = new();
        s.AlertSettings.Setup(c => c.AlertLabel).Returns("INCOMING");
        s.AlertSettings.Setup(c => c.AlertConfirmationKeys).Returns(["F5"]);

        AlertViewModel vm = s.Build();

        Assert.Equal("INCOMING", vm.AlertText);
        Assert.Equal(["F5"], vm.ConfirmationKeys);
    }

    /// <summary>An inserted alert message becomes pending, starts alarming, and plays the sound.</summary>
    [Fact]
    public void MessageInserted_AlertMessage_BecomesPendingAndPlaysSound()
    {
        Setup s = new();
        AlertViewModel vm = s.Build();

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));

        Assert.True(vm.IsAlerting);
        Assert.Equal(1, vm.PendingCount);
        s.SoundPlayer.Verify(p => p.Play(), Times.Once);
    }

    /// <summary>An inserted non-alert message is ignored.</summary>
    [Fact]
    public void MessageInserted_NonAlertMessage_IsIgnored()
    {
        Setup s = new();
        AlertViewModel vm = s.Build();

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: false));

        Assert.False(vm.IsAlerting);
        Assert.Equal(0, vm.PendingCount);
        s.SoundPlayer.Verify(p => p.Play(), Times.Never);
    }

    /// <summary>A second alert message increments the pending count without stopping the sound.</summary>
    [Fact]
    public void MessageInserted_SecondAlert_IncrementsPendingCount()
    {
        Setup s = new();
        AlertViewModel vm = s.Build();

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG2", isAlert: true));

        Assert.Equal(2, vm.PendingCount);
        s.SoundPlayer.Verify(p => p.Stop(), Times.Never);
    }

    /// <summary>Reading the only pending alert clears IsAlerting and stops the sound.</summary>
    [Fact]
    public void MessageRead_LastPendingAlert_StopsAlarmingAndSound()
    {
        Setup s = new();
        AlertViewModel vm = s.Build();
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));

        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("MSG1", isAlert: true));

        Assert.False(vm.IsAlerting);
        Assert.Equal(0, vm.PendingCount);
        s.SoundPlayer.Verify(p => p.Stop(), Times.Once);
    }

    /// <summary>Reading one of two pending alerts decrements the count but keeps alarming.</summary>
    [Fact]
    public void MessageRead_OneOfTwoPending_KeepsAlarming()
    {
        Setup s = new();
        AlertViewModel vm = s.Build();
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG2", isAlert: true));

        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("MSG1", isAlert: true));

        Assert.True(vm.IsAlerting);
        Assert.Equal(1, vm.PendingCount);
        s.SoundPlayer.Verify(p => p.Stop(), Times.Never);
    }

    /// <summary>Reading a message that was never pending (e.g. a non-alert message) is a no-op.</summary>
    [Fact]
    public void MessageRead_NotPending_IsNoOp()
    {
        Setup s = new();
        AlertViewModel vm = s.Build();

        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("MSG-OTHER", isAlert: false));

        Assert.False(vm.IsAlerting);
        Assert.Equal(0, vm.PendingCount);
    }

    /// <summary>ConfirmLatestCommand cannot execute while no alerts are pending.</summary>
    [Fact]
    public void ConfirmLatestCommand_NoPending_CannotExecute()
    {
        AlertViewModel vm = new Setup().Build();

        Assert.False(vm.ConfirmLatestCommand.CanExecute(null));
    }

    /// <summary>Executing ConfirmLatestCommand marks the most recently received pending alert read via the connection.</summary>
    [Fact]
    public async Task ConfirmLatestCommand_Execute_MarksMostRecentPendingAlertRead()
    {
        Setup s = new();
        s.Connection.Setup(c => c.MarkMessageRead(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        AlertViewModel vm = s.Build();
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG2", isAlert: true));

        Assert.True(vm.ConfirmLatestCommand.CanExecute(null));
        await vm.ConfirmLatestCommand.ExecuteAsync(null);

        s.Connection.Verify(c => c.MarkMessageRead("MSG2", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Pressing confirm repeatedly confirms each pending alert, most-recent-first, until none remain.</summary>
    [Fact]
    public async Task ConfirmLatestCommand_ExecutedForEachPending_ConfirmsAllMostRecentFirst()
    {
        Setup s = new();
        s.Connection.Setup(c => c.MarkMessageRead(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        AlertViewModel vm = s.Build();
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG2", isAlert: true));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG3", isAlert: true));

        // Each press confirms one; the ViewModel's own pending count only actually drops once
        // EntryService reports the read back via its MessageRead event (mirroring production wiring).
        await vm.ConfirmLatestCommand.ExecuteAsync(null);
        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("MSG3", isAlert: true));

        await vm.ConfirmLatestCommand.ExecuteAsync(null);
        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("MSG2", isAlert: true));

        await vm.ConfirmLatestCommand.ExecuteAsync(null);
        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("MSG1", isAlert: true));

        s.Connection.Verify(c => c.MarkMessageRead("MSG3", It.IsAny<CancellationToken>()), Times.Once);
        s.Connection.Verify(c => c.MarkMessageRead("MSG2", It.IsAny<CancellationToken>()), Times.Once);
        s.Connection.Verify(c => c.MarkMessageRead("MSG1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(vm.IsAlerting);
    }
}
