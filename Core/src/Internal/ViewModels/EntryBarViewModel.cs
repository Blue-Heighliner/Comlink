namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>ViewModel interface for the entry list panel.</summary>
internal interface IEntryBarViewModel
{
    /// <summary>Raised when the user's selection in the list changes, carrying every entry newly added to the selection (one for a plain click, several for a shift-range or accumulated ctrl-click selection).</summary>
    event Action<IReadOnlyList<EntryItemViewModel>>? EntriesSelected;
    /// <summary>Raised after an entry is successfully deleted via <see cref="DeleteEntry"/> (not raised on the silent no-op path).</summary>
    event Action<EntryItemViewModel>? EntryDeleted;

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
    /// <summary>Gets or sets a value indicating whether entries in the current folder can be deleted, per <see cref="IEngineController.CanDelete"/>.</summary>
    bool CanDeleteEntries { get; set; }
    /// <summary>
    /// Gets or sets the search text filtering the current folder's entries, matched case-insensitively against
    /// fields specific to each entry type (see <see cref="Services.IEntryService.GetMessages"/>). Setting it resets
    /// to the first page and reloads. Empty (the default) shows every entry, unfiltered.
    /// </summary>
    string SearchText { get; set; }
    /// <summary>Gets or sets a value indicating whether the search box is shown for the current folder; <see langword="false"/> for Activity, which has no free-text fields worth searching.</summary>
    bool ShowSearch { get; set; }
    /// <summary>Gets or sets the date of the inclusive lower bound on the entry's own timestamp (received for messages, last modified for drafts and notes). Setting it resets to the first page and reloads.</summary>
    DateTimeOffset? DateFrom { get; set; }
    /// <summary>Gets or sets the time of day paired with <see cref="DateFrom"/>; midnight (the start of the day) when unset. Setting it resets to the first page and reloads.</summary>
    TimeSpan? TimeFrom { get; set; }
    /// <summary>Gets or sets the date of the inclusive upper bound on the entry's own timestamp. Setting it resets to the first page and reloads.</summary>
    DateTimeOffset? DateTo { get; set; }
    /// <summary>Gets or sets the time of day paired with <see cref="DateTo"/>; the last instant of the day (23:59:59.999) when unset, so picking only a date still covers that whole day. Setting it resets to the first page and reloads.</summary>
    TimeSpan? TimeTo { get; set; }
    /// <summary>Gets the selectable security level filters, a leading "Any" (no filter) option followed by every configured <see cref="IEngineController.SecurityLevels"/> entry.</summary>
    IReadOnlyList<SecurityLevelFilterOption> AvailableSecurityLevelFilters { get; }
    /// <summary>Gets or sets the selected security level filter. Setting it resets to the first page and reloads.</summary>
    SecurityLevelFilterOption SelectedSecurityLevelFilter { get; set; }
    /// <summary>Gets the selectable priority filters, a leading "Any" (no filter) option followed by every <see cref="IEngineController.Priorities"/> entry.</summary>
    IReadOnlyList<PriorityFilterOption> AvailablePriorityFilters { get; }
    /// <summary>Gets or sets the selected priority filter. Setting it resets to the first page and reloads.</summary>
    PriorityFilterOption SelectedPriorityFilter { get; set; }
    /// <summary>Gets or sets a value indicating whether only entries flagged as an alert are shown. Setting it resets to the first page and reloads.</summary>
    bool AlertOnlyFilter { get; set; }
    /// <summary>Gets or sets the sender name filter (case-insensitive substring), Inbox only. Setting it resets to the first page and reloads.</summary>
    string AuthorFilter { get; set; }
    /// <summary>Gets or sets the addressee name filter (case-insensitive substring), Outbox and Drafts only. Setting it resets to the first page and reloads.</summary>
    string DestinationFilter { get; set; }
    /// <summary>Gets or sets a value indicating whether the author filter box is shown for the current folder; Inbox only.</summary>
    bool ShowAuthorFilter { get; set; }
    /// <summary>Gets or sets a value indicating whether the destination filter box is shown for the current folder; Outbox and Drafts only.</summary>
    bool ShowDestinationFilter { get; set; }
    /// <summary>Gets or sets a value indicating whether the security level filter picker is shown for the current folder; Inbox, Outbox and Drafts only, and only when at least one security level is configured.</summary>
    bool ShowSecurityLevelFilter { get; set; }
    /// <summary>Gets or sets a value indicating whether the priority filter picker is shown for the current folder; Inbox, Outbox and Drafts only.</summary>
    bool ShowPriorityFilter { get; set; }
    /// <summary>Gets or sets a value indicating whether the alert-only filter checkbox is shown for the current folder; Inbox, Outbox and Drafts only.</summary>
    bool ShowAlertFilter { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether the collapsible filter section (date range, author/destination, security level,
    /// priority, alert-only) is expanded. Collapsed by default. Collapsing only hides the controls - it never clears or disables
    /// the filters themselves, so search continues to run against the same already-filtered set either way; see
    /// <see cref="Services.EntryFilter"/>.
    /// </summary>
    bool IsFiltersExpanded { get; set; }
    /// <summary>Gets the expand/collapse indicator glyph for the filter section.</summary>
    string FiltersExpandIndicator { get; }
    /// <summary>Gets the number of filter section criteria currently set, shown next to the collapsed header so an active filter is never silently forgotten.</summary>
    int ActiveFilterCount { get; }
    /// <summary>Gets a value indicating whether <see cref="ActiveFilterCount"/> is non-zero.</summary>
    bool HasActiveFilters { get; }

    /// <summary>Loads the first page of entries for the given folder and resets pagination.</summary>
    Task LoadFolder(FolderItemViewModel folder);
    /// <summary>Reloads the current page of entries for the active folder.</summary>
    Task Refresh();
    /// <summary>Updates the overall delivery status on an entry already shown in the list.</summary>
    Task UpdateEntryStatus(string messageId, DestinationStatus? overallStatus);
    /// <summary>Inserts an entry at the top of the current page when the active folder and page match.</summary>
    Task PrependEntry(EntryItemViewModel entry);
    /// <summary>
    /// Deletes the given entry from the data store and removes it from the current list, unless
    /// <see cref="IEngineController.CanDelete"/> forbids deletion for the active folder's root type, in
    /// which case this is a silent no-op.
    /// </summary>
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
internal sealed partial class EntryBarViewModel : ObservableObject, IEntryBarViewModel
{
    private const int PageSize = 50;

    /// <summary>Initializes a new <see cref="EntryBarViewModel"/> with the required entry service.</summary>
    /// <param name="entryService">Entry service for data loading and delete operations.</param>
    /// <param name="engineController">Maps logical fields onto a message entity's stored message; provides the available message priority levels, used to label each Inbox/Outbox entry's priority, and whether each entry's tag is shown.</param>
    public EntryBarViewModel(IEntryService entryService, IEngineController engineController)
    {
        this.entryService = entryService;
        this.engineController = engineController;
        AvailableSecurityLevelFilters = [new SecurityLevelFilterOption { Label = "Any", Name = null }, .. engineController.SecurityLevels.Select(l => new SecurityLevelFilterOption { Label = l.Name, Name = l.Name })];
        AvailablePriorityFilters = [new PriorityFilterOption { Label = "Any", Value = null }, .. engineController.Priorities.Select(p => new PriorityFilterOption { Label = p.Name, Value = p.Value })];
        selectedSecurityLevelFilter = AvailableSecurityLevelFilters[0];
        selectedPriorityFilter = AvailablePriorityFilters[0];
    }

    private readonly IEntryService entryService;
    private readonly IEngineController engineController;

    [ObservableProperty] private EntryItemViewModel? selectedEntry;
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int totalPages = 1;
    [ObservableProperty] private bool isAlphabeticalSort;
    [ObservableProperty] private bool canGoNext;
    [ObservableProperty] private bool canGoPrev;
    [ObservableProperty] private bool showSortToggle;
    [ObservableProperty] private bool canDeleteEntries;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool showSearch;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private DateTimeOffset? dateFrom;
    [ObservableProperty] private TimeSpan? timeFrom;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private DateTimeOffset? dateTo;
    [ObservableProperty] private TimeSpan? timeTo;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private SecurityLevelFilterOption selectedSecurityLevelFilter;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private PriorityFilterOption selectedPriorityFilter;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool alertOnlyFilter;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private string authorFilter = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private string destinationFilter = string.Empty;
    [ObservableProperty] private bool showAuthorFilter;
    [ObservableProperty] private bool showDestinationFilter;
    [ObservableProperty] private bool showSecurityLevelFilter;
    [ObservableProperty] private bool showPriorityFilter;
    [ObservableProperty] private bool showAlertFilter;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FiltersExpandIndicator))]
    private bool isFiltersExpanded;

