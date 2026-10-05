namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for editing and saving a plain-text note entry.</summary>
internal interface INoteViewModel
{
    /// <summary>Gets the LiteDB object-id string for this note.</summary>
    string Id { get; }
    /// <summary>Gets or sets the editable body text of the note.</summary>
    string Body { get; set; }
    /// <summary>Raised when what the note is titled in the list changes while it is being edited: its name, or else the first line of its body.</summary>
    event Action<string>? TitleChanged;
    /// <summary>Gets or sets the name the user gave the note, shown in the list instead of the first line of the body. Empty clears it.</summary>
    string Name { get; set; }
    /// <summary>Duplicates the note as it is now into a new note, leaving this one as it was.</summary>
    IAsyncRelayCommand DuplicateCommand { get; }
    /// <summary>Raised with the id of the new note after it has been created by <see cref="DuplicateCommand"/>.</summary>
    event Func<string, Task>? Duplicated;
    /// <summary>Gets or sets a value indicating whether a save is in progress.</summary>
    bool IsSaving { get; set; }
    /// <summary>Gets or sets the status message shown after a save attempt, or <see langword="null"/> when idle.</summary>
    string? StatusMessage { get; set; }
    /// <summary>Saves the current body text to the data store.</summary>
    IAsyncRelayCommand SaveCommand { get; }
    /// <summary>Saves the body text without saying so, which is what happens when the user leaves the note. Does nothing for a note that was deleted.</summary>
    Task SaveChanges();
    /// <summary>Gets a value indicating whether the note can be deleted, per <see cref="IEngineController.CanDelete"/> for notes.</summary>
    bool CanDelete { get; }
    /// <summary>Gets a value indicating whether a delete is armed and the next <see cref="DeleteCommand"/> press will carry it out.</summary>
    bool IsConfirmingDelete { get; }
    /// <summary>Gets the text for the delete button: <c>"DELETE"</c>, or <c>"CONFIRM DELETE"</c> once armed.</summary>
    string DeleteButtonText { get; }
    /// <summary>Deletes the note. The first press arms a confirmation and a second one within a few seconds deletes it.</summary>
    IAsyncRelayCommand DeleteCommand { get; }
    /// <summary>Raised after the note has been deleted.</summary>
    event Func<Task>? Deleted;
}

/// <summary>ViewModel for editing and saving a plain-text note entry.</summary>
[ConstructedManually]
internal sealed partial class NoteViewModel : ObservableObject, INoteViewModel
{
    /// <summary>Initializes a new <see cref="NoteViewModel"/> for the given note entity.</summary>
    /// <param name="entity">The note entity to display and edit.</param>
    /// <param name="entryService">Entry service for saving changes.</param>
    /// <param name="canDelete">Whether the note can be deleted; see <see cref="IEngineController.CanDelete"/>.</param>
    /// <param name="confirmationWindow">How long an armed delete waits for its confirming press; defaults to a few seconds.</param>
    /// <param name="isNew">Whether the note has not been stored yet; it is only stored once it is altered and not blank.</param>
    public NoteViewModel(NoteEntity entity, IEntryService entryService, bool canDelete = true, TimeSpan? confirmationWindow = null, bool isNew = false)
    {
        this.isNew = isNew;
        name = entity.Name ?? string.Empty;
        this.entity = entity;
        this.entryService = entryService;
        CanDelete = canDelete;
        deleteConfirmation = new DeleteConfirmation(pending => IsConfirmingDelete = pending, confirmationWindow);
        body = entity.Body ?? string.Empty;
    }

    /// <inheritdoc />
    public event Func<Task>? Deleted;
    /// <inheritdoc />
    public event Func<string, Task>? Duplicated;
    /// <inheritdoc />
    public event Action<string>? TitleChanged;

    private bool isNew;
    private readonly IEntryService entryService;
    private readonly DeleteConfirmation deleteConfirmation;
    private bool isDeleted;
    private NoteEntity entity;

    [ObservableProperty] private string name;

    private void RaiseTitleChanged() => TitleChanged?.Invoke(string.IsNullOrWhiteSpace(Name) ? (Body ?? string.Empty).Split('\n')[0].Trim() : Name.Trim());

    partial void OnNameChanged(string value)
    {
        RaiseTitleChanged();
        StoreIfNew();
    }

    partial void OnBodyChanged(string value)
    {
        RaiseTitleChanged();
        StoreIfNew();
    }

    // The first time a new note is altered into something worth keeping it is stored, so it shows up in the list straight away.
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
            RaiseTitleChanged();
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
            if (!isNew || isDeleted) { return; }

            await entryService.InsertNote(entity);
            isNew = false;
        }
        finally
        {
            insertLock.Release();
        }
    }
    [ObservableProperty] private string body;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteButtonText))]
    private bool isConfirmingDelete;

    /// <inheritdoc />
    public bool CanDelete { get; }

    /// <inheritdoc />
    public string DeleteButtonText => IsConfirmingDelete ? "CONFIRM DELETE" : "DELETE";

    /// <summary>Gets the LiteDB object-id string for this note.</summary>
    public string Id => entity.Id.ToString();

    [RelayCommand]
    private async Task Delete()
    {
        if (!CanDelete || !deleteConfirmation.Confirm()) { return; }

        await insertLock.WaitAsync();
        bool wasStored;
        try
        {
            wasStored = !isNew;
            isDeleted = true;
        }
        finally
        {
            insertLock.Release();
        }

        if (wasStored) { await entryService.DeleteEntry(Id, EntryType.Note); }
        if (Deleted is not null) { await Deleted(); }
    }

    private string? StoredName => string.IsNullOrWhiteSpace(Name) ? null : Name.Trim();

    private bool IsChanged => Body != (entity.Body ?? string.Empty) || StoredName != entity.Name;

    // A new note is only stored once it has been altered and is not blank; one that already exists is stored as it is, even if cleared.
    private bool IsWorthStoring => !isNew || (IsChanged && !(Body.Trim().Length == 0 && StoredName is null));

    /// <inheritdoc />
    public async Task SaveChanges()
    {
        if (isDeleted || !IsChanged || !IsWorthStoring) { return; }

        entity.Body = Body;
        entity.Name = StoredName;
        await Store(quietly: true);
    }

    private async Task Store(bool quietly)
    {
        await InsertIfNew();

        if (quietly) { await entryService.SaveNoteQuietly(entity); }
        else { await entryService.SaveNote(entity); }
    }

    [RelayCommand]
    private async Task Duplicate()
    {
        entity.Body = Body;
        entity.Name = StoredName;
        NoteEntity copy = await entryService.DuplicateNote(entity);
        if (Duplicated is not null) { await Duplicated(copy.Id.ToString()); }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!IsWorthStoring)
        {
            StatusMessage = "Nothing to save";
            return;
        }

        IsSaving = true;
        try
        {
            entity.Body = Body;
            entity.Name = StoredName;
            await Store(quietly: false);
            StatusMessage = "Saved";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
