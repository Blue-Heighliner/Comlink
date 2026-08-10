namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

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
}

/// <summary>ViewModel for editing and saving a plain-text note entry.</summary>
public sealed partial class NoteViewModel : ObservableObject, INoteViewModel
{
    private readonly IEntryService _entryService;
    private NoteEntity _entity;

    [ObservableProperty] private string _body;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _statusMessage;

    /// <summary>Gets the LiteDB object-id string for this note.</summary>
    public string Id => _entity.Id.ToString();

    /// <summary>Initializes a new <see cref="NoteViewModel"/> for the given note entity.</summary>
    /// <param name="entity">The note entity to display and edit.</param>
    /// <param name="entryService">Entry service for saving changes.</param>
    public NoteViewModel(NoteEntity entity, IEntryService entryService)
    {
        _entity = entity;
        _entryService = entryService;
        _body = entity.Body;
    }

    [RelayCommand]
    private async Task Save()
    {
        IsSaving = true;
        try
        {
            _entity.Body = Body;
            await _entryService.SaveNote(_entity);
            StatusMessage = "Saved";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