    private FolderItemViewModel? currentFolder;
    private string? pendingSelectId;
    private int refreshGeneration;
    private bool suppressFilterRefresh;
    /// <summary>The last instant of a day (23:59:59.999), paired with an unset <see cref="TimeTo"/> so picking only a date still covers that whole day.</summary>
    private readonly TimeSpan endOfDay = new(0, 23, 59, 59, 999);

    /// <summary>The search text normalized for <see cref="Services.IEntryService"/> calls: <see langword="null"/> (no filtering) rather than empty or whitespace-only.</summary>
    private string? Search => string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;

    /// <summary><see cref="DateFrom"/> combined with <see cref="TimeFrom"/> (defaulting to midnight) into one instant, or <see langword="null"/> when no date is set.</summary>
    private DateTime? CombinedDateFrom => DateFrom?.Date + (TimeFrom ?? TimeSpan.Zero);

    /// <summary><see cref="DateTo"/> combined with <see cref="TimeTo"/> (defaulting to the last instant of the day) into one instant, or <see langword="null"/> when no date is set.</summary>
    private DateTime? CombinedDateTo => DateTo?.Date + (TimeTo ?? endOfDay);

    /// <summary>The current filter state as an <see cref="EntryFilter"/>, or <see langword="null"/> when every criterion is unset.</summary>
    private EntryFilter? Filter
    {
        get
        {
            EntryFilter filter = new()
            {
                Search = Search,
                DateFrom = CombinedDateFrom,
                DateTo = CombinedDateTo,
                Author = ShowAuthorFilter && !string.IsNullOrWhiteSpace(AuthorFilter) ? AuthorFilter.Trim() : null,
                Destination = ShowDestinationFilter && !string.IsNullOrWhiteSpace(DestinationFilter) ? DestinationFilter.Trim() : null,
                SecurityLevel = SelectedSecurityLevelFilter.Name,
                Priority = SelectedPriorityFilter.Value,
                AlertOnly = AlertOnlyFilter ? true : null
            };
            return filter.IsEmpty ? null : filter;
        }
    }

