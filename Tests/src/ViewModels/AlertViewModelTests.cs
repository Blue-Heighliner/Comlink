namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="AlertViewModel"/>.</summary>
public sealed class AlertViewModelTests
{
    private static readonly IMessageFormat Format = new TestMessageFormat();

    private sealed class Setup
    {
        public Mock<IEntryService> EntryService { get; } = new();
        public Mock<IServiceConnection> Connection { get; } = new();
        public Mock<IAlertSoundPlayer> SoundPlayer { get; } = new();
        public Mock<IAlertConfiguration> Configuration { get; } = new();

        public Setup()
        {
            Configuration.Setup(c => c.AlertText).Returns("ALERT");
            Configuration.Setup(c => c.AlarmSoundDuration).Returns(TimeSpan.FromMinutes(10));
            Configuration.Setup(c => c.QuickConfirmationEnabled).Returns(true);
        }

        public AlertViewModel Build() =>
            new(EntryService.Object, Connection.Object, Format, SoundPlayer.Object, Configuration.Object);
    }

    private static MessageEntity MakeMessage(string messageId, bool isAlert)
    {
        object message = Format.CreateMessage();
        Format.SetMessageId(message, messageId);
        Format.SetIsAlert(message, isAlert);
        return new MessageEntity { MessageId = messageId, Message = message };
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    /// <summary>A freshly constructed ViewModel has no pending alerts.</summary>
    [Fact]
    public void Ctor_InitialState_NotAlerting()
    {
        AlertViewModel vm = new Setup().Build();

        Assert.False(vm.IsAlerting);
        Assert.Equal(0, vm.PendingCount);
    }

    /// <summary>AlertText and QuickConfirmationEnabled are read from IAlertConfiguration.</summary>
    [Fact]
    public void Ctor_ExposesConfigurationValues()
    {
        Setup s = new();
        s.Configuration.Setup(c => c.AlertText).Returns("INCOMING");
        s.Configuration.Setup(c => c.QuickConfirmationEnabled).Returns(false);

        AlertViewModel vm = s.Build();

        Assert.Equal("INCOMING", vm.AlertText);
        Assert.False(vm.QuickConfirmationEnabled);
    }

    // ── MessageInserted ───────────────────────────────────────────────────────

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

    // ── MessageRead ───────────────────────────────────────────────────────────

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

    // ── ConfirmLatestCommand ──────────────────────────────────────────────────

    /// <summary>ConfirmLatestCommand cannot execute while no alerts are pending.</summary>
    [Fact]
    public void ConfirmLatestCommand_NoPending_CannotExecute()
    {
        AlertViewModel vm = new Setup().Build();

        Assert.False(vm.ConfirmLatestCommand.CanExecute(null));
    }

    /// <summary>ConfirmLatestCommand cannot execute when quick confirmation is disabled, even with a pending alert.</summary>
    [Fact]
    public void ConfirmLatestCommand_QuickConfirmationDisabled_CannotExecute()
    {
        Setup s = new();
        s.Configuration.Setup(c => c.QuickConfirmationEnabled).Returns(false);
        AlertViewModel vm = s.Build();
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));

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
