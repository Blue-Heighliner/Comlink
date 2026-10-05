namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for composing and sending a draft message.</summary>
internal interface IDraftViewModel
{
    /// <summary>Raised after the draft is successfully sent, providing the resulting message entity.</summary>
    event Func<IDraftViewModel, MessageEntity, Task>? DraftSent;

    /// <summary>Gets the LiteDB object-id string for this draft.</summary>
    string Id { get; }
    /// <summary>Gets or sets the user name being typed into the address field (auto-uppercased).</summary>
    string NewAddressUser { get; set; }
    /// <summary>Gets or sets the address type selected in the address field.</summary>
    AddressTypeOption NewAddressType { get; set; }
    /// <summary>Gets or sets the custom instructions being typed for the address (for example <c>Deliver to Eastside Office</c>).</summary>
    string NewAddressInformation { get; set; }
    /// <summary>Gets or sets a value indicating whether this draft has been sent.</summary>
    bool IsSent { get; set; }
    /// <summary>Gets a value indicating whether this draft will be sent as an alert, which the host's message handler decides from the draft's other properties (see <see cref="IEngineController.ComputeIsAlert"/>), so the user does not set it.</summary>
    bool IsAlert { get; }
    /// <summary>
    /// Gets the label for the alert checkbox, sourced from <see cref="IEngineController.AlertLabel"/> — the
    /// same text shown in the title bar's alert box, so both surfaces always agree on what "alert" is called.
    /// </summary>
    string AlertLabel { get; }
    /// <summary>
    /// Gets the message priority levels available to choose from; see <see cref="IEngineController.Priorities"/>.
    /// Excludes any priority that <see cref="IEngineController.BlockedCombinations"/> blocks for the current
    /// <see cref="Tag"/>, so a blocked tag/priority combination can never be selected in the first place.
    /// Recomputed whenever <see cref="Tag"/> changes.
    /// </summary>
    IReadOnlyList<MessagePriorityOption> AvailablePriorities { get; }
    /// <summary>Gets or sets the priority level this draft will be sent at.</summary>
    MessagePriorityOption SelectedPriority { get; set; }
    /// <summary>
    /// Gets the security levels available to send this draft at: every level configured with
    /// <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.SecurityLevels"/> up to and including the current user's own assigned level (see
    /// <see cref="IEngineController.GetUserSecurityLevel"/>): a user can declassify to a lower level but never send
    /// above their own clearance. Empty when no security levels are configured, in which case the picker is hidden.
    /// </summary>
    IReadOnlyList<SecurityLevel> AvailableSecurityLevels { get; }
    /// <summary>Gets or sets the security level this draft will be sent at, or <see langword="null"/> when no security levels are configured.</summary>
    SecurityLevel? SelectedSecurityLevel { get; set; }
    /// <summary>
    /// Gets or sets the short, user-inputted tag identifying the type of this message; see
    /// <see cref="IEngineController.GetTag"/>. Setting a tag that <see cref="IEngineController.BlockedCombinations"/>
    /// blocks for the current <see cref="SelectedPriority"/> is rejected — the value silently reverts to the
    /// last valid tag — so a blocked combination can never be entered.
    /// </summary>
    string Tag { get; set; }
    /// <summary>Gets a value indicating whether the tag input is shown; see <see cref="IEngineController"/>.</summary>
    bool TagsEnabled { get; }
    /// <summary>Gets the label for the tag input's watermark, sourced from <see cref="IEngineController.TagLabel"/>.</summary>
    string TagLabel { get; }
    /// <summary>
    /// Gets or sets the PLSO (Phonetic Language Spell Out) mode active in the body editor: when not
    /// <see cref="PlsoMode.Off"/>, typing a letter or digit inserts its phonetic word (see
    /// <see cref="PhoneticAlphabet"/>) instead of the character itself, with a trailing space added
    /// after each word when <see cref="PlsoMode.Spaces"/>. Editor-session-only UI state — not
    /// persisted with the draft.
    /// </summary>
    PlsoMode PlsoMode { get; set; }
    /// <summary>Gets the display text for the PLSO toggle button, reflecting the current <see cref="PlsoMode"/>.</summary>
    string PlsoButtonText { get; }
    /// <summary>Gets or sets a value indicating whether a save or send operation is in progress.</summary>
    bool IsSaving { get; set; }
    /// <summary>Gets or sets the status message displayed after a save or send attempt.</summary>
    string? StatusMessage { get; set; }
    /// <summary>Gets the collection of recipient addresses for this draft.</summary>
    ObservableCollection<AddressData> Addresses { get; }
    /// <summary>Gets the document backing the body editor.</summary>
    IBodyDocument BodyDocument { get; }
    /// <summary>Gets the map of fill-in IDs to their ViewModels, keyed by the 8-char hex ID.</summary>
    IReadOnlyDictionary<string, IFillInViewModel> FillIns { get; }
    /// <summary>Gets the most characters a tag may have, or <see langword="null"/> for no maximum, which the tag box is sized to fit exactly.</summary>
    int? TagMaxLength { get; }
    /// <summary>Returns <paramref name="tag"/> as the tag rules make it: in the forced case, without what is not allowed, and cut to the maximum length.</summary>
    /// <param name="tag">The tag as entered.</param>
    string FilterTag(string tag);
    /// <summary>Gets or sets how many monospace characters wide a line of the draft is shown, or <see langword="null"/> for no limit. Kept within the range the draft handler states. It only changes how the text is shown: no line break is ever added to the text.</summary>
    int? LineWidth { get; set; }
    /// <summary>Gets or sets <see cref="LineWidth"/> as the number the draft view's width control edits, with an empty control meaning no limit.</summary>
    decimal? LineWidthValue { get; set; }
    /// <summary>Gets whether the draft view offers a line width, which depends on the draft handler.</summary>
    bool IsLineWidthAvailable { get; }
    /// <summary>Gets the narrowest a line may be: what the draft handler states, but never less than the longest line of the <see cref="Header"/>, so the header always fits.</summary>
    decimal LineWidthMinimum { get; }
    /// <summary>Gets the widest a line may be; very large when there is no maximum. Never less than <see cref="LineWidthMinimum"/>: a header wider than the handler's maximum wins.</summary>
    decimal LineWidthMaximum { get; }
    /// <summary>Gets the header every message sent from the draft starts with, or <see langword="null"/> for none. Shown above the body where it cannot be edited, and asked for again whenever an aspect of the draft changes.</summary>
    string? Header { get; }
    /// <summary>Gets all known user names available for recipient auto-complete.</summary>
    IReadOnlyList<string> AllUserNames { get; }
    /// <summary>Gets the selectable address types, each paired with its display label; see <see cref="IEngineController.AddressTypes"/>.</summary>
    IReadOnlyList<AddressTypeOption> AddressTypes { get; }
    /// <summary>Saves the current draft state to the data store.</summary>
    IAsyncRelayCommand SaveCommand { get; }
    /// <summary>Sends the draft as a message.</summary>
    IAsyncRelayCommand SendCommand { get; }
    /// <summary>Gets a value indicating whether the draft can be deleted, per <see cref="IEngineController.CanDelete"/> for drafts.</summary>
    bool CanDelete { get; }
    /// <summary>Gets a value indicating whether a delete is armed and the next <see cref="DeleteCommand"/> press will carry it out.</summary>
    bool IsConfirmingDelete { get; }
    /// <summary>Gets the text for the delete button: <c>"DELETE"</c>, or <c>"CONFIRM DELETE"</c> once armed.</summary>
    string DeleteButtonText { get; }
    /// <summary>Deletes the draft. The first press arms a confirmation and a second one within a few seconds deletes it.</summary>
    IAsyncRelayCommand DeleteCommand { get; }
    /// <summary>Raised after the draft has been deleted.</summary>
    event Func<Task>? Deleted;
    /// <summary>Adds the current <see cref="NewAddressUser"/>, <see cref="NewAddressType"/> and <see cref="NewAddressInformation"/> as a recipient.</summary>
    IRelayCommand AddAddressCommand { get; }
    /// <summary>Removes the specified address from the recipient list.</summary>
    IRelayCommand<AddressData> RemoveAddressCommand { get; }
    /// <summary>Gets the recipients grouped by address type, in the order of the address types, each group in the order its recipients were added or moved to.</summary>
    ObservableCollection<AddressGroup> AddressGroups { get; }
    /// <summary>Moves the specified recipient up within its address type.</summary>
    IRelayCommand<AddressData> MoveAddressUpCommand { get; }
    /// <summary>Moves the specified recipient down within its address type.</summary>
    IRelayCommand<AddressData> MoveAddressDownCommand { get; }
    /// <summary>Raised when what the draft is titled in the list changes while it is being edited: its name, or else the first line of its body.</summary>
    event Action<string>? TitleChanged;
    /// <summary>Gets or sets the name the user gave the draft, shown in the list instead of the first line of the body. Empty clears it.</summary>
    string Name { get; set; }
    /// <summary>Duplicates the draft as it is now into a new draft, leaving this one as it was.</summary>
    IAsyncRelayCommand DuplicateCommand { get; }
    /// <summary>Raised with the id of the new draft after it has been created by <see cref="DuplicateCommand"/>.</summary>
    event Func<string, Task>? Duplicated;
    /// <summary>Saves what has been written to the data store without saying so, which is what happens when the user leaves the draft. Does nothing for a draft that was sent or deleted.</summary>
    Task SaveChanges();

