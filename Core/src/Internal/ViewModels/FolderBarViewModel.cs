namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for the folder tree panel.</summary>
internal interface IFolderBarViewModel
{
    /// <summary>Raised when the user selects a folder in the tree.</summary>
    event Action<FolderItemViewModel>? FolderSelected;
    /// <summary>Raised after an entry is successfully moved to another folder.</summary>
    event Action? EntryMoved;

    /// <summary>Gets or sets the currently selected folder.</summary>
    FolderItemViewModel? SelectedFolder { get; set; }
    /// <summary>Gets the collection of root folder ViewModels displayed in the tree.</summary>
    ObservableCollection<FolderItemViewModel> RootFolders { get; }

    /// <summary>Loads the folder tree from the repository and selects the first root folder.</summary>
    Task Load();
    /// <summary>Marks the given folder as selected, deselecting the previously selected folder.</summary>
    void SelectFolder(FolderItemViewModel folder);
    /// <summary>Clears the current selection, if any, without raising <see cref="FolderSelected"/>.</summary>
    void DeselectFolder();
    /// <summary>Selects the first root folder of the given type, if one exists.</summary>
    void SelectFolderByType(FolderType type);
    /// <summary>Moves the given entry to the target folder if the types are compatible.</summary>
    Task MoveEntry(EntryItemViewModel entry, FolderItemViewModel targetFolder);
    /// <summary>Creates and persists a new subfolder under the given parent, then selects it.</summary>
    Task AddSubfolder(FolderItemViewModel parent, string name);
    /// <summary>Whether <paramref name="folder"/> is a subfolder the host allows deleting; deleting it also deletes everything inside it.</summary>
    bool CanDeleteFolder(FolderItemViewModel folder);
    /// <summary>Permanently deletes a subfolder together with all of its subfolders and every entry in any of them, then selects its parent if the selection was inside it.</summary>
    Task DeleteFolder(FolderItemViewModel folder);
    /// <summary>Collapses all folders in the tree.</summary>
    void CollapseAll();
    /// <summary>Returns <paramref name="text"/>, written with the engine's own names for concepts, with what the host calls them (see <see cref="EngineControllerExtensions"/>).</summary>
    /// <param name="text">The text.</param>
    string Display(string text);
}

/// <summary>ViewModel for the folder tree panel, managing folder loading, selection, and drag-and-drop moves.</summary>
internal sealed partial class FolderBarViewModel : ObservableObject, IFolderBarViewModel
{
    private FolderItemViewModel BuildViewModel(Folder folder, bool? alertView = null)
    {
        FolderItemViewModel vm = new(folder.Id, folder.ParentId is null ? engineController.Display(folder.Name) : folder.Name, folder.RootType, folder.ParentId, alertView);
        foreach (Folder child in folder.Children)
        {
            vm.Children.Add(BuildViewModel(child));
        }
        return vm;
    }

    /// <summary>Returns <see langword="true"/> when an entry of the given type may be moved into a folder of the given type; a message may only move within its own direction's tree (<paramref name="isOutboundMessage"/>).</summary>
    public static bool IsCompatibleMove(EntryType entryType, FolderType folderType, bool isOutboundMessage = false) => entryType switch
    {
        // A received message stays under Inbox and a sent one under Outbox: each tree reads and deletes its entries as
        // its own direction, so a message moved across would no longer open or delete.
        EntryType.Message => folderType == (isOutboundMessage ? FolderType.Outbox : FolderType.Inbox),
        EntryType.Draft => folderType is FolderType.Drafts,
        EntryType.Note => folderType is FolderType.Notes,
        _ => false
    };

