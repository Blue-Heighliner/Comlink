namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>ViewModel interface for the folder tree panel.</summary>
public interface IFolderBarViewModel
{
    /// <summary>Gets or sets the currently selected folder.</summary>
    FolderItemViewModel? SelectedFolder { get; set; }
    /// <summary>Gets the collection of root folder ViewModels displayed in the tree.</summary>
    ObservableCollection<FolderItemViewModel> RootFolders { get; }
    /// <summary>Raised when the user selects a folder in the tree.</summary>
    event Action<FolderItemViewModel>? FolderSelected;
    /// <summary>Raised after an entry is successfully moved to another folder.</summary>
    event Action? EntryMoved;
    /// <summary>Loads the folder tree from the repository and selects the first root folder.</summary>
    Task Load();
    /// <summary>Marks the given folder as selected, deselecting the previously selected folder.</summary>
    void SelectFolder(FolderItemViewModel folder);
    /// <summary>Selects the first root folder of the given type, if one exists.</summary>
    void SelectFolderByType(FolderType type);
    /// <summary>Moves the given entry to the target folder if the types are compatible.</summary>
    Task MoveEntry(EntryItemViewModel entry, FolderItemViewModel targetFolder);
    /// <summary>Creates and persists a new subfolder under the given parent, then selects it.</summary>
    Task AddSubfolder(FolderItemViewModel parent, string name);
    /// <summary>Deletes an empty subfolder and removes it from the tree.</summary>
    Task DeleteFolder(FolderItemViewModel folder);
    /// <summary>Collapses all folders in the tree.</summary>
    void CollapseAll();
}

/// <summary>ViewModel for the folder tree panel, managing folder loading, selection, and drag-and-drop moves.</summary>
public sealed partial class FolderBarViewModel : ObservableObject, IFolderBarViewModel
{
    private readonly IFolderRepository _folders;
    private readonly IEntryService _entryService;

    [ObservableProperty] private FolderItemViewModel? _selectedFolder;

    /// <summary>Gets the collection of root folder ViewModels displayed in the tree.</summary>
    public ObservableCollection<FolderItemViewModel> RootFolders { get; } = [];

    /// <summary>Raised when the user selects a folder in the tree.</summary>
    public event Action<FolderItemViewModel>? FolderSelected;
    /// <summary>Raised after an entry is successfully moved to another folder.</summary>
    public event Action? EntryMoved;

    /// <summary>Initializes a new <see cref="FolderBarViewModel"/> with the required repositories.</summary>
    /// <param name="folders">Repository for loading and persisting folders.</param>
    /// <param name="entryService">Entry service for move operations.</param>
    public FolderBarViewModel(IFolderRepository folders, IEntryService entryService)
    {
        _folders = folders;
        _entryService = entryService;
    }

    /// <summary>Loads the folder tree from the repository and selects the first root folder.</summary>
    public async Task Load()
    {
        List<Folder> tree = await _folders.GetTree();
        RootFolders.Clear();

        FolderType[] rootOrder = [FolderType.Inbox, FolderType.Outbox, FolderType.Drafts, FolderType.Notes, FolderType.Activity];
        foreach (FolderType rootType in rootOrder)
        {
            Folder? rootFolder = tree.FirstOrDefault(f => f.ParentId is null && f.RootType == rootType);
            if (rootFolder is not null)
                RootFolders.Add(BuildViewModel(rootFolder));
        }

        if (RootFolders.Count > 0)
            SelectFolder(RootFolders[0]);
    }

    private static FolderItemViewModel BuildViewModel(Folder folder)
    {
        FolderItemViewModel vm = new(folder.Id, folder.Name, folder.RootType, folder.ParentId);
        foreach (Folder child in folder.Children)
            vm.Children.Add(BuildViewModel(child));
        return vm;
    }

    /// <summary>Marks the given folder as selected, deselecting the previously selected folder.</summary>
    public void SelectFolder(FolderItemViewModel folder)
    {
        if (SelectedFolder == folder) return;

        if (SelectedFolder is not null)
            SelectedFolder.IsSelected = false;

        SelectedFolder = folder;
        folder.IsSelected = true;
        FolderSelected?.Invoke(folder);
    }

    /// <summary>Selects the first root folder of the given type, if one exists.</summary>
    public void SelectFolderByType(FolderType type)
    {
        FolderItemViewModel? folder = RootFolders.FirstOrDefault(f => f.RootType == type);
        if (folder is not null) SelectFolder(folder);
    }

    /// <summary>Moves the given entry to the target folder if the types are compatible.</summary>
    public async Task MoveEntry(EntryItemViewModel entry, FolderItemViewModel targetFolder)
    {
        if (!IsCompatibleMove(entry.EntryType, targetFolder.RootType)) return;
        await _entryService.MoveEntry(entry.Id, entry.EntryType, targetFolder.Id, entry.IsOutboundMessage);
        EntryMoved?.Invoke();
    }

    /// <summary>Returns <see langword="true"/> when an entry of the given type may be moved into a folder of the given type.</summary>
    public static bool IsCompatibleMove(EntryType entryType, FolderType folderType) => entryType switch
    {
        EntryType.Message => folderType is FolderType.Inbox or FolderType.Outbox,
        EntryType.Draft   => folderType is FolderType.Drafts,
        EntryType.Note    => folderType is FolderType.Notes,
        _                 => false
    };

    /// <summary>Creates and persists a new subfolder under the given parent, then selects it.</summary>
    public async Task AddSubfolder(FolderItemViewModel parent, string name)
    {
        if (parent.RootType == FolderType.Activity) return;

        Data.Entities.FolderEntity entity = new()
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            RootType = parent.RootType,
            ParentId = parent.Id
        };

        await _folders.Insert(entity);
        FolderItemViewModel child = new(entity.Id, entity.Name, entity.RootType, entity.ParentId);
        parent.Children.Add(child);
        parent.IsExpanded = true;
        SelectFolder(child);
    }

    /// <summary>Deletes an empty subfolder and removes it from the tree, selecting the nearest remaining folder.</summary>
    public async Task DeleteFolder(FolderItemViewModel folder)
    {
        if (!folder.IsSubfolder || folder.Children.Count > 0) return;
        await _folders.Delete(folder.Id);
        FolderItemViewModel? parent = FindParent(folder.Id);
        parent?.Children.Remove(folder);
        if (SelectedFolder == folder)
            SelectFolder(parent ?? RootFolders.FirstOrDefault() ?? folder);
    }

    private FolderItemViewModel? FindParent(string childId)
    {
        foreach (FolderItemViewModel root in RootFolders)
        {
            if (FindParentInTree(root, childId) is { } found)
                return found;
        }
        return null;
    }

    private static FolderItemViewModel? FindParentInTree(FolderItemViewModel node, string childId)
    {
        if (node.Children.Any(c => c.Id == childId)) return node;
        foreach (FolderItemViewModel child in node.Children)
        {
            if (FindParentInTree(child, childId) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>Collapses all folders in the tree.</summary>
    public void CollapseAll()
    {
        foreach (FolderItemViewModel root in RootFolders)
            CollapseRecursive(root);
    }

    private static void CollapseRecursive(FolderItemViewModel folder)
    {
        folder.IsExpanded = false;
        foreach (FolderItemViewModel child in folder.Children)
            CollapseRecursive(child);
    }
}