    /// <summary>Inserts a new fill-in marker into the body document at the specified caret offset.</summary>
    void InsertFillIn(int caretOffset);
}

/// <summary>ViewModel for composing and sending a draft message, including fill-in field management.</summary>
[ConstructedManually]
internal sealed partial class DraftViewModel : ObservableObject, IDraftViewModel
{
    private const int FillInIdLength = 8;
    private const int FillInMarkerLength = FillInIdLength + 1; // 9
    // Marker format:  (Unicode PUA U+E001) + 8 lowercase hex chars = 9 chars per fill-in
    private const char FillInSentinel = '';

    private static List<DraftBodySegmentData> DeserializeSegments(string json)
    {
        return JsonSerializer.Deserialize<List<DraftBodySegmentData>>(json) ?? [];
    }

    private static string GenerateFillInId() => Guid.NewGuid().ToString("N")[..FillInIdLength];

    private static string NormalizeId(string id)
    {
        string clean = id.Replace("-", "");
        if (clean.Length >= FillInIdLength) { return clean[..FillInIdLength]; }
        return clean.PadRight(FillInIdLength, '0');
    }

    /// <summary>Initializes a new <see cref="DraftViewModel"/> for the given draft entity.</summary>
    /// <param name="entity">The draft entity to compose and send.</param>
    /// <param name="entryService">Entry service for saving and sending the draft.</param>
    /// <param name="connection">Service connection for sending messages.</param>
    /// <param name="userNames">All known user names available for recipient auto-complete.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    /// <param name="engineController">Provides the shared alert label text, whether the alert checkbox is shown, the available message priority levels, tag input visibility/label, and blocked tag/priority combinations enforced on send.</param>
    /// <param name="bodyDocument">Optional body document implementation; defaults to <see cref="StringBodyDocument"/> when <see langword="null"/>.</param>
    /// <param name="confirmationWindow">How long an armed delete waits for its confirming press; defaults to a few seconds.</param>
    /// <param name="currentSecurityLevel">The current user's own assigned security level name; see <see cref="IEngineController.GetUserSecurityLevel"/>.</param>
    /// <param name="isNew">Whether the draft has not been stored yet; it is only stored once it is altered and not blank.</param>
    public DraftViewModel(
        DraftEntity entity,
        IEntryService entryService,
        IServiceConnection connection,
        IReadOnlyList<string> userNames,
        ILoggerFactory loggerFactory,
        IEngineController engineController,
        IBodyDocument? bodyDocument = null,
        TimeSpan? confirmationWindow = null,
        string currentSecurityLevel = "",
        bool isNew = false)
    {
        this.entity = entity;
        this.isNew = isNew;
        name = entity.Name ?? string.Empty;
        CanDelete = engineController.CanDelete(FolderType.Drafts);
        deleteConfirmation = new DeleteConfirmation(pending => IsConfirmingDelete = pending, confirmationWindow);
        this.entryService = entryService;
        this.connection = connection;
        this.engineController = engineController;
        activityLogger = loggerFactory.CreateLogger("ACTIVITY");
        isSent = entity.IsSent;
        // LiteDB reads an empty string back as null.
        tag = engineController.DraftTagRules.Filter(entity.Tag ?? string.Empty);
        lastValidTag = tag;
        AllUserNames = userNames;
        BodyDocument = bodyDocument ?? new StringBodyDocument();
        AlertLabel = engineController.AlertLabel;
        TagsEnabled = engineController.TagsEnabled;
        TagLabel = engineController.TagLabel;
        AddressTypes = engineController.AddressTypes;
        newAddressType = AddressTypes[0];

        allPriorities = engineController.Priorities;
        availablePriorities = FilterPriorities(tag);
        selectedPriority = AvailablePriorities.FirstOrDefault(p => p.Stored == entity.Priority)
            ?? AvailablePriorities.FirstOrDefault()
            ?? allPriorities[0];

        IReadOnlyList<SecurityLevel> allSecurityLevels = engineController.SecurityLevels;
        int ownRank = allSecurityLevels.GetRank(currentSecurityLevel);
        AvailableSecurityLevels = ownRank < 0 ? [] : [.. allSecurityLevels.Take(ownRank + 1)];
        selectedSecurityLevel = AvailableSecurityLevels.FirstOrDefault(l => l.Value == entity.SecurityLevel)
            ?? AvailableSecurityLevels.LastOrDefault();

        foreach (AddressData a in entity.Addresses)
        {
            Addresses.Add(a);
        }

        lineWidthRange = engineController.DraftLineWidth;
        lineWidth = lineWidthRange is null ? null : entity.LineWidth is { } stored ? lineWidthRange.Clamp(stored) : lineWidthRange.Initial;

        LoadBody(entity);

        Addresses.CollectionChanged += (_, _) =>
        {
            RebuildAddressGroups();
            UpdateHeader();
            StoreIfNew();
        };
        RebuildAddressGroups();
        isReady = true;
        UpdateHeader();
        savedSnapshot = Snapshot();
        initialSnapshot = savedSnapshot;
        BodyDocument.Changed += RaiseTitleChanged;
    }

