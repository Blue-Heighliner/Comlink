namespace BlueHeighliner.Comlink.Engine.Views.Controls;

/// <summary>User control for the entry list panel, including drag-and-drop initiation for moving entries between folders.</summary>
[ExcludeFromCodeCoverage]
public partial class EntryBar : UserControl
{
    private Point _dragStartPoint;
    private EntryItemViewModel? _pendingDrag;

    /// <summary>Initializes the control, loads the AXAML layout, and registers drag-initiation handlers.</summary>
    public EntryBar()
    {
        InitializeComponent();
        EntryList.AddHandler(InputElement.PointerPressedEvent, OnEntryPointerPressed, handledEventsToo: true);
        EntryList.AddHandler(InputElement.PointerMovedEvent, OnEntryPointerMoved, handledEventsToo: true);
        EntryList.AddHandler(InputElement.PointerReleasedEvent, OnEntryPointerReleased, handledEventsToo: true);
    }

    private void OnEntrySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not IEntryBarViewModel vm) return;

        vm.SelectEntries(
            e.AddedItems.OfType<EntryItemViewModel>().ToList(),
            e.RemovedItems.OfType<EntryItemViewModel>().ToList());
    }

    private void OnEntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(EntryList).Properties.IsLeftButtonPressed) return;
        _pendingDrag = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as EntryItemViewModel;
        _dragStartPoint = e.GetPosition(EntryList);
    }

    private void OnEntryPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pendingDrag = null;
    }

    private async void OnEntryPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pendingDrag is null) return;

        var pos = e.GetPosition(EntryList);
        var dx = Math.Abs(pos.X - _dragStartPoint.X);
        var dy = Math.Abs(pos.Y - _dragStartPoint.Y);
        if (dx < 5 && dy < 5) return;

        if (!e.GetCurrentPoint(EntryList).Properties.IsLeftButtonPressed) { _pendingDrag = null; return; }

        var entry = _pendingDrag;
        _pendingDrag = null;

        var data = new DataObject();
        data.Set("entry", entry);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }
}
