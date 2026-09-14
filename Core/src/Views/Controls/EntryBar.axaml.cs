namespace BlueHeighliner.Comlink.Views.Controls;

/// <summary>User control for the entry list panel, including drag-and-drop initiation for moving entries between folders.</summary>
[ExcludeFromCodeCoverage]
public partial class EntryBar : UserControl
{
    /// <summary>Initializes the control, loads the AXAML layout, and registers drag-initiation handlers.</summary>
    public EntryBar()
    {
        InitializeComponent();
        EntryList.AddHandler(InputElement.PointerPressedEvent, OnEntryPointerPressed, handledEventsToo: true);
        EntryList.AddHandler(InputElement.PointerMovedEvent, OnEntryPointerMoved, handledEventsToo: true);
        EntryList.AddHandler(InputElement.PointerReleasedEvent, OnEntryPointerReleased, handledEventsToo: true);
    }

    private Point dragStartPoint;
    private EntryItemViewModel? pendingDrag;

    private void OnEntrySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not IEntryBarViewModel vm) { return; }

        vm.SelectEntries(
            e.AddedItems.OfType<EntryItemViewModel>().ToList(),
            e.RemovedItems.OfType<EntryItemViewModel>().ToList());
    }

    private void OnEntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(EntryList).Properties.IsLeftButtonPressed) { return; }
        pendingDrag = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as EntryItemViewModel;
        dragStartPoint = e.GetPosition(EntryList);
    }

    private void OnEntryPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        pendingDrag = null;
    }

    private async void OnEntryPointerMoved(object? sender, PointerEventArgs e)
    {
        if (pendingDrag is null) { return; }

        Point pos = e.GetPosition(EntryList);
        double dx = Math.Abs(pos.X - dragStartPoint.X);
        double dy = Math.Abs(pos.Y - dragStartPoint.Y);
        if (dx < 5 && dy < 5) { return; }

        if (!e.GetCurrentPoint(EntryList).Properties.IsLeftButtonPressed) { pendingDrag = null; return; }

        EntryItemViewModel entry = pendingDrag;
        pendingDrag = null;

        DataObject data = new();
        data.Set("entry", entry);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }
}
