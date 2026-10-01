namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>
/// ViewModel for the import screen: choosing a source drive, a format, then a file found on that drive by the
/// selected format to restore. Registered as a DI singleton (see <see cref="MainViewModel.Import"/>) so its
/// state — including an in-progress import, and any pending draft/note conflict prompt — survives navigating the
/// content area away to other views and back.
/// </summary>
internal interface IImportViewModel
{
    /// <summary>Raised after a successful import adds one or more staged sends to <see cref="IStagedSendViewModel"/>, so <see cref="MainViewModel"/> can switch to the staged send screen.</summary>
    event Func<Task>? StagedSendsReady;

    /// <summary>Gets the external drives currently available as an import source.</summary>
    IReadOnlyList<ExternalDriveInfo> AvailableDrives { get; }
    /// <summary>Gets or sets the drive selected as the import source. Setting this refreshes <see cref="AvailablePackages"/>.</summary>
    ExternalDriveInfo? SelectedDrive { get; set; }
    /// <summary>Gets the built-in package format plus every custom format added via <see cref="IEngineBuilder.ImportFormat{TFormat}"/>.</summary>
    IReadOnlyList<ImportFormatOption> AvailableFormats { get; }
    /// <summary>Gets or sets the format to import with; defaults to the built-in package format. Setting this refreshes <see cref="AvailablePackages"/>.</summary>
    ImportFormatOption SelectedFormat { get; set; }
    /// <summary>Gets the files found on <see cref="SelectedDrive"/> matching <see cref="SelectedFormat"/>.</summary>
    IReadOnlyList<ImportPackageInfo> AvailablePackages { get; }
    /// <summary>Gets a value indicating whether <see cref="AvailablePackages"/> is non-empty.</summary>
    bool HasPackages { get; }
    /// <summary>Gets a value indicating whether an import is currently running.</summary>
    bool IsImporting { get; }
    /// <summary>Gets the status message displayed after (or during) an import attempt.</summary>
    string? StatusMessage { get; }
    /// <summary>Gets the draft/note name conflict currently awaiting the user's resolution, or <see langword="null"/> if none.</summary>
    ImportConflict? PendingConflict { get; }
    /// <summary>Re-scans for available external drives, preserving <see cref="SelectedDrive"/> if it is still present.</summary>
    IRelayCommand RefreshDrivesCommand { get; }
    /// <summary>Imports the given package with <see cref="SelectedFormat"/>, entering the loading state until it completes or fails.</summary>
    IAsyncRelayCommand<ImportPackageInfo> StartImportCommand { get; }
    /// <summary>Resolves the current <see cref="PendingConflict"/> with the given choice.</summary>
    IRelayCommand<DraftNoteConflictResolution> ResolveConflictCommand { get; }
}

/// <inheritdoc cref="IImportViewModel" />
internal sealed partial class ImportViewModel : ObservableObject, IImportViewModel
{
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private ImportConflict? pendingConflict;

    /// <summary>Initializes a new <see cref="ImportViewModel"/> with the drive provider, import service, staged send ViewModel, and every configured import format.</summary>
    /// <param name="driveProvider">Enumerates available external drives.</param>
    /// <param name="importService">Lists files on a drive and restores their entries.</param>
    /// <param name="stagedSend">Receives the staged sends a custom format's reader adds.</param>
    /// <param name="engineController">Supplies the custom import formats added via <see cref="IEngineBuilder.ImportFormat{TFormat}"/>.</param>
    public ImportViewModel(IExternalDriveProvider driveProvider, IImportService importService, IStagedSendViewModel stagedSend, IEngineController engineController)
    {
        this.driveProvider = driveProvider;
        this.importService = importService;
        this.stagedSend = stagedSend;
        availableFormats = [new ImportFormatOption { Label = "Package" }, .. engineController.ImportFormats.Select(f => new ImportFormatOption { Label = f.Name, Format = f })];
        selectedFormat = availableFormats[0];
    }

    private readonly IExternalDriveProvider driveProvider;
    private readonly IImportService importService;
    private readonly IStagedSendViewModel stagedSend;
    private TaskCompletionSource<DraftNoteConflictResolution>? pendingResolution;

    /// <inheritdoc />
    public event Func<Task>? StagedSendsReady;

    [ObservableProperty] private IReadOnlyList<ExternalDriveInfo> availableDrives = [];
    [ObservableProperty] private ExternalDriveInfo? selectedDrive;
    [ObservableProperty] private IReadOnlyList<ImportFormatOption> availableFormats;
    [ObservableProperty] private ImportFormatOption selectedFormat;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPackages))]
    private IReadOnlyList<ImportPackageInfo> availablePackages = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartImportCommand))]
    private bool isImporting;

    /// <inheritdoc />
    public bool HasPackages => AvailablePackages.Count > 0;

    partial void OnSelectedDriveChanged(ExternalDriveInfo? value) => RefreshPackages();

    partial void OnSelectedFormatChanged(ImportFormatOption value) => RefreshPackages();

    [RelayCommand]
    private void RefreshDrives()
    {
        IReadOnlyList<ExternalDriveInfo> drives = driveProvider.GetDrives();
        AvailableDrives = drives;
        if (SelectedDrive is not null && !drives.Any(d => d.RootPath == SelectedDrive.RootPath))
        {
            SelectedDrive = null;
        }
    }

    private void RefreshPackages()
        => AvailablePackages = SelectedDrive is null ? [] : importService.GetPackages(SelectedDrive.RootPath, SelectedFormat.Format);

    [RelayCommand(CanExecute = nameof(CanStartImport))]
    private async Task StartImport(ImportPackageInfo? package)
    {
        if (package is null) { return; }

        ImportFormatDefinition? format = SelectedFormat.Format;
        IsImporting = true;
        StatusMessage = null;
        try
        {
            ImportSummary summary = await importService.Import(package.FullPath, AwaitConflictResolution, format);
            StatusMessage = $"Imported {summary.Imported}, overwrote {summary.Overwritten}, skipped {summary.Skipped}";

            if (format is not null && summary.StagedSends.Count > 0)
            {
                stagedSend.Enqueue(summary.StagedSends, format.StagedSendMode, format.StagedSendDelay);
                if (StagedSendsReady is not null) { await StagedSendsReady(); }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
            PendingConflict = null;
            pendingResolution = null;
        }
    }

    private bool CanStartImport(ImportPackageInfo? package) => !IsImporting;

    private Task<DraftNoteConflictResolution> AwaitConflictResolution(ImportConflict conflict)
    {
        TaskCompletionSource<DraftNoteConflictResolution> tcs = new();
        pendingResolution = tcs;
        PendingConflict = conflict;
        return tcs.Task;
    }

    [RelayCommand]
    private void ResolveConflict(DraftNoteConflictResolution resolution)
    {
        PendingConflict = null;
        TaskCompletionSource<DraftNoteConflictResolution>? pending = pendingResolution;
        pendingResolution = null;
        pending?.TrySetResult(resolution);
    }
}
