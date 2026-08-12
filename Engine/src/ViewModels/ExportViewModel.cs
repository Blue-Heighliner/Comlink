namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>
/// ViewModel for the export screen: choosing a destination drive, a package file name, and either all entries
/// or an explicitly built list of entries, then writing them out as JSON files inside a zip archive named with
/// the <see cref="IExportService.PackageExtension"/> extension so <see cref="IImportViewModel"/> can find it.
/// Registered as a DI singleton (see <see cref="MainViewModel.Export"/>) so its state — including an
/// in-progress export — survives navigating the content area away to other views and back.
/// </summary>
public interface IExportViewModel
{
    /// <summary>Gets the external drives currently available as an export destination.</summary>
    IReadOnlyList<ExternalDriveInfo> AvailableDrives { get; }
    /// <summary>Gets or sets the drive selected as the export destination.</summary>
    ExternalDriveInfo? SelectedDrive { get; set; }
    /// <summary>Gets or sets the name (without extension) of the export package to create.</summary>
    string FileName { get; set; }
    /// <summary>Gets or sets whether to export every entry or only <see cref="SelectedEntries"/>.</summary>
    ExportScope Scope { get; set; }
    /// <summary>Gets a value indicating whether <see cref="Scope"/> is <see cref="ExportScope.All"/>.</summary>
    bool IsAllScope { get; set; }
    /// <summary>Gets a value indicating whether <see cref="Scope"/> is <see cref="ExportScope.Some"/>.</summary>
    bool IsSomeScope { get; set; }
    /// <summary>Gets the entries explicitly added for export when <see cref="Scope"/> is <see cref="ExportScope.Some"/>.</summary>
    ObservableCollection<EntryItemViewModel> SelectedEntries { get; }
    /// <summary>
    /// Gets a value indicating whether entries clicked in the entry list should be added to
    /// <see cref="SelectedEntries"/> instead of opened — <see langword="true"/> while <see cref="Scope"/> is
    /// <see cref="ExportScope.Some"/> and no export is currently running.
    /// </summary>
    bool IsCollectingEntries { get; }
    /// <summary>Gets a value indicating whether an export is currently running.</summary>
    bool IsExporting { get; }
    /// <summary>Gets the status message displayed after (or during) an export attempt.</summary>
    string? StatusMessage { get; }
    /// <summary>Re-scans for available external drives, preserving <see cref="SelectedDrive"/> if it is still present.</summary>
    IRelayCommand RefreshDrivesCommand { get; }
    /// <summary>Adds an entry to <see cref="SelectedEntries"/> if not already present.</summary>
    void AddEntry(EntryItemViewModel entry);
    /// <summary>Removes an entry from <see cref="SelectedEntries"/>.</summary>
    IRelayCommand<EntryItemViewModel> RemoveEntryCommand { get; }
    /// <summary>Clears every entry from <see cref="SelectedEntries"/>.</summary>
    IRelayCommand ClearEntriesCommand { get; }
    /// <summary>Starts the export, entering the loading state until it completes, fails, or is cancelled.</summary>
    IAsyncRelayCommand StartExportCommand { get; }
    /// <summary>Cancels an export in progress.</summary>
    IRelayCommand CancelExportCommand { get; }
}

/// <inheritdoc cref="IExportViewModel" />
public sealed partial class ExportViewModel : ObservableObject, IExportViewModel
{
    private readonly IExternalDriveProvider _driveProvider;
    private readonly IExportService _exportService;
    private CancellationTokenSource? _cancellation;