    private void ResetPageAndRefresh()
    {
        if (suppressFilterRefresh) { return; }
        CurrentPage = 1;
        _ = Refresh();
    }

    private void ResetFilterCriteria()
    {
        DateFrom = null;
        TimeFrom = null;
        DateTo = null;
        TimeTo = null;
        AuthorFilter = string.Empty;
        DestinationFilter = string.Empty;
        SelectedSecurityLevelFilter = AvailableSecurityLevelFilters[0];
        SelectedPriorityFilter = AvailablePriorityFilters[0];
        AlertOnlyFilter = false;
    }

    partial void OnSearchTextChanged(string value) => ResetPageAndRefresh();
    partial void OnDateFromChanged(DateTimeOffset? value) => ResetPageAndRefresh();
    partial void OnTimeFromChanged(TimeSpan? value) => ResetPageAndRefresh();
    partial void OnDateToChanged(DateTimeOffset? value) => ResetPageAndRefresh();
    partial void OnTimeToChanged(TimeSpan? value) => ResetPageAndRefresh();
    partial void OnAuthorFilterChanged(string value) => ResetPageAndRefresh();
    partial void OnDestinationFilterChanged(string value) => ResetPageAndRefresh();
    partial void OnSelectedSecurityLevelFilterChanged(SecurityLevelFilterOption value) => ResetPageAndRefresh();
    partial void OnSelectedPriorityFilterChanged(PriorityFilterOption value) => ResetPageAndRefresh();
    partial void OnAlertOnlyFilterChanged(bool value) => ResetPageAndRefresh();

