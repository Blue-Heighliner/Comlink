namespace BlueHeighliner.Comlink;

/// <summary>How far a <see cref="StagedSendEntry"/> has gotten toward being sent.</summary>
internal enum StagedSendStatus
{
    /// <summary>Not yet sent; waiting for <see cref="IStagedSendViewModel.SendAllCommand"/>.</summary>
    Pending,
    /// <summary>Currently being sent.</summary>
    Sending,
    /// <summary>Sent successfully.</summary>
    Sent,
    /// <summary>The send failed; see <see cref="StagedSendEntry.StatusMessage"/>.</summary>
    Failed
}

/// <summary>A single message a custom import format staged, next-to-send ordering handled by <see cref="IStagedSendViewModel"/>.</summary>
internal sealed record StagedSendEntry
{
    /// <summary>Unique identifier for this queue entry.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the first line of the message body, shown for the entry in the queue.</summary>
    public string Preview => Body.FirstLine;
    /// <summary>Message body text.</summary>
    public required string Body { get; init; }
    /// <summary>Recipient addresses for this send.</summary>
    public required List<AddressRequest> Addresses { get; init; }
    /// <summary>Whether this message will be sent as an alert.</summary>
    public bool IsAlert { get; init; }
    /// <summary>Priority number this message will be sent at.</summary>
    public int Priority { get; init; }
    /// <summary>Tag identifying the type of this message.</summary>
    public string Tag { get; init; } = string.Empty;
    /// <summary>Security level name this message will be sent at.</summary>
    public string SecurityLevel { get; init; } = string.Empty;
    /// <summary>How far this entry has gotten toward being sent.</summary>
    public StagedSendStatus Status { get; init; } = StagedSendStatus.Pending;
    /// <summary>The failure reason, when <see cref="StagedSendEntry.Status"/> is <see cref="StagedSendStatus.Failed"/>; otherwise <see langword="null"/>.</summary>
    public string? StatusMessage { get; init; }
}

/// <summary>
/// ViewModel interface for the staged send screen: every message a custom import format has prepared (see
/// <see cref="IEngineBuilder.ImportFormat{TFormat}"/>), reviewed by the user and sent only once they press
/// <see cref="SendAllCommand"/>. Registered as a DI singleton (see <see cref="MainViewModel.StagedSend"/>) so
/// staged sends added by one import, and the progress of a send-all in flight, survive navigating the content
/// area away to other views and back.
/// </summary>
internal interface IStagedSendViewModel
{
    /// <summary>Gets the current staged send queue, in the order entries were added.</summary>
    ObservableCollection<StagedSendEntry> Queue { get; }
    /// <summary>Gets a value indicating whether <see cref="Queue"/> is non-empty.</summary>
    bool HasQueue { get; }
    /// <summary>Gets a value indicating whether <see cref="SendAllCommand"/> is currently running.</summary>
    bool IsSending { get; }
    /// <summary>Gets the status message displayed after (or during) a send-all attempt.</summary>
    string? StatusMessage { get; }
    /// <summary>Sends every <see cref="StagedSendStatus.Pending"/> entry, using the mode and delay from whichever import last added to the queue.</summary>
    IAsyncRelayCommand SendAllCommand { get; }
    /// <summary>Removes a single entry from the queue.</summary>
    IRelayCommand<StagedSendEntry> RemoveCommand { get; }
    /// <summary>Removes every entry from the queue.</summary>
    IRelayCommand ClearCommand { get; }

    /// <summary>
    /// Adds every send in <paramref name="sends"/> to the queue as <see cref="StagedSendStatus.Pending"/>, and
    /// remembers <paramref name="mode"/>/<paramref name="delay"/> as how <see cref="SendAllCommand"/> processes
    /// the whole queue next.
    /// </summary>
    void Enqueue(IReadOnlyList<StagedSendData> sends, StagedSendMode mode, TimeSpan? delay);
}

