namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>
/// ViewModel interface tracking pending (unread) alert messages and driving the title bar's alarm box
/// and sound. See <see cref="Control.IEngineController.GetIsAlert"/> and <c>Docs/ViewModels.md</c>.
/// </summary>
public interface IAlertViewModel
{
    /// <summary>Gets a value indicating whether one or more alert messages are pending (unread).</summary>
    bool IsAlerting { get; }
    /// <summary>Gets the number of pending (unread) alert messages.</summary>
    int PendingCount { get; }
    /// <summary>Gets the text to display in the title bar's alert box.</summary>
    string AlertText { get; }
    /// <summary>Gets a value indicating whether click/keyboard quick confirmation is enabled.</summary>
    bool QuickConfirmationEnabled { get; }
    /// <summary>Confirms (marks read) the most recently received pending alert, if any and if enabled.</summary>
    IAsyncRelayCommand ConfirmLatestCommand { get; }
}

/// <summary>
/// Tracks pending (unread) alert messages and drives the title bar's alarm box and sound. Subscribes to
/// <see cref="IEntryService.MessageInserted"/>/<see cref="IEntryService.MessageRead"/> so it reflects
/// alerts regardless of whether they are read by opening the message normally or via quick confirmation.
/// </summary>
public sealed partial class AlertViewModel : ObservableObject, IAlertViewModel
{
    /// <summary>Initializes a new <see cref="AlertViewModel"/> and subscribes to entry read/insert events.</summary>
    /// <param name="entryService">Entry service raising the insert/read events that drive the pending list.</param>
    /// <param name="connection">Service connection used to mark an alert read via quick confirmation.</param>
    /// <param name="engineController">Maps logical fields onto a message entity's stored message; provides alert box text, alarm sound duration, and quick-confirmation setting.</param>
    /// <param name="soundPlayer">Plays and stops the alarm sound.</param>
    public AlertViewModel(
        IEntryService entryService,
        IServiceConnection connection,
        IEngineController engineController,
        IAlertSoundPlayer soundPlayer)
    {
        this.entryService = entryService;
        this.connection = connection;
        this.engineController = engineController;
        this.soundPlayer = soundPlayer;

        entryService.MessageInserted += OnMessageInserted;
        entryService.MessageRead += OnMessageRead;
    }

    private readonly IEntryService entryService;
    private readonly IServiceConnection connection;
    private readonly IEngineController engineController;
    private readonly IAlertSoundPlayer soundPlayer;
    private readonly List<string> pending = [];
    private Timer? soundTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlerting))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmLatestCommand))]
    private int pendingCount;

    /// <inheritdoc />
    public bool IsAlerting => PendingCount > 0;
    /// <inheritdoc />
    public string AlertText => engineController.AlertLabel;
    /// <inheritdoc />
    public bool QuickConfirmationEnabled => engineController.QuickConfirmationEnabled;

    private Task OnMessageInserted(MessageEntity entity)
    {
        if (!engineController.GetIsAlert(entity.Message))
        {
            return Task.CompletedTask;
        }

        lock (pending) pending.Add(entity.MessageId);
        PendingCount = pending.Count;

        soundPlayer.Play();
        ResetSoundTimer();
        return Task.CompletedTask;
    }

    private Task OnMessageRead(MessageEntity entity)
    {
        bool removed;
        lock (pending) removed = pending.Remove(entity.MessageId);
        if (!removed) { return Task.CompletedTask; }

        PendingCount = pending.Count;
        if (PendingCount == 0)
        {
            soundTimer?.Dispose();
            soundTimer = null;
            soundPlayer.Stop();
        }
        return Task.CompletedTask;
    }

    private void ResetSoundTimer()
    {
        TimeSpan duration = engineController.AlarmSoundDuration;
        if (soundTimer is null)
        {
            soundTimer = new Timer(_ => soundPlayer.Stop(), null, duration, Timeout.InfiniteTimeSpan);
        }
        else
        {
            soundTimer.Change(duration, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Confirms (marks read) the most recently received pending alert, if any and if enabled.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirmLatest))]
    private async Task ConfirmLatest()
    {
        string? latest;
        lock (pending) latest = pending.Count > 0 ? pending[^1] : null;
        if (latest is null) { return; }
        await connection.MarkMessageRead(latest);
    }

    private bool CanConfirmLatest() => IsAlerting && QuickConfirmationEnabled;
}
