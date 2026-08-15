namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>A single entry queued for printing, next-to-print ordering handled by <see cref="IPrintManagerViewModel"/>.</summary>
public sealed record PrintQueueEntry
{
    /// <summary>Unique identifier for this specific queue entry (not the underlying entry's own ID — the same entry can be queued more than once).</summary>
    public required string Id { get; init; }
    /// <summary>Identifier of the underlying entry (message ID or LiteDB object-id string), per <see cref="EntryType"/>.</summary>
    public required string EntryId { get; init; }
    /// <summary>The kind of entry this queue entry prints.</summary>
    public required EntryType EntryType { get; init; }
    /// <summary>For <see cref="Data.EntryType.Message"/> entries, disambiguates the Outbox (sent) record from the Inbox (received) record.</summary>
    public bool IsOutboundMessage { get; init; }
    /// <summary>Display title shown in the print queue.</summary>
    public required string Title { get; init; }
    /// <summary>
    /// Whether this entry was explicitly queued by the user (via the entry list's "Print" option) rather than
    /// automatically queued on receipt. Manual entries always print before any automatically-queued one.
    /// </summary>
    public required bool IsManual { get; init; }
    /// <summary>
    /// For automatically-queued received messages, the message's priority value (see
    /// <see cref="Control.IEngineController"/>) — higher prints first among other automatically-queued
    /// entries. Unused (and irrelevant to ordering) for manual entries.
    /// </summary>
    public required int Priority { get; init; }
    /// <summary>UTC timestamp this entry was added to the queue, breaking ties between equal-priority entries (earlier prints first).</summary>
    public required DateTime QueuedAt { get; init; }
    /// <summary>Gets a short display badge: <c>"MANUAL"</c> for a manual print, or <c>"P{Priority}"</c> for an automatically-queued one.</summary>
    public string BadgeText => IsManual ? "MANUAL" : $"P{Priority}";
}

/// <summary>
/// ViewModel interface for the print manager: the print queue, printer selection, and the "print received"
/// toggle. Registered as a DI singleton so its background line-printer loop keeps running, and its state
/// (queue, selected printer, toggle) is preserved, regardless of whether the print manager screen is
/// currently shown.
/// </summary>
public interface IPrintManagerViewModel
{
    /// <summary>Gets the current print queue, ordered with the next entry to print first.</summary>
    ObservableCollection<PrintQueueEntry> Queue { get; }
    /// <summary>
    /// Gets or sets whether every received message is automatically added to the print queue (the number of
    /// copies decided by <see cref="Control.IEngineController"/>). Off by default; see <see cref="Control.IEngineController"/>.
    /// </summary>
    bool PrintReceivedEnabled { get; set; }
    /// <summary>Gets the printers available on this computer; see <see cref="Control.IEngineController"/>.</summary>
    IReadOnlyList<string> AvailablePrinters { get; }
    /// <summary>Gets or sets the printer the queue prints to. Initializes to this computer's default printer.</summary>
    string? SelectedPrinter { get; set; }
    /// <summary>Removes every entry from the print queue, interrupting whichever entry is currently printing.</summary>
    IRelayCommand PurgeCommand { get; }
    /// <summary>Removes a single entry from the print queue, interrupting it first if it is the one currently printing.</summary>
    IRelayCommand<PrintQueueEntry> RemoveCommand { get; }
    /// <summary>Adds <paramref name="entry"/> to the print queue as a manual print — always printed ahead of automatically-queued entries.</summary>
    void EnqueueManual(EntryItemViewModel entry);
}

/// <inheritdoc cref="IPrintManagerViewModel" />
public sealed partial class PrintManagerViewModel : ObservableObject, IPrintManagerViewModel
{
    private static List<string> SplitLines(string? text)
        => [.. (text ?? string.Empty).Replace("\r\n", "\n").Split('\n')];

    private static ObjectId? TryParseObjectId(string id)
    {
        try { return new ObjectId(id); }
        catch { return null; }
    }