    /// <summary>Gets the current page of entry items displayed in the list.</summary>
    public ObservableCollection<EntryItemViewModel> Entries { get; } = [];
    /// <inheritdoc />
    public IReadOnlyList<SecurityLevelFilterOption> AvailableSecurityLevelFilters { get; }
    /// <inheritdoc />
    public IReadOnlyList<PriorityFilterOption> AvailablePriorityFilters { get; }
    /// <inheritdoc />
    public string FiltersExpandIndicator => IsFiltersExpanded ? "▲" : "▼";
    /// <inheritdoc />
    public int ActiveFilterCount
        => (DateFrom is not null ? 1 : 0)
         + (DateTo is not null ? 1 : 0)
         + (ShowAuthorFilter && !string.IsNullOrWhiteSpace(AuthorFilter) ? 1 : 0)
         + (ShowDestinationFilter && !string.IsNullOrWhiteSpace(DestinationFilter) ? 1 : 0)
         + (SelectedSecurityLevelFilter.Name is not null ? 1 : 0)
         + (SelectedPriorityFilter.Value is not null ? 1 : 0)
         + (AlertOnlyFilter ? 1 : 0);
    /// <inheritdoc />
    public bool HasActiveFilters => ActiveFilterCount > 0;
    /// <inheritdoc />
    public event Action<IReadOnlyList<EntryItemViewModel>>? EntriesSelected;
    /// <inheritdoc />
    public event Action<EntryItemViewModel>? EntryDeleted;

    private string GetPriorityLabel(object message) => engineController.Priorities.GetLabel(engineController.GetPriority(message));

    private string? GetTagLabel(object message)
    {
        if (!engineController.TagsEnabled) { return null; }
        string tag = engineController.GetTag(message);
        return string.IsNullOrEmpty(tag) ? null : tag;
    }

    private string? GetSecurityLevelColor(object message)
    {
        string level = engineController.GetSecurityLevel(message);
        return engineController.SecurityLevels.IsRecognized(level) ? engineController.SecurityLevels.GetColor(level) : null;
    }

    /// <summary>Loads the first page of entries for the given folder and resets pagination.</summary>
    public async Task LoadFolder(FolderItemViewModel folder)
    {
        suppressFilterRefresh = true;
        try
        {
            currentFolder = folder;
            CurrentPage = 1;
            ShowSortToggle = folder.RootType is FolderType.Drafts or FolderType.Notes;
            ShowSearch = folder.RootType is FolderType.Inbox or FolderType.Outbox or FolderType.Drafts or FolderType.Notes;
            bool isMessageOrDraftFolder = folder.RootType is FolderType.Inbox or FolderType.Outbox or FolderType.Drafts;
            ShowAuthorFilter = folder.RootType is FolderType.Inbox;
            ShowDestinationFilter = folder.RootType is FolderType.Outbox or FolderType.Drafts;
            ShowSecurityLevelFilter = isMessageOrDraftFolder && engineController.SecurityLevels.Count > 0;
            ShowPriorityFilter = isMessageOrDraftFolder;
            ShowAlertFilter = isMessageOrDraftFolder;
            CanDeleteEntries = engineController.CanDelete(folder.RootType);
            SearchText = string.Empty;
            ResetFilterCriteria();
            DeselectEntry();
        }
        finally
        {
            suppressFilterRefresh = false;
        }
        await Refresh();
    }

