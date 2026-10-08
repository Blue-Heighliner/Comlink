namespace BlueHeighliner.Comlink;

/// <summary>ViewModel representing a single folder node in the folder tree.</summary>
internal sealed partial class FolderItemViewModel : ObservableObject
{
    /// <summary>Initializes a new folder item with the given identity and classification.</summary>
    /// <param name="id">Unique folder identifier.</param>
    /// <param name="name">Display name of the folder.</param>
    /// <param name="rootType">Root content type this folder belongs to.</param>
    /// <param name="parentId">Identifier of the parent folder, or <see langword="null"/> for root folders.</param>
    /// <param name="alertView">For an inbox or outbox root while alerts are kept apart: <see langword="true"/> for the one listing only alerts, <see langword="false"/> for the one listing only non-alerts. <see langword="null"/> for any other folder.</param>
    /// <param name="storageId">The identifier of the stored folder this one lists, when that is not <paramref name="id"/>: the alert inbox and alert outbox list the stored inbox and outbox. Defaults to <paramref name="id"/>.</param>
    public FolderItemViewModel(string id, string name, FolderType rootType, string? parentId = null, bool? alertView = null, string? storageId = null)
    {
        Id = id;
        AlertView = alertView;
        StorageId = storageId ?? id;
        Name = name;
        RootType = rootType;
        ParentId = parentId;
        isExpanded = parentId is null;
    }

    [ObservableProperty] private bool isExpanded = true;
    [ObservableProperty] private bool isSelected;

    /// <summary>Gets the unique folder identifier.</summary>
    public string Id { get; }
    /// <summary>Gets the identifier of the stored folder this folder lists, which differs from <see cref="Id"/> for the alert inbox and alert outbox.</summary>
    public string StorageId { get; }
    /// <summary>Gets whether this folder lists only alerts (<see langword="true"/>) or only non-alerts (<see langword="false"/>) of its stored folder, or <see langword="null"/> when it lists everything in it.</summary>
    public bool? AlertView { get; }
    /// <summary>Gets the display name of the folder.</summary>
    public string Name { get; }
    /// <summary>Gets the root folder type that classifies what this folder holds.</summary>
    public FolderType RootType { get; }
    /// <summary>Gets the identifier of the parent folder, or <see langword="null"/> for root folders.</summary>
    public string? ParentId { get; }
    /// <summary>Gets the collection of child folder ViewModels.</summary>
    public ObservableCollection<FolderItemViewModel> Children { get; } = [];

    /// <summary>Gets a value indicating whether this folder is nested under a root folder.</summary>
    public bool IsSubfolder => ParentId is not null;
    /// <summary>Gets a value indicating whether a new subfolder can be created under this folder.</summary>
    public bool CanCreateSubfolder => !(ParentId is null && (RootType is FolderType.Activity || RootType is FolderType.Outbox)) && AlertView is not true;
    /// <summary>Gets a value indicating whether this is a top-level root folder.</summary>
    public bool IsRootFolder => ParentId is null;
    /// <summary>Gets a value indicating whether the folder label should be displayed in bold (true for root folders).</summary>
    public bool IsLabelBold => IsRootFolder;
    /// <summary>Gets the font size used to label this folder in the UI.</summary>
    public double LabelFontSize => IsRootFolder ? 15.0 : 13.0;

    /// <summary>Gets a value indicating whether this is an alert inbox or alert outbox, which show a doubled arrow in the alert color.</summary>
    public bool IsAlertView => AlertView is true;

    /// <summary>Gets the icon character for root folders, or an empty string for subfolders.</summary>
    public string Icon => ParentId is null ? RootType switch
    {
        FolderType.Inbox => IsAlertView ? "⇊" : "↓",
        FolderType.Outbox => IsAlertView ? "⇈" : "↑",
        FolderType.Drafts => "✎",
        FolderType.Notes => "▤",
        FolderType.Activity => "↻",
        _ => ""
    } : "";
}
