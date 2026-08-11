namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>
/// ViewModel interface tracking pending (unread) alert messages and driving the title bar's alarm box
/// and sound. See <see cref="Control.IMessageFormat.GetIsAlert"/> and <c>Docs/ViewModels.md</c>.
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
    private readonly IEntryService _entryService;
    private readonly IServiceConnection _connection;
    private readonly IMessageFormat _messageFormat;
    private readonly IAlertSoundPlayer _soundPlayer;
    private readonly IAlertConfiguration _configuration;
    private readonly List<string> _pending = [];
    private Timer? _soundTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlerting))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmLatestCommand))]
    private int _pendingCount;

    /// <inheritdoc />
    public bool IsAlerting => PendingCount > 0;
    /// <inheritdoc />
    public string AlertText => _configuration.AlertText;
    /// <inheritdoc />
    public bool QuickConfirmationEnabled => _configuration.QuickConfirmationEnabled;

    /// <summary>Initializes a new <see cref="AlertViewModel"/> and subscribes to entry read/insert events.</summary>
    /// <param name="entryService">Entry service raising the insert/read events that drive the pending list.</param>
    /// <param name="connection">Service connection used to mark an alert read via quick confirmation.</param>
    /// <param name="messageFormat">Maps logical fields onto a message entity's stored message.</param>
    /// <param name="soundPlayer">Plays and stops the alarm sound.</param>
    /// <param name="configuration">Provides alert box text, alarm sound duration, and quick-confirmation setting.</param>
    public AlertViewModel(
        IEntryService entryService,
        IServiceConnection connection,
        IMessageFormat messageFormat,
        IAlertSoundPlayer soundPlayer,
        IAlertConfiguration configuration)
    {
        _entryService = entryService;
        _connection = connection;
        _messageFormat = messageFormat;
        _soundPlayer = soundPlayer;
        _configuration = configuration;

        entryService.MessageInserted += OnMessageInserted;
        entryService.MessageRead += OnMessageRead;
    }

    private Task OnMessageInserted(MessageEntity entity)
    {
        if (!_messageFormat.GetIsAlert(entity.Message))
            return Task.CompletedTask;

        lock (_pending) _pending.Add(entity.MessageId);
        PendingCount = _pending.Count;

        _soundPlayer.Play();
        ResetSoundTimer();
        return Task.CompletedTask;
    }

    private Task OnMessageRead(MessageEntity entity)
    {
        bool removed;
        lock (_pending) removed = _pending.Remove(entity.MessageId);
        if (!removed) return Task.CompletedTask;

        PendingCount = _pending.Count;
        if (PendingCount == 0)
        {
            _soundTimer?.Dispose();
            _soundTimer = null;
            _soundPlayer.Stop();
        }
        return Task.CompletedTask;
    }

    private void ResetSoundTimer()
    {
        TimeSpan duration = _configuration.AlarmSoundDuration;
        if (_soundTimer is null)
            _soundTimer = new Timer(_ => _soundPlayer.Stop(), null, duration, Timeout.InfiniteTimeSpan);
        else
            _soundTimer.Change(duration, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Confirms (marks read) the most recently received pending alert, if any and if enabled.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirmLatest))]
    private async Task ConfirmLatest()
    {
        string? latest;
        lock (_pending) latest = _pending.Count > 0 ? _pending[^1] : null;
        if (latest is null) return;
        await _connection.MarkMessageRead(latest);
    }

    private bool CanConfirmLatest() => IsAlerting && QuickConfirmationEnabled;
}
