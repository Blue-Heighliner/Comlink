namespace BlueHeighliner.Comlink;

/// <summary>
/// ViewModel interface tracking unread alert messages and driving the title bar's alert indicator and the alarm sound.
/// See <see cref="Message.IsAlert"/> and <c>Docs/Components/ViewModels.md</c>.
/// </summary>
internal interface IAlertViewModel
{
    /// <summary>Raised with the identifier of the oldest unread alert when <see cref="OpenOldestCommand"/> runs; whatever shows messages opens it, which reads it.</summary>
    event Func<string, Task>? OpenRequested;

    /// <summary>Gets a value indicating whether one or more alert messages are unread, which shows the indicator in the title bar.</summary>
    bool IsAlerting { get; }
    /// <summary>Gets the number of unread alert messages.</summary>
    int PendingCount { get; }
    /// <summary>Gets the text to display in the title bar's alert indicator.</summary>
    string AlertText { get; }
    /// <summary>Gets the names of the keys that open the oldest unread alert: Space and Enter.</summary>
    IReadOnlyList<string> QuickReadKeys { get; }
    /// <summary>Opens the oldest unread alert, if any, which reads it.</summary>
    IAsyncRelayCommand OpenOldestCommand { get; }
}

/// <summary>
/// Tracks unread alert messages and drives the title bar's alert indicator and the alarm sound. Subscribes to
/// <see cref="IEntryService.MessageInserted"/>/<see cref="IEntryService.MessageRead"/> so it reflects
/// alerts however they are read. The alarm sounds for the alarm handler's alert duration, starting the time again when another alert arrives while it sounds,
/// and stops early once every alert that set off the current alarm has been read.
/// </summary>
internal sealed partial class AlertViewModel : ObservableObject, IAlertViewModel
{
    /// <summary>Initializes a new <see cref="AlertViewModel"/> and subscribes to entry read/insert events.</summary>
    /// <param name="entryService">Entry service raising the insert/read events that drive the unread list.</param>
    /// <param name="engineController">Maps logical fields onto a message entity's stored message; provides the indicator text, the alarm sound duration and the quick read keys.</param>
    /// <param name="soundPlayer">Plays and stops the alarm sound.</param>
    public AlertViewModel(
        IEntryService entryService,
        IEngineController engineController,
        IAlertSoundPlayer soundPlayer)
    {
        this.entryService = entryService;
        this.engineController = engineController;
        this.soundPlayer = soundPlayer;

        entryService.MessageInserted += OnMessageInserted;
        entryService.MessageRead += OnMessageRead;
    }

    private readonly IEntryService entryService;
    private readonly IEngineController engineController;
    private readonly IAlertSoundPlayer soundPlayer;
    private readonly List<string> pending = [];
    private readonly HashSet<string> alarming = [];
    private Timer? soundTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlerting))]
    [NotifyCanExecuteChangedFor(nameof(OpenOldestCommand))]
    private int pendingCount;

    /// <inheritdoc />
    public event Func<string, Task>? OpenRequested;

    /// <inheritdoc />
    public bool IsAlerting => PendingCount > 0;
    /// <inheritdoc />
    public string AlertText => engineController.AlertLabel;
    /// <inheritdoc />
    public IReadOnlyList<string> QuickReadKeys { get; } = ["Space", "Enter"];

    private Task OnMessageInserted(MessageEntity entity)
    {
        if (!entity.Message.IsAlert)
        {
            return Task.CompletedTask;
        }

        int count;
        lock (pending)
        {
            pending.Add(entity.MessageId);
            alarming.Add(entity.MessageId);
            count = pending.Count;
            ResetSoundTimer();
        }

        PendingCount = count;
        soundPlayer.Play();
        return Task.CompletedTask;
    }

    private Task OnMessageRead(MessageEntity entity)
    {
        int count;
        bool silence = false;
        lock (pending)
        {
            if (!pending.Remove(entity.MessageId))
            {
                return Task.CompletedTask;
            }

            count = pending.Count;
            if (alarming.Remove(entity.MessageId) && alarming.Count == 0 && soundTimer is not null)
            {
                soundTimer.Dispose();
                soundTimer = null;
                silence = true;
            }
        }

        PendingCount = count;
        if (silence)
        {
            soundPlayer.Stop();
        }
        return Task.CompletedTask;
    }

    private void ResetSoundTimer()
    {
        TimeSpan duration = engineController.AlarmSoundDuration;
        if (soundTimer is null)
        {
            soundTimer = new Timer(_ => EndAlarm(), null, duration, Timeout.InfiniteTimeSpan);
        }
        else
        {
            soundTimer.Change(duration, Timeout.InfiniteTimeSpan);
        }
    }

    private void EndAlarm()
    {
        lock (pending)
        {
            soundTimer?.Dispose();
            soundTimer = null;
            alarming.Clear();
        }

        soundPlayer.Stop();
    }

    /// <summary>Opens the oldest unread alert, if any.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenOldest))]
    private async Task OpenOldest()
    {
        string? oldest;
        lock (pending)
        {
            oldest = pending.Count > 0 ? pending[0] : null;
        }

        if (oldest is null || OpenRequested is null)
        {
            return;
        }
        await OpenRequested.InvokeAll(oldest);
    }

    private bool CanOpenOldest() => IsAlerting;
}
