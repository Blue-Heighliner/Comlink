namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>ViewModel representing a single folder node in the folder tree.</summary>
public sealed partial class FolderItemViewModel : ObservableObject
{
    /// <summary>Initializes a new folder item with the given identity and classification.</summary>
    /// <param name="id">Unique folder identifier.</param>
    /// <param name="name">Display name of the folder.</param>
    /// <param name="rootType">Root content type this folder belongs to.</param>
    /// <param name="parentId">Identifier of the parent folder, or <see langword="null"/> for root folders.</param>
    public FolderItemViewModel(string id, string name, FolderType rootType, string? parentId = null)
    {
        Id = id;
        Name = name;
        RootType = rootType;
        ParentId = parentId;
        isExpanded = parentId is null;
    }

    [ObservableProperty] private bool isExpanded = true;
    [ObservableProperty] private bool isSelected;

    /// <summary>Gets the unique folder identifier.</summary>
    public string Id { get; }
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
    public bool CanCreateSubfolder => !(ParentId is null && (RootType == FolderType.Activity || RootType == FolderType.Outbox));
    /// <summary>Gets a value indicating whether this is a top-level root folder.</summary>
    public bool IsRootFolder => ParentId is null;
    /// <summary>Gets a value indicating whether the folder label should be displayed in bold (true for root folders).</summary>
    public bool IsLabelBold => IsRootFolder;
    /// <summary>Gets the font size used to label this folder in the UI.</summary>
    public double LabelFontSize => IsRootFolder ? 15.0 : 13.0;

    /// <summary>Gets the icon character for root folders, or an empty string for subfolders.</summary>
    public string Icon => ParentId is null ? RootType switch
    {
        FolderType.Inbox     => "↓",
        FolderType.Outbox    => "↑",
        FolderType.Drafts    => "✎",
        FolderType.Notes     => "☰",
        FolderType.Activity  => "≡",
        _                    => ""
    } : "";
}
