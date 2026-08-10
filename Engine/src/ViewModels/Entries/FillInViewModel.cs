namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>ViewModel interface for a fill-in field embedded in a draft body.</summary>
public interface IFillInViewModel
{
    /// <summary>Gets the unique 8-character hex identifier for this fill-in.</summary>
    string Id { get; }
    /// <summary>Gets the ordered list of option ViewModels for this fill-in.</summary>
    ObservableCollection<FillInOptionViewModel> Options { get; }
    /// <summary>Gets or sets a value indicating whether the options popup is open.</summary>
    bool IsPopupOpen { get; set; }
    /// <summary>Gets or sets the text being typed for a new option.</summary>
    string NewOption { get; set; }
    /// <summary>Gets the value of the currently selected option, or <see langword="null"/> if none is selected.</summary>
    string? SelectedOption { get; }
    /// <summary>Gets the display text for this fill-in: the selected option value or a blank placeholder.</summary>
    string DisplayText { get; }
    /// <summary>Selects an option by value, deselecting any previously selected option.</summary>
    IRelayCommand<string> SelectOptionCommand { get; }
    /// <summary>Removes the option with the given value.</summary>
    IRelayCommand<string> RemoveOptionCommand { get; }
    /// <summary>Adds <see cref="NewOption"/> as a new option entry.</summary>
    IRelayCommand AddOptionCommand { get; }
    /// <summary>Moves the option with the given value one position earlier in the list.</summary>
    IRelayCommand<string> MoveOptionUpCommand { get; }
    /// <summary>Moves the option with the given value one position later in the list.</summary>
    IRelayCommand<string> MoveOptionDownCommand { get; }
    /// <summary>Toggles the options popup open or closed.</summary>
    IRelayCommand TogglePopupCommand { get; }
}

/// <summary>ViewModel for a fill-in field embedded in a draft body, managing options and selection state.</summary>
public sealed partial class FillInViewModel : ObservableObject, IFillInViewModel
{
    /// <summary>Gets the unique 8-character hex identifier for this fill-in.</summary>
    public string Id { get; }

    /// <summary>Gets the ordered list of option ViewModels for this fill-in.</summary>
    public ObservableCollection<FillInOptionViewModel> Options { get; } = [];

    [ObservableProperty] private bool _isPopupOpen;
    [ObservableProperty] private string _newOption = string.Empty;

    /// <summary>Gets the value of the currently selected option, or <see langword="null"/> if none is selected.</summary>
    public string? SelectedOption => Options.FirstOrDefault(o => o.IsSelected)?.Value;

    /// <summary>Gets the text to show for this fill-in: the selected option value or a blank placeholder.</summary>
    public string DisplayText => SelectedOption ?? "______";

    /// <summary>Initializes a new fill-in with a generated GUID identifier and no options.</summary>
    public FillInViewModel()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>Initializes a fill-in with an existing identifier, option list, and optionally a pre-selected value.</summary>
    public FillInViewModel(string id, IEnumerable<string> options, string? selected)
    {
        Id = id;
        foreach (string opt in options)
            Options.Add(new FillInOptionViewModel(opt, opt == selected));
    }

    [RelayCommand]
    private void SelectOption(string value)
    {
        foreach (FillInOptionViewModel opt in Options)
            opt.IsSelected = opt.Value == value && !opt.IsSelected;

        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(DisplayText));
    }

    [RelayCommand]
    private void RemoveOption(string value)
    {
        FillInOptionViewModel? opt = Options.FirstOrDefault(o => o.Value == value);
        if (opt is null) return;
        Options.Remove(opt);
        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(DisplayText));
    }

    [RelayCommand]
    private void AddOption()
    {
        string trimmed = NewOption.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;
        Options.Add(new FillInOptionViewModel(trimmed));
        NewOption = string.Empty;
    }

    [RelayCommand]
    private void MoveOptionUp(string value)
    {
        FillInOptionViewModel? item = Options.FirstOrDefault(o => o.Value == value);
        if (item is null) return;
        int idx = Options.IndexOf(item);
        if (idx <= 0) return;
        Options.RemoveAt(idx);
        Options.Insert(idx - 1, item);
    }

    [RelayCommand]
    private void MoveOptionDown(string value)
    {
        FillInOptionViewModel? item = Options.FirstOrDefault(o => o.Value == value);
        if (item is null) return;
        int idx = Options.IndexOf(item);
        if (idx < 0 || idx >= Options.Count - 1) return;
        Options.RemoveAt(idx);
        Options.Insert(idx + 1, item);
    }

    [RelayCommand]
    private void TogglePopup()
    {
        IsPopupOpen = !IsPopupOpen;
    }
}