    /// <summary>Initializes a new <see cref="PrintManagerViewModel"/> and subscribes to inbound message events.</summary>
    /// <param name="entryService">Entry service raising <see cref="IEntryService.MessageInserted"/> for received messages.</param>
    /// <param name="messages">Repository for loading message content when it reaches the front of the queue.</param>
    /// <param name="drafts">Repository for loading draft content when it reaches the front of the queue.</param>
    /// <param name="notes">Repository for loading note content when it reaches the front of the queue.</param>
    /// <param name="activityLogs">Repository for loading activity log content when it reaches the front of the queue.</param>
    /// <param name="engineController">Maps logical fields onto a message entity's stored message, and provides the starting state of <see cref="PrintReceivedEnabled"/> and how many copies of each received message to auto-queue.</param>
    /// <param name="printDriver">Enumerates available printers and this computer's default, and drives the selected printer line by line.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    public PrintManagerViewModel(
        IEntryService entryService,
        IMessageRepository messages,
        IDraftRepository drafts,
        INoteRepository notes,
        IActivityLogRepository activityLogs,
        IEngineController engineController,
        IPrintDriver printDriver,
        ILoggerFactory loggerFactory)
    {
        this.messages = messages;
        this.drafts = drafts;
        this.notes = notes;
        this.activityLogs = activityLogs;
        this.engineController = engineController;
        this.printDriver = printDriver;
        activityLogger = loggerFactory.CreateLogger("ACTIVITY");

        printReceivedEnabled = engineController.PrintReceivedDefaultEnabled;
        AvailablePrinters = printDriver.GetAvailablePrinters();
        selectedPrinter = printDriver.GetDefaultPrinter();

        entryService.MessageInserted += OnMessageInserted;
    }

    private readonly IMessageRepository messages;
    private readonly IDraftRepository drafts;
    private readonly INoteRepository notes;
    private readonly IActivityLogRepository activityLogs;
    private readonly IEngineController engineController;
    private readonly IPrintDriver printDriver;
    private readonly ILogger activityLogger;
    private readonly List<PrintQueueEntry> queue = [];
    private readonly Lock gate = new();
    private bool isProcessing;

    // Next-to-print first: manual entries before any automatic one, then by descending message priority,
    // then oldest-queued first among ties.
    private readonly Comparison<PrintQueueEntry> order = (a, b) =>
    {
        int manual = b.IsManual.CompareTo(a.IsManual);
        if (manual != 0) { return manual; }
        int priority = b.Priority.CompareTo(a.Priority);
        if (priority != 0) { return priority; }
        return a.QueuedAt.CompareTo(b.QueuedAt);
    };

    [ObservableProperty] private bool printReceivedEnabled;
    [ObservableProperty] private string? selectedPrinter;

    /// <inheritdoc />
    public ObservableCollection<PrintQueueEntry> Queue { get; } = [];
    /// <inheritdoc />
    public IReadOnlyList<string> AvailablePrinters { get; }

    private Task OnMessageInserted(MessageEntity entity)
    {
        if (!PrintReceivedEnabled) { return Task.CompletedTask; }

        int count = engineController.GetPrintCount(entity.Message);
        if (count <= 0) { return Task.CompletedTask; }

        int priority = engineController.GetPriority(entity.Message);
        string title = engineController.GetSubject(entity.Message);
        for (int i = 0; i < count; i++)
        {
            Enqueue(new PrintQueueEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                EntryId = entity.MessageId,
                EntryType = EntryType.Message,
                IsOutboundMessage = false,
                Title = title,
                IsManual = false,
                Priority = priority,
                QueuedAt = DateTime.UtcNow
            });
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void EnqueueManual(EntryItemViewModel entry) => Enqueue(new PrintQueueEntry
    {
        Id = Guid.NewGuid().ToString("N"),
        EntryId = entry.Id,
        EntryType = entry.EntryType,
        IsOutboundMessage = entry.IsOutboundMessage,
        Title = entry.Title,
        IsManual = true,
        Priority = 0,
        QueuedAt = DateTime.UtcNow
    });

    private void Enqueue(PrintQueueEntry job)
    {
        lock (gate)
        {
            queue.Add(job);
            queue.Sort(order);
        }
        RefreshQueueDisplay();
        TryStartProcessing();
    }

    [RelayCommand]
    private void Remove(PrintQueueEntry entry)
    {
        lock (gate) queue.RemoveAll(j => j.Id == entry.Id);
        RefreshQueueDisplay();
    }

    [RelayCommand]
    private void Purge()
    {
        lock (gate) queue.Clear();
        RefreshQueueDisplay();
    }

    partial void OnSelectedPrinterChanged(string? value) => TryStartProcessing();

    private void RefreshQueueDisplay()
    {
        List<PrintQueueEntry> snapshot;
        lock (gate) snapshot = [.. queue];
        Queue.Clear();
        foreach (PrintQueueEntry entry in snapshot)
        {
            Queue.Add(entry);
        }
    }

    private void TryStartProcessing()
    {
        lock (gate)
        {
            if (isProcessing || SelectedPrinter is null || queue.Count == 0) { return; }
            isProcessing = true;
        }
        _ = RunPrintLoop();
    }

    private PrintQueueEntry? PeekTopLocked() => queue.Count > 0 ? queue[0] : null;

    private async Task RunPrintLoop()
    {
        while (true)
        {
            string? printer = SelectedPrinter;
            PrintQueueEntry? job;
            lock (gate)
            {
                job = printer is null ? null : PeekTopLocked();
                if (job is null)
                {
                    isProcessing = false;
                    return;
                }
            }

            List<string> lines;
            try
            {
                lines = await LoadLines(job);
            }
            catch (Exception ex)
            {
                activityLogger.LogError(ex, "Failed to load print content for {EntryId}", job.EntryId);
                lock (gate) queue.RemoveAll(j => j.Id == job.Id);
                RefreshQueueDisplay();
                continue;
            }

            bool interrupted = false;
            foreach (string line in lines)
            {
                await printDriver.PrintLine(printer!, line);
                lock (gate)
                {
                    PrintQueueEntry? top = PeekTopLocked();
                    interrupted = top is null || top.Id != job.Id;
                }
                if (interrupted) { break; }
            }
            await printDriver.PageFeed(printer!);

            if (!interrupted)
            {
                lock (gate) queue.RemoveAll(j => j.Id == job.Id);
                RefreshQueueDisplay();
            }
        }
    }

    private async Task<List<string>> LoadLines(PrintQueueEntry job)
    {
        switch (job.EntryType)
        {
            case EntryType.Message:
            {
                MessageEntity? entity = await messages.Get(job.EntryId, job.IsOutboundMessage);
                if (entity is null) { return []; }
                List<string> lines = [engineController.GetSubject(entity.Message), string.Empty];
                lines.AddRange(SplitLines(engineController.GetBody(entity.Message)));
                return lines;
            }
            case EntryType.Draft:
            {
                ObjectId? id = TryParseObjectId(job.EntryId);
                DraftEntity? entity = id is null ? null : await drafts.Get(id);
                if (entity is null) { return []; }
                List<string> lines = [entity.Subject, string.Empty];
                lines.AddRange(SplitLines(entity.Body));
                return lines;
            }
            case EntryType.Note:
            {
                ObjectId? id = TryParseObjectId(job.EntryId);
                NoteEntity? entity = id is null ? null : await notes.Get(id);
                return entity is null ? [] : SplitLines(entity.Body);
            }
            case EntryType.Activity:
            {
                ObjectId? id = TryParseObjectId(job.EntryId);
                ActivityLogEntity? entity = id is null ? null : await activityLogs.Get(id);
                return entity is null ? [] : entity.EventEntries.Select(e => $"{e.At:HH:mm} {e.Message}").ToList();
            }
            default:
                return [];
        }
    }
}
