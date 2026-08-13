namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>ViewModel interface for the entry list panel.</summary>
public interface IEntryBarViewModel
{
    /// <summary>Gets or sets the currently selected entry.</summary>
    EntryItemViewModel? SelectedEntry { get; set; }
    /// <summary>Gets the current page of entry items displayed in the list.</summary>
    ObservableCollection<EntryItemViewModel> Entries { get; }
    /// <summary>Gets or sets the current page number.</summary>
    int CurrentPage { get; set; }
    /// <summary>Gets or sets the total number of pages for the current folder.</summary>
    int TotalPages { get; set; }
    /// <summary>Gets or sets a value indicating whether entries are sorted alphabetically.</summary>
    bool IsAlphabeticalSort { get; set; }
    /// <summary>Gets or sets a value indicating whether navigation to the next page is possible.</summary>
    bool CanGoNext { get; set; }
    /// <summary>Gets or sets a value indicating whether navigation to the previous page is possible.</summary>
    bool CanGoPrev { get; set; }
    /// <summary>Gets or sets a value indicating whether the sort toggle control is visible.</summary>
    bool ShowSortToggle { get; set; }
    /// <summary>Raised when the user's selection in the list changes, carrying every entry newly added to the selection (one for a plain click, several for a shift-range or accumulated ctrl-click selection).</summary>
    event Action<IReadOnlyList<EntryItemViewModel>>? EntriesSelected;
    /// <summary>Loads the first page of entries for the given folder and resets pagination.</summary>
    Task LoadFolder(FolderItemViewModel folder);
    /// <summary>Reloads the current page of entries for the active folder.</summary>
    Task Refresh();
    /// <summary>Updates the overall delivery status on an entry already shown in the list.</summary>
    Task UpdateEntryStatus(string messageId, DestinationStatus? overallStatus);
    /// <summary>Inserts an entry at the top of the current page when the active folder and page match.</summary>
    Task PrependEntry(EntryItemViewModel entry);
    /// <summary>Deletes the given entry from the data store and removes it from the current list.</summary>
    Task DeleteEntry(EntryItemViewModel entry);
    /// <summary>Queues an entry ID to be auto-selected after the next refresh.</summary>
    void SetPendingSelectId(string id);
    /// <summary>Marks the given entry as selected, deselecting every other entry.</summary>
    void SelectEntry(EntryItemViewModel entry);
    /// <summary>
    /// Applies a selection change reported by the entry list (e.g. from a shift-range or ctrl-click
    /// multi-selection): marks <paramref name="added"/> as selected and <paramref name="removed"/> as
    /// deselected, then raises <see cref="EntriesSelected"/> with <paramref name="added"/> if non-empty.
    /// </summary>
    void SelectEntries(IReadOnlyList<EntryItemViewModel> added, IReadOnlyList<EntryItemViewModel> removed);
    /// <summary>Clears the current selection, if any, without raising <see cref="EntriesSelected"/>.</summary>
    void DeselectEntry();
}

/// <summary>ViewModel for the entry list panel, providing paginated browsing and selection of entries within a folder.</summary>
public sealed partial class EntryBarViewModel : ObservableObject, IEntryBarViewModel
{
    private readonly IEntryService _entryService;
    private readonly IMessageFormat _messageFormat;
    private readonly IMessagePriorityProvider _priorityProvider;
    private const int PageSize = 50;

    [ObservableProperty] private EntryItemViewModel? _selectedEntry;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _isAlphabeticalSort;
    [ObservableProperty] private bool _canGoNext;
    [ObservableProperty] private bool _canGoPrev;
    [ObservableProperty] private bool _showSortToggle;

    private FolderItemViewModel? _currentFolder;
    private string? _pendingSelectId;

    /// <summary>Gets the current page of entry items displayed in the list.</summary>
    public ObservableCollection<EntryItemViewModel> Entries { get; } = [];
    /// <inheritdoc />
    public event Action<IReadOnlyList<EntryItemViewModel>>? EntriesSelected;

    /// <summary>Initializes a new <see cref="EntryBarViewModel"/> with the required entry service.</summary>
    /// <param name="entryService">Entry service for data loading and delete operations.</param>
    /// <param name="messageFormat">Maps logical fields onto a message entity's stored message.</param>
    /// <param name="priorityProvider">Provides the available message priority levels, used to label each Inbox/Outbox entry's priority.</param>
    public EntryBarViewModel(IEntryService entryService, IMessageFormat messageFormat, IMessagePriorityProvider priorityProvider)
    {
        _entryService = entryService;
        _messageFormat = messageFormat;
        _priorityProvider = priorityProvider;
    }