/// <inheritdoc cref="IStagedSendViewModel" />
internal sealed partial class StagedSendViewModel : ObservableObject, IStagedSendViewModel
{
    /// <summary>Initializes a new <see cref="StagedSendViewModel"/> with the connection and entry service needed to actually send and record each staged message.</summary>
    /// <param name="connection">Service connection used to send each staged message.</param>
    /// <param name="entryService">Entry service used to persist each sent message to the Outbox.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    public StagedSendViewModel(IServiceConnection connection, IEntryService entryService, ILoggerFactory loggerFactory)
    {
        this.connection = connection;
        this.entryService = entryService;
        activityLogger = loggerFactory.CreateLogger("ACTIVITY");
        Queue.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasQueue));
    }

    private readonly IServiceConnection connection;
    private readonly IEntryService entryService;
    private readonly ILogger activityLogger;
    private readonly List<StagedSendEntry> queue = [];
    private readonly Lock gate = new();
    private StagedSendMode mode = StagedSendMode.Sequential;
    private TimeSpan? delay;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendAllCommand))]
    private bool isSending;

    [ObservableProperty] private string? statusMessage;

    /// <inheritdoc />
    public ObservableCollection<StagedSendEntry> Queue { get; } = [];

    /// <inheritdoc />
    public bool HasQueue => Queue.Count > 0;

    /// <inheritdoc />
    public void Enqueue(IReadOnlyList<StagedSendData> sends, StagedSendMode mode, TimeSpan? delay)
    {
        this.mode = mode;
        this.delay = delay;

        lock (gate)
        {
            foreach (StagedSendData send in sends)
            {
                queue.Add(new StagedSendEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Body = send.Body,
                    Addresses = send.Addresses,
                    IsAlert = send.IsAlert,
                    Priority = send.Priority,
                    Tag = send.Tag,
                    SecurityLevel = send.SecurityLevel
                });
            }
        }

        RefreshQueueDisplay();
    }

    [RelayCommand]
    private void Remove(StagedSendEntry entry)
    {
        lock (gate)
        {
            queue.RemoveAll(e => e.Id == entry.Id);
        }

        RefreshQueueDisplay();
    }

    [RelayCommand]
    private void Clear()
    {
        lock (gate)
        {
            queue.Clear();
        }

        RefreshQueueDisplay();
    }

    [RelayCommand(CanExecute = nameof(CanSendAll))]
    private async Task SendAll()
    {
        List<StagedSendEntry> pending;
        lock (gate)
        {
            pending = queue.Where(e => e.Status == StagedSendStatus.Pending).ToList();
        }

        if (pending.Count == 0) { return; }

        IsSending = true;
        StatusMessage = null;
        try
        {
            if (mode == StagedSendMode.Simultaneous)
            {
                await Task.WhenAll(pending.Select(SendOne));
            }
            else
            {
                foreach (StagedSendEntry entry in pending)
                {
                    await SendOne(entry);
                    if (delay is { } d && d > TimeSpan.Zero) { await Task.Delay(d); }
                }
            }

            int sent = pending.Count(e => GetStatus(e.Id) == StagedSendStatus.Sent);
            StatusMessage = $"Sent {sent} of {pending.Count}";
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSendAll() => !IsSending && HasPending();

    private bool HasPending()
    {
        lock (gate)
        {
            return queue.Any(e => e.Status == StagedSendStatus.Pending);
        }
    }

    private StagedSendStatus GetStatus(string id)
    {
        lock (gate)
        {
            return queue.FirstOrDefault(e => e.Id == id)?.Status ?? StagedSendStatus.Failed;
        }
    }

    private async Task SendOne(StagedSendEntry entry)
    {
        SetStatus(entry.Id, StagedSendStatus.Sending);
        try
        {
            SendMessageResult? result = await connection.SendMessage(
                entry.Body, entry.Addresses, entry.IsAlert, entry.Priority, entry.Tag, entry.SecurityLevel);
            if (result is null)
            {
                SetStatus(entry.Id, StagedSendStatus.Failed, "Cannot send until a user is installed");
                return;
            }

            List<AddressData> addresses = [.. entry.Addresses.Select(a => new AddressData { UserName = a.UserName, Type = a.Type, Information = a.Information })];
            await entryService.StoreSentMessage(
                result.MessageId, entry.Body, addresses, DateTime.UtcNow, result.UserResults,
                entry.IsAlert, entry.Priority, entry.Tag, entry.SecurityLevel);

            SetStatus(entry.Id, StagedSendStatus.Sent);
        }
        catch (Exception ex)
        {
            activityLogger.LogError(ex, "Staged send transmission failed for {Preview}", entry.Preview);
            SetStatus(entry.Id, StagedSendStatus.Failed, ex.Message);
        }
    }

    private void SetStatus(string id, StagedSendStatus status, string? statusMessage = null)
    {
        lock (gate)
        {
            int index = queue.FindIndex(e => e.Id == id);
            if (index < 0) { return; }
            queue[index] = queue[index] with { Status = status, StatusMessage = statusMessage };
        }

        RefreshQueueDisplay();
    }

    private void RefreshQueueDisplay()
    {
        List<StagedSendEntry> snapshot;
        lock (gate)
        {
            snapshot = [.. queue];
        }

        Queue.Clear();
        foreach (StagedSendEntry entry in snapshot)
        {
            Queue.Add(entry);
        }

        SendAllCommand.NotifyCanExecuteChanged();
    }
}