    [ObservableProperty] private IReadOnlyList<ExternalDriveInfo> _availableDrives = [];
    [ObservableProperty] private ExternalDriveInfo? _selectedDrive;
    [ObservableProperty] private string _fileName = "export";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectingEntries))]
    [NotifyPropertyChangedFor(nameof(IsAllScope))]
    [NotifyPropertyChangedFor(nameof(IsSomeScope))]
    private ExportScope _scope = ExportScope.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectingEntries))]
    [NotifyCanExecuteChangedFor(nameof(StartExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearEntriesCommand))]
    private bool _isExporting;

    [ObservableProperty] private string? _statusMessage;

    /// <inheritdoc />
    public ObservableCollection<EntryItemViewModel> SelectedEntries { get; } = [];

    /// <inheritdoc />
    public bool IsAllScope
    {
        get => Scope == ExportScope.All;
        set { if (value) Scope = ExportScope.All; }
    }

    /// <inheritdoc />
    public bool IsSomeScope
    {
        get => Scope == ExportScope.Some;
        set { if (value) Scope = ExportScope.Some; }
    }

    /// <inheritdoc />
    public bool IsCollectingEntries => Scope == ExportScope.Some && !IsExporting;

    /// <summary>Initializes a new <see cref="ExportViewModel"/> with the drive provider and export service.</summary>
    /// <param name="driveProvider">Enumerates available external drives.</param>
    /// <param name="exportService">Builds the full entry list and writes the zip archive.</param>
    public ExportViewModel(IExternalDriveProvider driveProvider, IExportService exportService)
    {
        _driveProvider = driveProvider;
        _exportService = exportService;
    }

    [RelayCommand]
    private void RefreshDrives()
    {
        IReadOnlyList<ExternalDriveInfo> drives = _driveProvider.GetDrives();
        AvailableDrives = drives;
        if (SelectedDrive is not null && !drives.Any(d => d.RootPath == SelectedDrive.RootPath))
            SelectedDrive = null;
    }

    /// <inheritdoc />
    public void AddEntry(EntryItemViewModel entry)
    {
        bool alreadyAdded = SelectedEntries.Any(e =>
            e.Id == entry.Id && e.EntryType == entry.EntryType && e.IsOutboundMessage == entry.IsOutboundMessage);
        if (!alreadyAdded)
            SelectedEntries.Add(entry);
    }

    [RelayCommand]
    private void RemoveEntry(EntryItemViewModel entry) => SelectedEntries.Remove(entry);

    [RelayCommand(CanExecute = nameof(CanClearEntries))]
    private void ClearEntries() => SelectedEntries.Clear();

    private bool CanClearEntries() => !IsExporting;

    [RelayCommand(CanExecute = nameof(CanStartExport))]
    private async Task StartExport()
    {
        ExternalDriveInfo? drive = SelectedDrive;
        if (drive is null) { StatusMessage = "Select a drive"; return; }
        if (string.IsNullOrWhiteSpace(FileName)) { StatusMessage = "Enter a file name"; return; }
        if (Scope == ExportScope.Some && SelectedEntries.Count == 0) { StatusMessage = "Select at least one entry to export"; return; }

        string zipPath = Path.Combine(drive.RootPath, SanitizeFileName(FileName) + IExportService.PackageExtension);

        _cancellation = new CancellationTokenSource();
        IsExporting = true;
        StatusMessage = null;
        try
        {
            IReadOnlyList<ExportEntryRef> refs = Scope == ExportScope.All
                ? await _exportService.GetAllEntryRefs()
                : SelectedEntries
                    .Select(e => new ExportEntryRef { Id = e.Id, EntryType = e.EntryType, IsOutboundMessage = e.IsOutboundMessage })
                    .ToList();

            await _exportService.Export(refs, zipPath, _cancellation.Token);

            StatusMessage = $"Exported {refs.Count} {(refs.Count == 1 ? "entry" : "entries")} to {drive.DisplayName}";
            SelectedEntries.Clear();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private bool CanStartExport() => !IsExporting;

    [RelayCommand(CanExecute = nameof(CanCancelExport))]
    private void CancelExport() => _cancellation?.Cancel();

    private bool CanCancelExport() => IsExporting;

    private static string SanitizeFileName(string name)
    {
        string trimmed = name.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            trimmed = trimmed.Replace(c, '_');
        return trimmed;
    }
}