    private string GetPriorityLabel(object message)
    {
        int value = _messageFormat.GetPriority(message);
        return _priorityProvider.GetPriorities().FirstOrDefault(p => p.Value == value)?.Name ?? value.ToString();
    }

    /// <summary>Loads the first page of entries for the given folder and resets pagination.</summary>
    public async Task LoadFolder(FolderItemViewModel folder)
    {
        _currentFolder = folder;
        CurrentPage = 1;
        ShowSortToggle = folder.RootType is FolderType.Drafts or FolderType.Notes;
        DeselectEntry();
        await Refresh();
    }

    /// <inheritdoc />
    public void DeselectEntry()
    {
        foreach (EntryItemViewModel entry in Entries.Where(e => e.IsSelected).ToList())
            entry.IsSelected = false;
        SelectedEntry = null;
    }

    [RelayCommand]
    private async Task NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await Refresh();
        }
    }

    [RelayCommand]
    private async Task PrevPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await Refresh();
        }
    }

    [RelayCommand]
    private async Task ToggleSort()
    {
        IsAlphabeticalSort = !IsAlphabeticalSort;
        CurrentPage = 1;
        await Refresh();
    }

    /// <summary>Reloads the current page of entries from the service for the active folder.</summary>
    public async Task Refresh()
    {
        if (_currentFolder is null) return;

        Entries.Clear();

        switch (_currentFolder.RootType)
        {
            case FolderType.Inbox:
                (List<MessageEntity> inboxMsgs, int inboxTotal) = await _entryService.GetMessages(_currentFolder.Id, CurrentPage);
                foreach (MessageEntity m in inboxMsgs)
                {
                    string timeText = m.ReceivedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                    EntryItemViewModel item = new(m.MessageId, _messageFormat.GetFromUser(m.Message), EntryType.Message, m.ReceivedAt,
                        secondaryText: _messageFormat.GetSubject(m.Message), priorityText: GetPriorityLabel(m.Message), timeText: timeText);
                    item.OverallStatus = m.ReadStatus;
                    Entries.Add(item);
                }
                UpdatePagination(inboxTotal);
                break;

            case FolderType.Outbox:
                (List<MessageEntity> outboxMsgs, int outboxTotal) = await _entryService.GetMessages(_currentFolder.Id, CurrentPage);
                foreach (MessageEntity m in outboxMsgs)
                {
                    string destinations = string.Join(", ", _messageFormat.GetAddresses(m.Message).Select(a => a.UserName).Distinct());
                    string timeText = m.ReceivedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                    EntryItemViewModel item = new(m.MessageId, destinations, EntryType.Message, m.ReceivedAt,
                        secondaryText: _messageFormat.GetSubject(m.Message), priorityText: GetPriorityLabel(m.Message), timeText: timeText, isOutboundMessage: true);
                    item.OverallStatus = m.OverallStatus;
                    Entries.Add(item);
                }
                UpdatePagination(outboxTotal);
                break;

            case FolderType.Drafts:
                (List<DraftEntity> drafts, int draftTotal) = await _entryService.GetDrafts(_currentFolder.Id, CurrentPage, IsAlphabeticalSort);
                foreach (DraftEntity d in drafts)
                {
                    string subject = string.IsNullOrEmpty(d.Subject) ? "(No subject)" : d.Subject;
                    string timeText = d.ModifiedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                    Entries.Add(new EntryItemViewModel(d.Id.ToString(), subject, EntryType.Draft, d.ModifiedAt, timeText: timeText));
                }
                UpdatePagination(draftTotal);
                break;

            case FolderType.Notes:
                (List<NoteEntity> notes, int noteTotal) = await _entryService.GetNotes(_currentFolder.Id, CurrentPage, IsAlphabeticalSort);
                foreach (NoteEntity n in notes)
                {
                    string? title = (n.Body ?? string.Empty).Split('\n').FirstOrDefault()?.Trim();
                    string timeText = n.ModifiedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                    Entries.Add(new EntryItemViewModel(n.Id.ToString(),
                        string.IsNullOrEmpty(title) ? "(Empty note)" : title, EntryType.Note, n.ModifiedAt,
                        timeText: timeText));
                }
                UpdatePagination(noteTotal);
                break;

            case FolderType.Activity:
                (List<ActivityLogEntity> logs, int logTotal) = await _entryService.GetActivityLogs(CurrentPage);
                foreach (ActivityLogEntity l in logs)
                    Entries.Add(new EntryItemViewModel(l.Id.ToString(), l.Date.ToString("dd-MMM-yyyy").ToUpperInvariant(), EntryType.Activity, l.Date));
                UpdatePagination(logTotal);
                break;
        }

        ApplyPendingSelect();
    }

    /// <summary>Updates the overall delivery status on an entry already shown in the list.</summary>
    public Task UpdateEntryStatus(string messageId, DestinationStatus? overallStatus)
    {
        EntryItemViewModel? entry = Entries.FirstOrDefault(e => e.Id == messageId && e.EntryType == EntryType.Message);
        if (entry is not null)
            entry.OverallStatus = overallStatus;
        return Task.CompletedTask;
    }

    private void ApplyPendingSelect()
    {
        if (_pendingSelectId is null) return;
        string id = _pendingSelectId;
        _pendingSelectId = null;
        EntryItemViewModel? match = Entries.FirstOrDefault(e => e.Id == id);
        if (match is not null) SelectEntry(match);
    }

    private void UpdatePagination(int total)
    {
        TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        CanGoNext = CurrentPage < TotalPages;
        CanGoPrev = CurrentPage > 1;
    }

    /// <summary>Inserts an entry at the top of the current page when the active folder and page match, then refreshes pagination counts.</summary>
    public async Task PrependEntry(EntryItemViewModel entry)
    {
        if (_currentFolder is null || _currentFolder.RootType != FolderType.Inbox &&
            _currentFolder.RootType != FolderType.Outbox &&
            _currentFolder.RootType != FolderType.Drafts &&
            _currentFolder.RootType != FolderType.Notes) return;

        if (CurrentPage == 1)
        {
            Entries.Insert(0, entry);
            if (Entries.Count > PageSize)
                Entries.RemoveAt(PageSize);
        }

        await RefreshPaginationCounts();
    }

    private async Task RefreshPaginationCounts()
    {
        if (_currentFolder is null) return;
        int total = _currentFolder.RootType switch
        {
            FolderType.Inbox or FolderType.Outbox => (await _entryService.GetMessages(_currentFolder.Id, 1)).Total,
            FolderType.Drafts => (await _entryService.GetDrafts(_currentFolder.Id, 1, IsAlphabeticalSort)).Total,
            FolderType.Notes => (await _entryService.GetNotes(_currentFolder.Id, 1, IsAlphabeticalSort)).Total,
            FolderType.Activity => (await _entryService.GetActivityLogs(1)).Total,
            _ => 0
        };
        UpdatePagination(total);
    }

    /// <summary>Deletes the given entry from the data store and removes it from the current list.</summary>
    public async Task DeleteEntry(EntryItemViewModel entry)
    {
        await _entryService.DeleteEntry(entry.Id, entry.EntryType, entry.IsOutboundMessage);
        Entries.Remove(entry);
    }

    /// <summary>Queues an entry ID to be auto-selected after the next refresh.</summary>
    public void SetPendingSelectId(string id) => _pendingSelectId = id;

    /// <inheritdoc />
    public void SelectEntry(EntryItemViewModel entry)
    {
        if (SelectedEntry == entry && Entries.Count(e => e.IsSelected) == 1) return;

        foreach (EntryItemViewModel other in Entries.Where(e => e.IsSelected && e != entry).ToList())
            other.IsSelected = false;
        SelectedEntry = entry;
        entry.IsSelected = true;
        EntriesSelected?.Invoke([entry]);
    }

    /// <inheritdoc />
    public void SelectEntries(IReadOnlyList<EntryItemViewModel> added, IReadOnlyList<EntryItemViewModel> removed)
    {
        // Deliberately does not assign SelectedEntry: this method reacts to the View's own
        // SelectionChanged, so the ListBox's native multi-selection is already correct. SelectedEntry
        // drives the OneWay SelectedItem binding back into that same ListBox, and Avalonia's
        // SelectedItem setter collapses a multi-selection down to a single item — writing it here
        // would immediately undo a ctrl/shift selection the user just made.
        foreach (EntryItemViewModel entry in removed)
            entry.IsSelected = false;
        foreach (EntryItemViewModel entry in added)
            entry.IsSelected = true;

        if (added.Count > 0)
            EntriesSelected?.Invoke(added);
    }
}
