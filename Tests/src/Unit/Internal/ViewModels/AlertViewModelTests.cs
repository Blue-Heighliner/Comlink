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
        public Mock<TestEngineController> AlertSettings { get; } = new() { CallBase = true };
        public Mock<IAlertSoundPlayer> SoundPlayer { get; } = new();

        public AlertViewModel Build()
            => new(EntryService.Object, AlertSettings.Object, SoundPlayer.Object);
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

    /// <summary>AlertText and QuickReadKeys are read from IEngineController.</summary>
    [Fact]
    public void Ctor_ExposesConfigurationValues()
    {
        Setup s = new();
        s.AlertSettings.Setup(c => c.AlertLabel).Returns("INCOMING");
        s.AlertSettings.Setup(c => c.AlertQuickReadKeys).Returns(["F5"]);

        AlertViewModel vm = s.Build();

        Assert.Equal("INCOMING", vm.AlertText);
        Assert.Equal(["F5"], vm.QuickReadKeys);
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

    /// <summary>OpenOldestCommand cannot execute while no alerts are unread.</summary>
    [Fact]
    public void OpenOldestCommand_NoPending_CannotExecute()
    {
        AlertViewModel vm = new Setup().Build();

        Assert.False(vm.OpenOldestCommand.CanExecute(null));
    }

    /// <summary>OpenOldestCommand asks for the oldest unread alert to be opened, and keeps doing so until it has been read.</summary>
    [Fact]
    public async Task OpenOldestCommand_Execute_RequestsTheOldestUnreadAlert()
    {
        Setup s = new();
        AlertViewModel vm = s.Build();
        List<string> opened = [];
        vm.OpenRequested += id =>
        {
            opened.Add(id);
            return Task.CompletedTask;
        };
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG2", isAlert: true));

        Assert.True(vm.OpenOldestCommand.CanExecute(null));
        await vm.OpenOldestCommand.ExecuteAsync(null);
        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("MSG1", isAlert: true));
        await vm.OpenOldestCommand.ExecuteAsync(null);

        Assert.Equal(["MSG1", "MSG2"], opened);
    }

    /// <summary>Another alert while the alarm sounds starts the time again, so the sound outlasts the first alert's duration.</summary>
    [Fact]
    public async Task MessageInserted_WhileSounding_RestartsTheDuration()
    {
        Setup s = new();
        s.AlertSettings.Setup(c => c.AlarmSoundDuration).Returns(TimeSpan.FromMilliseconds(400));
        s.Build();

        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG1", isAlert: true));
        await Task.Delay(250);
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("MSG2", isAlert: true));
        await Task.Delay(250);

        s.SoundPlayer.Verify(p => p.Stop(), Times.Never);

        await Task.Delay(400);
        s.SoundPlayer.Verify(p => p.Stop(), Times.Once);
    }

    /// <summary>The alarm stops early only once every alert that set it off has been read, however many other alerts are still unread.</summary>
    [Fact]
    public async Task MessageRead_AllAlertsOfTheCurrentAlarm_StopsEarlyEvenWithOlderUnread()
    {
        Setup s = new();
        s.AlertSettings.Setup(c => c.AlarmSoundDuration).Returns(TimeSpan.FromMilliseconds(150));
        AlertViewModel vm = s.Build();
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("OLD", isAlert: true));
        await Task.Delay(400);
        s.SoundPlayer.Verify(p => p.Stop(), Times.Once);

        s.AlertSettings.Setup(c => c.AlarmSoundDuration).Returns(TimeSpan.FromMinutes(10));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("NEW1", isAlert: true));
        s.EntryService.Raise(e => e.MessageInserted += null, MakeMessage("NEW2", isAlert: true));
        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("NEW1", isAlert: true));
        s.SoundPlayer.Verify(p => p.Stop(), Times.Once);

        s.EntryService.Raise(e => e.MessageRead += null, MakeMessage("NEW2", isAlert: true));

        s.SoundPlayer.Verify(p => p.Stop(), Times.Exactly(2));
        Assert.Equal(1, vm.PendingCount);
        Assert.True(vm.IsAlerting);
    }
}