    private void RaiseTitleChanged()
    {
        TitleChanged?.Invoke(string.IsNullOrWhiteSpace(Name) ? BuildPlainBody().FirstLine : Name.Trim());
        StoreIfNew();
    }

    // The first time a new draft is altered into something worth keeping it is stored, so it shows up in the list straight away.
    private void StoreIfNew()
    {
        if (!isNew || isStoringNew) { return; }

        _ = StoreNew();
    }

    private async Task StoreNew()
    {
        isStoringNew = true;
        try
        {
            await Task.Yield();
            await SaveChanges();
        }
        catch (Exception ex)
        {
            activityLogger.LogError(ex, "Failed to store a new draft");
        }
        finally
        {
            isStoringNew = false;
        }
    }

    private bool isStoringNew;
    private readonly SemaphoreSlim insertLock = new(1, 1);

    private async Task InsertIfNew()
    {
        await insertLock.WaitAsync();
        try
        {
            if (!isNew) { return; }

            await entryService.InsertDraft(entity);
            isNew = false;
        }
        finally
        {
            insertLock.Release();
        }
    }

    partial void OnNameChanged(string value) => RaiseTitleChanged();

    private bool isNew;
    private readonly string initialSnapshot;
    private readonly LineWidthRange? lineWidthRange;
    private int headerWidth;
    private bool isUpdatingHeader;
    private bool isDeleted;
    private string savedSnapshot = string.Empty;
    private readonly IEntryService entryService;
    private readonly DeleteConfirmation deleteConfirmation;
    private readonly IServiceConnection connection;
    private readonly IEngineController engineController;
    private readonly IReadOnlyList<MessagePriorityOption> allPriorities;
    private readonly ILogger activityLogger;
    private DraftEntity entity;
    private string lastValidTag = string.Empty;
    private bool isReady;

