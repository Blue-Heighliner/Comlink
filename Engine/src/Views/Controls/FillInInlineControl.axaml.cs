namespace BlueHeighliner.Comlink.Engine.Views.Controls;

/// <summary>Inline user control embedded by <see cref="FillInElementGenerator"/> to render a fill-in field within the draft body editor.</summary>
[ExcludeFromCodeCoverage]
public partial class FillInInlineControl : UserControl
{
    /// <summary>Initializes the control and loads the AXAML layout.</summary>
    public FillInInlineControl()
    {
        InitializeComponent();
    }

    private FillInViewModel? vm;
    private double targetWidth;

    /// <summary>Gets or sets the advance width of a single monospace character, used to size the control to match its display text.</summary>
    public double CharWidth { get; set; }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (vm is not null)
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
        }
        vm = DataContext as FillInViewModel;
        if (vm is not null)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
        }
        RefreshWidth();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FillInViewModel.DisplayText))
        {
            RefreshWidth();
        }
    }

    private void RefreshWidth()
    {
        if (vm is not null && CharWidth > 0)
        {
            targetWidth = vm.DisplayText.Length * CharWidth;
        }
    }

    /// <summary>
    /// AvaloniaEdit measures this control via <c>element.Measure(infinity)</c> to determine how wide the
    /// inline element should be. This override returns the target width (<c>DisplayText</c> chars ×
    /// advance width) so <c>InlineObjectRun.Size</c> picks up the correct value regardless of the
    /// button's natural content size. The button must also have <c>HorizontalAlignment="Stretch"</c> in
    /// AXAML to fill the arranged width.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        Size childSize = base.MeasureOverride(new Size(
            targetWidth > 0 ? targetWidth : double.PositiveInfinity,
            availableSize.Height));
        return targetWidth > 0
            ? new Size(targetWidth, childSize.Height)
            : childSize;
    }
}