    /// <inheritdoc />
    public void DeselectEntry()
    {
        foreach (EntryItemViewModel entry in Entries.Where(e => e.IsSelected).ToList())
        {
            entry.IsSelected = false;
        }
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

    /// <summary>Toggles the filter section between expanded and collapsed. Purely a display concern - the filters themselves stay in effect either way.</summary>
    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    /// <summary>Clears every filter section criterion back to "Any"/unset (search text is untouched), resets to the first page, and reloads.</summary>
    [RelayCommand]
    private async Task ResetFilters()
    {
        suppressFilterRefresh = true;
        try
        {
            ResetFilterCriteria();
        }
        finally
        {
            suppressFilterRefresh = false;
        }
        CurrentPage = 1;
        await Refresh();
    }

    /// <summary>Reloads the current page of entries from the service for the active folder.</summary>
    public async Task Refresh()
    {
        if (currentFolder is null) { return; }

        // Several triggers can refresh at once (a saved draft, a received message, the user paging). Each load runs to
        // completion, but only the newest one is shown; clearing up front and adding after the load let overlapping
        // refreshes interleave and list entries twice.
        int generation = ++refreshGeneration;
        (List<EntryItemViewModel> items, int total) = await Load(currentFolder);
        if (generation != refreshGeneration) { return; }

        Entries.Clear();
        foreach (EntryItemViewModel item in items)
        {
            Entries.Add(item);
        }
        UpdatePagination(total);
        ApplyPendingSelect();
    }

    private async Task<(List<EntryItemViewModel> Items, int Total)> Load(FolderItemViewModel folder)
    {
        List<EntryItemViewModel> items = [];
        switch (folder.RootType)
        {
            case FolderType.Inbox:
                {
                    (List<MessageEntity> messages, int total) = await entryService.GetMessages(folder.Id, CurrentPage, Filter);
                    foreach (MessageEntity m in messages)
                    {
                        string timeText = m.ReceivedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                        EntryItemViewModel item = new(m.MessageId, engineController.GetFromUser(m.Message), EntryType.Message, m.ReceivedAt,
                            secondaryText: engineController.GetSubject(m.Message), priorityText: GetPriorityLabel(m.Message), tagText: GetTagLabel(m.Message), timeText: timeText,
                            securityLevelColorHex: GetSecurityLevelColor(m.Message), isAlert: engineController.GetIsAlert(m.Message));
                        item.OverallStatus = m.ReadStatus;
                        items.Add(item);
                    }
                    return (items, total);
                }

            case FolderType.Outbox:
                {
                    (List<MessageEntity> messages, int total) = await entryService.GetMessages(folder.Id, CurrentPage, Filter);
                    foreach (MessageEntity m in messages)
                    {
                        string destinations = string.Join(", ", engineController.GetAddresses(m.Message).Select(a => a.UserName).Distinct());
                        string timeText = m.ReceivedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                        EntryItemViewModel item = new(m.MessageId, destinations, EntryType.Message, m.ReceivedAt,
                            secondaryText: engineController.GetSubject(m.Message), priorityText: GetPriorityLabel(m.Message), tagText: GetTagLabel(m.Message), timeText: timeText, isOutboundMessage: true,
                            securityLevelColorHex: GetSecurityLevelColor(m.Message), isAlert: engineController.GetIsAlert(m.Message));
                        item.OverallStatus = m.OverallStatus;
                        items.Add(item);
                    }
                    return (items, total);
                }

            case FolderType.Drafts:
                {
                    (List<DraftEntity> drafts, int total) = await entryService.GetDrafts(folder.Id, CurrentPage, IsAlphabeticalSort, Filter);
                    foreach (DraftEntity d in drafts)
                    {
                        string subject = string.IsNullOrEmpty(d.Subject) ? "(No subject)" : d.Subject;
                        string timeText = d.ModifiedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                        items.Add(new EntryItemViewModel(d.Id.ToString(), subject, EntryType.Draft, d.ModifiedAt, timeText: timeText, isAlert: d.IsAlert));
                    }
                    return (items, total);
                }

            case FolderType.Notes:
                {
                    (List<NoteEntity> notes, int total) = await entryService.GetNotes(folder.Id, CurrentPage, IsAlphabeticalSort, Filter);
                    foreach (NoteEntity n in notes)
                    {
                        string? title = (n.Body ?? string.Empty).Split('\n').FirstOrDefault()?.Trim();
                        string timeText = n.ModifiedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                        items.Add(new EntryItemViewModel(n.Id.ToString(),
                            string.IsNullOrEmpty(title) ? "(Empty note)" : title, EntryType.Note, n.ModifiedAt,
                            timeText: timeText));
                    }
                    return (items, total);
                }

            case FolderType.Activity:
                {
                    (List<ActivityLogEntity> logs, int total) = await entryService.GetActivityLogs(CurrentPage);
                    foreach (ActivityLogEntity l in logs)
                    {
                        items.Add(new EntryItemViewModel(l.Id.ToString(), l.Date.ToString("dd-MMM-yyyy").ToUpperInvariant(), EntryType.Activity, l.Date));
                    }
                    return (items, total);
                }

            default:
                return (items, 0);
        }
    }

    /// <summary>Updates the overall delivery status on an entry already shown in the list.</summary>
    public Task UpdateEntryStatus(string messageId, DestinationStatus? overallStatus)
    {
        EntryItemViewModel? entry = Entries.FirstOrDefault(e => e.Id == messageId && e.EntryType == EntryType.Message);
        if (entry is not null)
        {
            entry.OverallStatus = overallStatus;
        }
        return Task.CompletedTask;
    }

    private void ApplyPendingSelect()
    {
        if (pendingSelectId is null) { return; }
        string id = pendingSelectId;
        pendingSelectId = null;
        EntryItemViewModel? match = Entries.FirstOrDefault(e => e.Id == id);
        if (match is not null) { SelectEntry(match); }
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
        if (currentFolder is null || currentFolder.RootType != FolderType.Inbox &&
            currentFolder.RootType != FolderType.Outbox &&
            currentFolder.RootType != FolderType.Drafts &&
            currentFolder.RootType != FolderType.Notes)
        {
            return;
        }

        // While any filter is active, whether the new entry matches it can only be answered by IEntryService
        // (its match rules read fields this ViewModel does not itself decode), so it is left out of the visible
        // page rather than risked as a false positive; RefreshPaginationCounts below still reflects it if it does match.
        if (CurrentPage == 1 && Filter is null)
        {
            Entries.Insert(0, entry);
            if (Entries.Count > PageSize)
            {
                Entries.RemoveAt(PageSize);
            }
        }

        await RefreshPaginationCounts();
    }

    private async Task RefreshPaginationCounts()
    {
        if (currentFolder is null) { return; }
        int total = currentFolder.RootType switch
        {
            FolderType.Inbox or FolderType.Outbox => (await entryService.GetMessages(currentFolder.Id, 1, Filter)).Total,
            FolderType.Drafts => (await entryService.GetDrafts(currentFolder.Id, 1, IsAlphabeticalSort, Filter)).Total,
            FolderType.Notes => (await entryService.GetNotes(currentFolder.Id, 1, IsAlphabeticalSort, Filter)).Total,
            FolderType.Activity => (await entryService.GetActivityLogs(1)).Total,
            _ => 0
        };
        UpdatePagination(total);
    }

    /// <inheritdoc />
    public async Task DeleteEntry(EntryItemViewModel entry)
    {
        if (currentFolder is null || !engineController.CanDelete(currentFolder.RootType)) { return; }

        await entryService.DeleteEntry(entry.Id, entry.EntryType, entry.IsOutboundMessage);
        Entries.Remove(entry);
        await RefreshPaginationCounts();
        EntryDeleted?.Invoke(entry);
    }

    /// <summary>Bound to the entry list's right-click "Delete" context menu item (<see cref="CanDeleteEntries"/> controls its visibility).</summary>
    [RelayCommand]
    private Task Delete(EntryItemViewModel entry) => DeleteEntry(entry);

    /// <summary>Queues an entry ID to be auto-selected after the next refresh.</summary>
    public void SetPendingSelectId(string id) => pendingSelectId = id;

    /// <inheritdoc />
    public void SelectEntry(EntryItemViewModel entry)
    {
        if (SelectedEntry == entry && Entries.Count(e => e.IsSelected) == 1) { return; }

        foreach (EntryItemViewModel other in Entries.Where(e => e.IsSelected && e != entry).ToList())
        {
            other.IsSelected = false;
        }
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
        {
            entry.IsSelected = false;
        }
        foreach (EntryItemViewModel entry in added)
        {
            entry.IsSelected = true;
        }

        if (added.Count > 0)
        {
            EntriesSelected?.Invoke(added);
        }
    }
}