    private static FolderItemViewModel? FindParentInTree(FolderItemViewModel node, string childId)
    {
        if (node.Children.Any(c => c.Id == childId))
        {
            return node;
        }
        foreach (FolderItemViewModel child in node.Children)
        {
            if (FindParentInTree(child, childId) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static List<FolderItemViewModel> Flatten(FolderItemViewModel folder)
    {
        List<FolderItemViewModel> all = [folder];
        for (int i = 0; i < all.Count; i++)
        {
            all.AddRange(all[i].Children);
        }
        return all;
    }

    private static void CollapseRecursive(FolderItemViewModel folder)
    {
        folder.IsExpanded = false;
        foreach (FolderItemViewModel child in folder.Children)
        {
            CollapseRecursive(child);
        }
    }

    /// <summary>Initializes a new <see cref="FolderBarViewModel"/> with the required repositories.</summary>
    /// <param name="folders">Repository for loading and persisting folders.</param>
    /// <param name="entryService">Entry service for move and delete operations.</param>
    /// <param name="engineController">Host rules, consulted for whether entries may be deleted.</param>
    public FolderBarViewModel(IFolderRepository folders, IEntryService entryService, IEngineController engineController)
    {
        this.folders = folders;
        this.entryService = entryService;
        this.engineController = engineController;
    }

    private readonly IFolderRepository folders;
    private readonly IEntryService entryService;
    private readonly IEngineController engineController;

    [ObservableProperty] private FolderItemViewModel? selectedFolder;

    /// <summary>Gets the collection of root folder ViewModels displayed in the tree.</summary>
    public ObservableCollection<FolderItemViewModel> RootFolders { get; } = [];

    /// <summary>Raised when the user selects a folder in the tree.</summary>
    public event Action<FolderItemViewModel>? FolderSelected;
    /// <summary>Raised after an entry is successfully moved to another folder.</summary>
    public event Action? EntryMoved;

    /// <summary>Loads the folder tree from the repository and selects the first root folder.</summary>
    public async Task Load()
    {
        List<Folder> tree = await folders.GetTree();
        RootFolders.Clear();

        FolderType[] rootOrder = [FolderType.Inbox, FolderType.Outbox, FolderType.Drafts, FolderType.Notes, FolderType.Activity];
        foreach (FolderType rootType in rootOrder)
        {
            Folder? rootFolder = tree.FirstOrDefault(f => f.ParentId is null && f.RootType == rootType);
            if (rootFolder is null)
            {
                continue;
            }

            bool isSeparated = rootType is FolderType.Inbox or FolderType.Outbox && engineController.SeparateAlerts;
            if (isSeparated)
            {
                RootFolders.Add(new FolderItemViewModel($"{rootFolder.Id}-alerts", engineController.Display($"Alert {rootFolder.Name}"), rootType, alertView: true, storageId: rootFolder.Id));
            }

            RootFolders.Add(BuildViewModel(rootFolder, isSeparated ? false : null));
        }

        if (RootFolders.Count > 0)
        {
            SelectFolder(RootFolders[0]);
        }
    }

    /// <summary>Marks the given folder as selected, deselecting the previously selected folder.</summary>
    public void SelectFolder(FolderItemViewModel folder)
    {
        if (SelectedFolder == folder)
        {
            return;
        }

        if (SelectedFolder is not null)
        {
            SelectedFolder.IsSelected = false;
        }

        SelectedFolder = folder;
        folder.IsSelected = true;
        FolderSelected?.Invoke(folder);
    }

    /// <summary>Clears the current selection, if any, without raising <see cref="FolderSelected"/>.</summary>
    public void DeselectFolder()
    {
        if (SelectedFolder is null)
        {
            return;
        }
        SelectedFolder.IsSelected = false;
        SelectedFolder = null;
    }

    /// <summary>Selects the first root folder of the given type, if one exists.</summary>
    public void SelectFolderByType(FolderType type)
    {
        FolderItemViewModel? folder = RootFolders.FirstOrDefault(f => f.RootType == type);
        if (folder is not null)
        {
            SelectFolder(folder);
        }
    }

    /// <summary>Moves the given entry to the target folder if the types are compatible.</summary>
    public async Task MoveEntry(EntryItemViewModel entry, FolderItemViewModel targetFolder)
    {
        if (!IsCompatibleMove(entry.EntryType, targetFolder.RootType, entry.IsOutboundMessage))
        {
            return;
        }
        await entryService.MoveEntry(entry.Id, entry.EntryType, targetFolder.StorageId, entry.IsOutboundMessage);
        EntryMoved?.Invoke();
    }

    /// <summary>Creates and persists a new subfolder under the given parent, then selects it.</summary>
    public async Task AddSubfolder(FolderItemViewModel parent, string name)
    {
        if (parent.RootType is FolderType.Activity)
        {
            return;
        }

        FolderEntity entity = new()
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            RootType = parent.RootType,
            ParentId = parent.Id
        };

        await folders.Insert(entity);
        FolderItemViewModel child = new(entity.Id, entity.Name, entity.RootType, entity.ParentId);
        parent.Children.Add(child);
        parent.IsExpanded = true;
        SelectFolder(child);
    }

    /// <inheritdoc />
    public string Display(string text) => engineController.Display(text);

    /// <inheritdoc />
    public bool CanDeleteFolder(FolderItemViewModel folder)
        => folder.IsSubfolder && engineController.CanDelete(folder.RootType);

    /// <inheritdoc />
    public async Task DeleteFolder(FolderItemViewModel folder)
    {
        if (!CanDeleteFolder(folder))
        {
            return;
        }
        FolderItemViewModel? parent = FindParent(folder.Id);
        if (parent is null)
        {
            return;
        }

        List<FolderItemViewModel> doomedFolders = Flatten(folder);
        bool selectionInside = SelectedFolder is { } selected && doomedFolders.Contains(selected);
        doomedFolders.Reverse();
        foreach (FolderItemViewModel doomed in doomedFolders)
        {
            await entryService.DeleteFolderContents(doomed.Id);
            await folders.Delete(doomed.Id);
        }

        parent.Children.Remove(folder);
        if (selectionInside)
        {
            SelectFolder(parent);
        }
    }

    private FolderItemViewModel? FindParent(string childId)
    {
        foreach (FolderItemViewModel root in RootFolders)
        {
            if (FindParentInTree(root, childId) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>Collapses all folders in the tree.</summary>
    public void CollapseAll()
    {
        foreach (FolderItemViewModel root in RootFolders)
        {
            CollapseRecursive(root);
        }
    }
}
