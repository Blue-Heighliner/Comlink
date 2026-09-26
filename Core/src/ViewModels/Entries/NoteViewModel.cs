namespace BlueHeighliner.Comlink.ViewModels.Entries;

/// <summary>ViewModel interface for editing and saving a plain-text note entry.</summary>
public interface INoteViewModel
{
    /// <summary>Gets the LiteDB object-id string for this note.</summary>
    string Id { get; }
    /// <summary>Gets or sets the editable body text of the note.</summary>
    string Body { get; set; }
    /// <summary>Gets or sets a value indicating whether a save is in progress.</summary>
    bool IsSaving { get; set; }
    /// <summary>Gets or sets the status message shown after a save attempt, or <see langword="null"/> when idle.</summary>
    string? StatusMessage { get; set; }
    /// <summary>Saves the current body text to the data store.</summary>
    IAsyncRelayCommand SaveCommand { get; }
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
public sealed partial class NoteViewModel : ObservableObject, INoteViewModel
{
    /// <summary>Initializes a new <see cref="NoteViewModel"/> for the given note entity.</summary>
    /// <param name="entity">The note entity to display and edit.</param>
    /// <param name="entryService">Entry service for saving changes.</param>
    /// <param name="canDelete">Whether the note can be deleted; see <see cref="IEngineController.CanDelete"/>.</param>
    /// <param name="confirmationWindow">How long an armed delete waits for its confirming press; defaults to a few seconds.</param>
    public NoteViewModel(NoteEntity entity, IEntryService entryService, bool canDelete = true, TimeSpan? confirmationWindow = null)
    {
        this.entity = entity;
        this.entryService = entryService;
        CanDelete = canDelete;
        deleteConfirmation = new DeleteConfirmation(pending => IsConfirmingDelete = pending, confirmationWindow);
        body = entity.Body;
    }

    /// <inheritdoc />
    public event Func<Task>? Deleted;

    private readonly IEntryService entryService;
    private readonly DeleteConfirmation deleteConfirmation;
    private NoteEntity entity;

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

        await entryService.DeleteEntry(Id, EntryType.Note);
        if (Deleted is not null) { await Deleted(); }
    }

    [RelayCommand]
    private async Task Save()
    {
        IsSaving = true;
        try
        {
            entity.Body = Body;
            await entryService.SaveNote(entity);
            StatusMessage = "Saved";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
