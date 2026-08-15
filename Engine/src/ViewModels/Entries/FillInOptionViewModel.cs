namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>Represents a single selectable option within a fill-in field.</summary>
public sealed partial class FillInOptionViewModel : ObservableObject
{
    [ObservableProperty] private string value;
    [ObservableProperty] private bool isSelected;

    /// <summary>Initializes a new fill-in option with the given value and selection state.</summary>
    /// <param name="value">The text value of this option.</param>
    /// <param name="isSelected">Whether this option is initially selected.</param>
    public FillInOptionViewModel(string value, bool isSelected = false)
    {
        this.value = value;
        this.isSelected = isSelected;
    }
}
