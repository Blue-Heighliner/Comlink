namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>
/// ViewModel for the import screen: choosing a source drive, then an export package on that drive to restore.
/// Registered as a DI singleton (see <see cref="MainViewModel.Import"/>) so its state — including an
/// in-progress import, and any pending draft/note conflict prompt — survives navigating the content area
/// away to other views and back.
/// </summary>
public interface IImportViewModel
{
    /// <summary>Gets the external drives currently available as an import source.</summary>
    IReadOnlyList<ExternalDriveInfo> AvailableDrives { get; }
    /// <summary>Gets or sets the drive selected as the import source. Setting this refreshes <see cref="AvailablePackages"/>.</summary>
    ExternalDriveInfo? SelectedDrive { get; set; }
    /// <summary>Gets the export packages found on <see cref="SelectedDrive"/>.</summary>
    IReadOnlyList<ImportPackageInfo> AvailablePackages { get; }
    /// <summary>Gets a value indicating whether an import is currently running.</summary>
    bool IsImporting { get; }
    /// <summary>Gets the status message displayed after (or during) an import attempt.</summary>
    string? StatusMessage { get; }
    /// <summary>Gets the draft/note name conflict currently awaiting the user's resolution, or <see langword="null"/> if none.</summary>
    ImportConflict? PendingConflict { get; }
    /// <summary>Re-scans for available external drives, preserving <see cref="SelectedDrive"/> if it is still present.</summary>
    IRelayCommand RefreshDrivesCommand { get; }
    /// <summary>Imports the given package, entering the loading state until it completes or fails.</summary>
    IAsyncRelayCommand<ImportPackageInfo> StartImportCommand { get; }
    /// <summary>Resolves the current <see cref="PendingConflict"/> with the given choice.</summary>
    IRelayCommand<DraftNoteConflictResolution> ResolveConflictCommand { get; }
}

/// <inheritdoc cref="IImportViewModel" />
public sealed partial class ImportViewModel : ObservableObject, IImportViewModel
{
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private ImportConflict? pendingConflict;

    /// <summary>Initializes a new <see cref="ImportViewModel"/> with the drive provider and import service.</summary>
    /// <param name="driveProvider">Enumerates available external drives.</param>
    /// <param name="importService">Lists export packages on a drive and restores their entries.</param>
    public ImportViewModel(IExternalDriveProvider driveProvider, IImportService importService)
    {
        this.driveProvider = driveProvider;
        this.importService = importService;
    }

    private readonly IExternalDriveProvider driveProvider;
    private readonly IImportService importService;
    private TaskCompletionSource<DraftNoteConflictResolution>? pendingResolution;

    [ObservableProperty] private IReadOnlyList<ExternalDriveInfo> availableDrives = [];
    [ObservableProperty] private ExternalDriveInfo? selectedDrive;
    [ObservableProperty] private IReadOnlyList<ImportPackageInfo> availablePackages = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartImportCommand))]
    private bool isImporting;

    partial void OnSelectedDriveChanged(ExternalDriveInfo? value) => RefreshPackages();

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
        => AvailablePackages = SelectedDrive is null ? [] : importService.GetPackages(SelectedDrive.RootPath);

    [RelayCommand(CanExecute = nameof(CanStartImport))]
    private async Task StartImport(ImportPackageInfo? package)
    {
        if (package is null) { return; }

        IsImporting = true;
        StatusMessage = null;
        try
        {
            ImportSummary summary = await importService.Import(package.FullPath, AwaitConflictResolution);
            StatusMessage = $"Imported {summary.Imported}, overwrote {summary.Overwritten}, skipped {summary.Skipped}";
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
