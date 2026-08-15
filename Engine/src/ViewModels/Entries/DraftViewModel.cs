namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>ViewModel interface for composing and sending a draft message.</summary>
public interface IDraftViewModel
{
    /// <summary>Raised after the draft is successfully sent, providing the resulting message entity.</summary>
    event Func<IDraftViewModel, MessageEntity, Task>? DraftSent;

    /// <summary>Gets the LiteDB object-id string for this draft.</summary>
    string Id { get; }
    /// <summary>Gets or sets the message subject.</summary>
    string Subject { get; set; }
    /// <summary>Gets or sets the user name being typed into the address field (auto-uppercased).</summary>
    string NewAddressUser { get; set; }
    /// <summary>Gets or sets the address type selected in the address field.</summary>
    string NewAddressType { get; set; }
    /// <summary>Gets or sets a value indicating whether this draft has been sent.</summary>
    bool IsSent { get; set; }
    /// <summary>Gets or sets a value indicating whether this draft will be sent as an alert.</summary>
    bool IsAlert { get; set; }
    /// <summary>
    /// Gets the label for the alert checkbox, sourced from <see cref="IEngineController.AlertLabel"/> — the
    /// same text shown in the title bar's alert box, so both surfaces always agree on what "alert" is called.
    /// </summary>
    string AlertLabel { get; }
    /// <summary>Gets a value indicating whether the alert checkbox is shown; see <see cref="IEngineController"/>.</summary>
    bool ComposeAlertsEnabled { get; }
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
    /// <see cref="Entries.PlsoMode.Off"/>, typing a letter or digit inserts its phonetic word (see
    /// <see cref="PhoneticAlphabet"/>) instead of the character itself, with a trailing space added
    /// after each word when <see cref="Entries.PlsoMode.Spaces"/>. Editor-session-only UI state — not
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
    /// <summary>Gets all known user names available for recipient auto-complete.</summary>
    IReadOnlyList<string> AllUserNames { get; }
    /// <summary>Gets the list of valid address type labels.</summary>
    IReadOnlyList<string> AddressTypes { get; }
    /// <summary>Saves the current draft state to the data store.</summary>
    IAsyncRelayCommand SaveCommand { get; }
    /// <summary>Sends the draft as a message.</summary>
    IAsyncRelayCommand SendCommand { get; }
    /// <summary>Adds the current <see cref="NewAddressUser"/> and <see cref="NewAddressType"/> as a recipient.</summary>
    IRelayCommand AddAddressCommand { get; }
    /// <summary>Removes the specified address from the recipient list.</summary>
    IRelayCommand<AddressData> RemoveAddressCommand { get; }

    /// <summary>Inserts a new fill-in marker into the body document at the specified caret offset.</summary>
    void InsertFillIn(int caretOffset);
}