    [ObservableProperty] private string name;
    [ObservableProperty] private string newAddressUser = string.Empty;
    [ObservableProperty] private AddressTypeOption newAddressType;
    [ObservableProperty] private string newAddressInformation = string.Empty;
    [ObservableProperty] private bool isSent;
    private bool isAlert;
    [ObservableProperty] private MessagePriorityOption selectedPriority;
    [ObservableProperty] private IReadOnlyList<MessagePriorityOption> availablePriorities = [];
    [ObservableProperty] private SecurityLevel? selectedSecurityLevel;
    [ObservableProperty] private string tag = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineWidthValue))]
    private int? lineWidth;
    [ObservableProperty] private string? header;
    [ObservableProperty] private PlsoMode plsoMode;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteButtonText))]
    private bool isConfirmingDelete;

    /// <inheritdoc />
    public event Func<Task>? Deleted;
    /// <inheritdoc />
    public event Func<string, Task>? Duplicated;
    /// <inheritdoc />
    public event Action<string>? TitleChanged;

    /// <inheritdoc />
    public bool CanDelete { get; }

    /// <inheritdoc />
    public string DeleteButtonText => IsConfirmingDelete ? "CONFIRM DELETE" : "DELETE";

    private readonly Dictionary<string, IFillInViewModel> fillIns = [];

    /// <inheritdoc />
    public string Id => entity.Id.ToString();
    /// <inheritdoc />
    public ObservableCollection<AddressData> Addresses { get; } = [];
    /// <inheritdoc />
    public ObservableCollection<AddressGroup> AddressGroups { get; } = [];
    /// <inheritdoc />
    public IBodyDocument BodyDocument { get; }
    /// <inheritdoc />
    public IReadOnlyDictionary<string, IFillInViewModel> FillIns => fillIns;
    /// <inheritdoc />
    public IReadOnlyList<string> AllUserNames { get; }
    /// <inheritdoc />
    public IReadOnlyList<AddressTypeOption> AddressTypes { get; }
    /// <inheritdoc />
    public IReadOnlyList<SecurityLevel> AvailableSecurityLevels { get; }
    /// <inheritdoc />
    public string FilterTag(string tag) => engineController.DraftTagRules.Filter(tag);

    /// <inheritdoc />
    public int? TagMaxLength => engineController.DraftTagRules.MaxLength;
    /// <inheritdoc />
    public bool IsLineWidthAvailable => lineWidthRange is not null;
    /// <inheritdoc />
    public decimal LineWidthMinimum => MinimumWidth;
    /// <inheritdoc />
    public decimal LineWidthMaximum => Math.Max(lineWidthRange?.Max ?? 1000, MinimumWidth);
    /// <inheritdoc />
    public decimal? LineWidthValue
    {
        get => LineWidth;
        set => LineWidth = lineWidthRange is null ? null : value is { } width ? ClampWidth((int)width) : lineWidthRange.Max is null ? null : ClampWidth(lineWidthRange.Max.Value);
    }

    /// <inheritdoc />
    public string AlertLabel { get; }
    /// <inheritdoc />
    public bool IsAlert { get => isAlert; private set => SetProperty(ref isAlert, value); }
    /// <inheritdoc />
    public bool TagsEnabled { get; }
    /// <inheritdoc />
    public string TagLabel { get; }
    /// <inheritdoc />
    public string PlsoButtonText => PlsoMode switch
    {
        PlsoMode.Off => "PLSO OFF",
        PlsoMode.On => "PLSO ON",
        PlsoMode.Spaces => "PLSO SPACES",
        _ => "PLSO OFF"
    };

    /// <inheritdoc />
    public event Func<IDraftViewModel, MessageEntity, Task>? DraftSent;

    private void LoadBody(DraftEntity entity)
    {
        fillIns.Clear();

        if (!string.IsNullOrEmpty(entity.BodySegmentsJson))
        {
            List<DraftBodySegmentData> dataList = DeserializeSegments(entity.BodySegmentsJson);
            System.Text.StringBuilder sb = new();
            foreach (DraftBodySegmentData seg in dataList)
            {
                if (seg.Kind == "fillin")
                {
                    string id = NormalizeId(seg.FillInId ?? GenerateFillInId());
                    fillIns[id] = new FillInViewModel(id, seg.Options, seg.Selected);
                    sb.Append(FillInSentinel).Append(id);
                }
                else
                {
                    sb.Append(seg.Text ?? string.Empty);
                }
            }
            BodyDocument.Text = sb.ToString();
        }
        else
        {
            BodyDocument.Text = entity.Body ?? string.Empty;
        }
    }

    partial void OnNewAddressUserChanged(string value)
    {
        string upper = value.ToUpperInvariant();
        if (value != upper) { NewAddressUser = upper; }
    }

    partial void OnPlsoModeChanged(PlsoMode value) => OnPropertyChanged(nameof(PlsoButtonText));

    private IReadOnlyList<MessagePriorityOption> FilterPriorities(string tag)
        => allPriorities.Where(p => p.Mode == PriorityMode.User && !engineController.BlockedCombinations.IsBlocked(tag, p.Key)).ToList();

    partial void OnTagChanged(string value)
    {
        // What is typed or pasted is made into a tag the rules allow, and the change is made again with that.
        string allowed = engineController.DraftTagRules.Filter(value);
        if (allowed != value)
        {
            Tag = allowed;
            return;
        }

        if (engineController.BlockedCombinations.IsBlocked(value, SelectedPriority.Key))
        {
            // Reject the change: this combination is blocked, so revert to the last valid tag instead of
            // letting the blocked value stand. Re-enters this method with a value that is never blocked
            // (by invariant, lastValidTag was itself accepted previously), so this does not recurse further.
            Tag = lastValidTag;
            return;
        }

        lastValidTag = value;
        AvailablePriorities = FilterPriorities(value);
        if (!AvailablePriorities.Contains(SelectedPriority))
        {
            SelectedPriority = AvailablePriorities.FirstOrDefault() ?? SelectedPriority;
        }

        UpdateHeader();
    }

    partial void OnSelectedPriorityChanged(MessagePriorityOption value) => UpdateHeader();

    partial void OnSelectedSecurityLevelChanged(SecurityLevel? value) => UpdateHeader();

    partial void OnLineWidthChanged(int? value) => UpdateHeader();

    private int MinimumWidth => Math.Max(lineWidthRange?.Min ?? 1, headerWidth);

    private int ClampWidth(int width) => Math.Max(Math.Min(width, lineWidthRange?.Max ?? int.MaxValue), MinimumWidth);

    private void UpdateHeader()
    {
        if (!isReady || isUpdatingHeader) { return; }

        isUpdatingHeader = true;
        try
        {
            // The header can depend on the width and the width can not be less than the header, so this settles by raising the width until the header fits.
            for (int pass = 0; pass < 8; pass++)
            {
                IsAlert = engineController.ComputeIsAlert(string.Empty, SelectedPriority.Key, Tag, SelectedSecurityLevel?.Name ?? string.Empty, [.. Addresses.Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type, Information = a.Information })]);
                string? text = engineController.GetDraftHeader(new DraftContent
                {
                    Tag = Tag,
                    Priority = SelectedPriority.Key,
                    SecurityLevel = SelectedSecurityLevel?.Name ?? string.Empty,
                    IsAlert = IsAlert,
                    Addresses = [.. Addresses.Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type, Information = a.Information })],
                    LineWidth = LineWidth
                });
                Header = text;
                headerWidth = lineWidthRange is null || text is null ? 0 : text.Split('\n').Max(line => line.TrimEnd('\r').Length);
                OnPropertyChanged(nameof(LineWidthMinimum));
                OnPropertyChanged(nameof(LineWidthMaximum));

                if (LineWidth is not { } width || width >= MinimumWidth) { break; }

                LineWidth = MinimumWidth;
            }
        }
        finally
        {
            isUpdatingHeader = false;
        }
    }

    /// <inheritdoc />
    public void InsertFillIn(int caretOffset)
    {
        string id = GenerateFillInId();
        fillIns[id] = new FillInViewModel(id, [], null);
        BodyDocument.Insert(caretOffset, $"{FillInSentinel}{id}");
    }

    private string BuildPlainBody()
    {
        string text = BodyDocument.Text;
        System.Text.StringBuilder sb = new();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == FillInSentinel && i + FillInMarkerLength <= text.Length)
            {
                string id = text.Substring(i + 1, FillInIdLength);
                sb.Append(fillIns.TryGetValue(id, out IFillInViewModel? fi) ? fi.SelectedOption ?? "______" : "______");
                i += FillInMarkerLength;
            }
            else
            {
                sb.Append(text[i++]);
            }
        }
        return sb.ToString();
    }

    private string SerializeBody()
    {
        string text = BodyDocument.Text;
        List<DraftBodySegmentData> dataList = new();
        System.Text.StringBuilder sb = new();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == FillInSentinel && i + FillInMarkerLength <= text.Length)
            {
                if (sb.Length > 0)
                {
                    dataList.Add(new DraftBodySegmentData { Kind = "text", Text = sb.ToString() });
                    sb.Clear();
                }
                string id = text.Substring(i + 1, FillInIdLength);
                i += FillInMarkerLength;
                if (fillIns.TryGetValue(id, out IFillInViewModel? fi))
                {
                    dataList.Add(new DraftBodySegmentData
                    {
                        Kind = "fillin",
                        FillInId = id,
                        Options = fi.Options.Select(o => o.Value).ToList(),
                        Selected = fi.SelectedOption
                    });
                }
            }
            else
            {
                sb.Append(text[i++]);
            }
        }
        if (sb.Length > 0)
        {
            dataList.Add(new DraftBodySegmentData { Kind = "text", Text = sb.ToString() });
        }
        return JsonSerializer.Serialize(dataList);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (!CanDelete || !deleteConfirmation.Confirm()) { return; }

        if (!isNew) { await entryService.DeleteEntry(Id, EntryType.Draft); }
        isDeleted = true;
        if (Deleted is not null) { await Deleted(); }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!IsWorthStoring())
        {
            StatusMessage = "Nothing to save";
            return;
        }

        IsSaving = true;
        try
        {
            await Persist(quietly: false);
            StatusMessage = "Saved";
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <inheritdoc />
    public async Task SaveChanges()
    {
        if (IsSent || isDeleted || Snapshot() == savedSnapshot || !IsWorthStoring()) { return; }

        await Persist(quietly: true);
    }

    // A new draft is only stored once it has been altered and is not blank; one that already exists is stored as it is, even if cleared.
    private bool IsWorthStoring()
        => !isNew || (Snapshot() != initialSnapshot && !(BuildPlainBody().Trim().Length == 0 && Addresses.Count == 0 && string.IsNullOrWhiteSpace(Name)));

    private void ApplyToEntity()
    {
        entity.Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim();
        entity.Body = BuildPlainBody();
        entity.BodySegmentsJson = SerializeBody();
        entity.Addresses = [.. Addresses];
        entity.IsAlert = IsAlert;
        entity.Priority = SelectedPriority.Stored;
        entity.Tag = Tag;
        entity.SecurityLevel = SelectedSecurityLevel?.Value;
        entity.LineWidth = LineWidth;
    }

    private async Task Persist(bool quietly)
    {
        ApplyToEntity();
        await InsertIfNew();

        if (quietly) { await entryService.SaveDraftQuietly(entity); }
        else { await entryService.SaveDraft(entity); }

        savedSnapshot = Snapshot();
    }

    [RelayCommand]
    private async Task Duplicate()
    {
        ApplyToEntity();
        DraftEntity copy = await entryService.DuplicateDraft(entity);
        if (Duplicated is not null) { await Duplicated(copy.Id.ToString()); }
    }

    // Everything a save writes, so leaving a draft that was only looked at does not save it and move it to the top of the list.
    private string Snapshot()
        => string.Join('\u001F', Name, SerializeBody(), Tag, SelectedPriority.Stored, SelectedSecurityLevel?.Value, LineWidth, string.Join('\u001E', Addresses.Select(address => $"{address.UserName}\u001D{address.Type}\u001D{address.Information}")));

    [RelayCommand]
    private async Task Send()
    {
        if (Addresses.Count == 0)
        {
            StatusMessage = "Add at least one recipient";
            return;
        }

        if (engineController.BlockedCombinations.IsBlocked(Tag, SelectedPriority.Key))
        {
            StatusMessage = engineController.Display("This tag/priority combination is not allowed");
            return;
        }

        if (TagsEnabled && engineController.DraftTagRules.Validate(Tag) is { } tagError)
        {
            StatusMessage = engineController.Display(tagError);
            return;
        }

        UpdateHeader();
        IsSaving = true;
        try
        {
            string plainBody = BuildPlainBody();
            string body = Header is { } header ? header + "\n" + plainBody : plainBody;
            ApplyToEntity();

            SendMessageResult? result = await connection.SendMessage(
                body,
                Addresses.Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type, Information = a.Information }).ToList(),
                SelectedPriority.Key, Tag, SelectedSecurityLevel?.Key);
            if (result is null)
            {
                StatusMessage = "Cannot send until a user is installed";
                return;
            }

            entity.IsAlert = result.IsAlert;
            entity.IsSent = true;
            entity.SentAt = DateTime.UtcNow;
            await InsertIfNew();
            await entryService.SaveDraft(entity);

            DateTime sentAt = entity.SentAt ?? DateTime.UtcNow;
            MessageEntity sentMessage = await entryService.StoreSentMessage(
                result.MessageId, body, [.. Addresses], sentAt, result.UserResults, SelectedPriority.Key, Tag, SelectedSecurityLevel?.Name ?? string.Empty);

            IsSent = true;
            StatusMessage = "Sent";

            if (DraftSent is not null)
            {
                await DraftSent(this, sentMessage);
            }
        }
        catch (Exception ex)
        {
            activityLogger.LogError(ex, "Message transmission failed for {Preview}", BuildPlainBody().FirstLine);
            StatusMessage = $"Send failed: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void AddAddress()
    {
        if (string.IsNullOrWhiteSpace(NewAddressUser)) { return; }
        Addresses.Add(new AddressData { UserName = NewAddressUser.Trim(), Type = NewAddressType.Type.ToString(), Information = NewAddressInformation.Trim() });
        NewAddressUser = string.Empty;
        NewAddressInformation = string.Empty;
    }

    [RelayCommand]
    private void RemoveAddress(AddressData address) => Addresses.Remove(address);

    [RelayCommand]
    private void MoveAddressUp(AddressData address) => MoveAddress(address, -1);

    [RelayCommand]
    private void MoveAddressDown(AddressData address) => MoveAddress(address, 1);

    private void MoveAddress(AddressData address, int direction)
    {
        int index = Addresses.IndexOf(address);
        if (index < 0) { return; }

        // The next recipient of the same type in that direction is the one to swap places with, skipping the other types in between.
        for (int other = index + direction; other >= 0 && other < Addresses.Count; other += direction)
        {
            if (Addresses[other].Type.ParseAddressType() != address.Type.ParseAddressType()) { continue; }

            Addresses.Move(index, other);
            return;
        }
    }

    private void RebuildAddressGroups()
    {
        AddressGroups.Clear();
        foreach (AddressTypeOption type in AddressTypes)
        {
            List<AddressData> items = [.. Addresses.Where(address => address.Type.ParseAddressType() == type.Type)];
            if (items.Count > 0) { AddressGroups.Add(new AddressGroup(type.Label, items)); }
        }
    }
}
