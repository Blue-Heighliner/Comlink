namespace BlueHeighliner.Comlink.Views.Controls;

/// <summary>User control for the folder tree panel, handling selection, context menus, and drop targets for entry moves.</summary>
[ExcludeFromCodeCoverage]
public partial class FolderBar : UserControl
{
    /// <summary>Initializes the control, loads the AXAML layout, and registers drag-and-drop handlers.</summary>
    public FolderBar()
    {
        InitializeComponent();
        FolderTree.AddHandler(DragDrop.DragOverEvent, OnFolderDragOver);
        FolderTree.AddHandler(DragDrop.DropEvent, OnFolderDrop);
    }

    private void OnFolderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is IFolderBarViewModel vm &&
            e.AddedItems.Count > 0 &&
            e.AddedItems[0] is FolderItemViewModel selected)
        {
            vm.SelectFolder(selected);
        }
    }

    private void OnFolderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source)
        {
            TreeViewItem? item = source.FindAncestorOfType<TreeViewItem>(includeSelf: true);
            if (item?.DataContext is FolderItemViewModel folder)
            {
                folder.IsExpanded = !folder.IsExpanded;
            }
        }
    }

    private void OnCollapseAll(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IFolderBarViewModel vm)
        {
            vm.CollapseAll();
        }
    }

    private void OnFolderContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Source is not Visual source) { return; }
        TreeViewItem? treeItem = source.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        FolderItemViewModel? folder = treeItem?.DataContext as FolderItemViewModel;
        if (folder is null) { return; }

        ContextMenu menu = new();

        if (folder.CanCreateSubfolder)
        {
            MenuItem newItem = new() { Header = "New Folder" };
            newItem.Click += async (_, _) =>
            {
                Window? owner = this.GetVisualRoot() as Window;
                if (owner is null || DataContext is not IFolderBarViewModel vm) { return; }
                FolderNameDialog dialog = new();
                string? name = await dialog.ShowDialog<string?>(owner);
                if (name is not null)
                {
                    await vm.AddSubfolder(folder, name);
                }
            };
            menu.Items.Add(newItem);
        }

        if (folder.IsSubfolder)
        {
            bool canDelete = folder.Children.Count == 0;
            MenuItem deleteItem = new() { Header = "Delete", IsEnabled = canDelete };
            if (!canDelete)
            {
                ToolTip.SetTip(deleteItem, "Folder must be empty to delete");
            }
            deleteItem.Click += async (_, _) =>
            {
                if (DataContext is IFolderBarViewModel vm)
                {
                    await vm.DeleteFolder(folder);
                }
            };
            menu.Items.Add(deleteItem);
        }

        if (menu.Items.Count == 0) { return; }
        menu.Open(treeItem!);
        e.Handled = true;
    }

    private FolderItemViewModel? GetFolderAtPoint(Point point)
    {
        Visual? current = FolderTree.InputHitTest(point) as Visual;
        while (current != null)
        {
            if (current is TreeViewItem tvi)
            {
                return tvi.DataContext as FolderItemViewModel;
            }
            current = current.GetVisualParent();
        }
        return null;
    }

    private void OnFolderDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("entry")) { e.DragEffects = DragDropEffects.None; return; }
        EntryItemViewModel? entry = e.Data.Get("entry") as EntryItemViewModel;
        FolderItemViewModel? folder = GetFolderAtPoint(e.GetPosition(FolderTree));
        e.DragEffects = entry is not null && folder is not null &&
                        FolderBarViewModel.IsCompatibleMove(entry.EntryType, folder.RootType)
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private async void OnFolderDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("entry")) { return; }
        EntryItemViewModel? entry = e.Data.Get("entry") as EntryItemViewModel;
        FolderItemViewModel? folder = GetFolderAtPoint(e.GetPosition(FolderTree));
        if (entry is null || folder is null) { return; }
        if (DataContext is IFolderBarViewModel vm)
        {
            await vm.MoveEntry(entry, folder);
        }
    }
}