/// <summary>ViewModel for composing and sending a draft message, including fill-in field management.</summary>
public sealed partial class DraftViewModel : ObservableObject, IDraftViewModel
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
    public DraftViewModel(
        DraftEntity entity,
        IEntryService entryService,
        IServiceConnection connection,
        IReadOnlyList<string> userNames,
        ILoggerFactory loggerFactory,
        IEngineController engineController,
        IBodyDocument? bodyDocument = null)
    {
        this.entity = entity;
        this.entryService = entryService;
        this.connection = connection;
        this.engineController = engineController;
        activityLogger = loggerFactory.CreateLogger("ACTIVITY");
        subject = entity.Subject;
        isSent = entity.IsSent;
        isAlert = entity.IsAlert;
        tag = entity.Tag;
        lastValidTag = entity.Tag;
        AllUserNames = userNames;
        BodyDocument = bodyDocument ?? new StringBodyDocument();
        AlertLabel = engineController.AlertLabel;
        ComposeAlertsEnabled = engineController.ComposeAlertsEnabled;
        TagsEnabled = engineController.TagsEnabled;
        TagLabel = engineController.TagLabel;

        allPriorities = engineController.Priorities;
        availablePriorities = FilterPriorities(entity.Tag);
        selectedPriority = AvailablePriorities.FirstOrDefault(p => p.Value == entity.Priority)
            ?? AvailablePriorities.FirstOrDefault()
            ?? new MessagePriorityOption { Name = "Normal", Value = 0 };

        foreach (AddressData a in entity.Addresses)
        {
            Addresses.Add(a);
        }

        LoadBody(entity);
    }

    private readonly IEntryService entryService;
    private readonly IServiceConnection connection;
    private readonly IEngineController engineController;
    private readonly IReadOnlyList<MessagePriorityOption> allPriorities;
    private readonly ILogger activityLogger;
    private DraftEntity entity;
    private string lastValidTag = string.Empty;

    [ObservableProperty] private string subject;
    [ObservableProperty] private string newAddressUser = string.Empty;
    [ObservableProperty] private string newAddressType = "To";
    [ObservableProperty] private bool isSent;
    [ObservableProperty] private bool isAlert;
    [ObservableProperty] private MessagePriorityOption selectedPriority;
    [ObservableProperty] private IReadOnlyList<MessagePriorityOption> availablePriorities = [];
    [ObservableProperty] private string tag = string.Empty;
    [ObservableProperty] private PlsoMode plsoMode;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string? statusMessage;

    private readonly Dictionary<string, IFillInViewModel> _fillIns = [];

    /// <inheritdoc />
    public string Id => entity.Id.ToString();
    /// <inheritdoc />
    public ObservableCollection<AddressData> Addresses { get; } = [];
    /// <inheritdoc />
    public IBodyDocument BodyDocument { get; }
    /// <inheritdoc />
    public IReadOnlyDictionary<string, IFillInViewModel> FillIns => _fillIns;
    /// <inheritdoc />
    public IReadOnlyList<string> AllUserNames { get; }
    /// <inheritdoc />
    public IReadOnlyList<string> AddressTypes { get; } = ["To", "Cc"];
    /// <inheritdoc />
    public string AlertLabel { get; }
    /// <inheritdoc />
    public bool ComposeAlertsEnabled { get; }
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
        _fillIns.Clear();

        if (!string.IsNullOrEmpty(entity.BodySegmentsJson))
        {
            List<DraftBodySegmentData> dataList = DeserializeSegments(entity.BodySegmentsJson);
            System.Text.StringBuilder sb = new();
            foreach (DraftBodySegmentData seg in dataList)
            {
                if (seg.Kind == "fillin")
                {
                    string id = NormalizeId(seg.FillInId ?? GenerateFillInId());
                    _fillIns[id] = new FillInViewModel(id, seg.Options, seg.Selected);
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
        => allPriorities.Where(p => !engineController.BlockedCombinations.IsBlocked(tag, p.Value)).ToList();

    partial void OnTagChanged(string value)
    {
        if (engineController.BlockedCombinations.IsBlocked(value, SelectedPriority.Value))
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
    }

    /// <inheritdoc />
    public void InsertFillIn(int caretOffset)
    {
        string id = GenerateFillInId();
        _fillIns[id] = new FillInViewModel(id, [], null);
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
                sb.Append(_fillIns.TryGetValue(id, out IFillInViewModel? fi) ? fi.SelectedOption ?? "______" : "______");
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
                if (_fillIns.TryGetValue(id, out IFillInViewModel? fi))
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
    private async Task Save()
    {
        IsSaving = true;
        try
        {
            entity.Subject = Subject;
            entity.Body = BuildPlainBody();
            entity.BodySegmentsJson = SerializeBody();
            entity.Addresses = [.. Addresses];
            entity.IsAlert = IsAlert;
            entity.Priority = SelectedPriority.Value;
            entity.Tag = Tag;
            await entryService.SaveDraft(entity);
            StatusMessage = "Saved";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task Send()
    {
        if (Addresses.Count == 0)
        {
            StatusMessage = "Add at least one recipient";
            return;
        }

        if (engineController.BlockedCombinations.IsBlocked(Tag, SelectedPriority.Value))
        {
            StatusMessage = "This tag/priority combination is not allowed";
            return;
        }

        IsSaving = true;
        try
        {
            string body = BuildPlainBody();
            entity.Subject = Subject;
            entity.Body = body;
            entity.BodySegmentsJson = SerializeBody();
            entity.Addresses = [.. Addresses];
            entity.IsAlert = IsAlert;
            entity.Priority = SelectedPriority.Value;
            entity.Tag = Tag;

            SendMessageResult? result = await connection.SendMessage(
                Subject, body,
                Addresses.Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type }).ToList(),
                IsAlert, SelectedPriority.Value, Tag);

            entity.IsSent = true;
            entity.SentAt = DateTime.UtcNow;
            await entryService.SaveDraft(entity);

            DateTime sentAt = entity.SentAt ?? DateTime.UtcNow;
            MessageEntity sentMessage = await entryService.StoreSentMessage(
                result!.MessageId, Subject, body, [.. Addresses], sentAt, result.UserResults, IsAlert, SelectedPriority.Value, Tag);

            IsSent = true;
            StatusMessage = "Sent";

            if (DraftSent is not null)
            {
                await DraftSent(this, sentMessage);
            }
        }
        catch (Exception ex)
        {
            activityLogger.LogError(ex, "Message transmission failed for {Subject}", Subject);
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
        Addresses.Add(new AddressData { UserName = NewAddressUser.Trim(), Type = NewAddressType });
        NewAddressUser = string.Empty;
    }

    [RelayCommand]
    private void RemoveAddress(AddressData address) => Addresses.Remove(address);
}
