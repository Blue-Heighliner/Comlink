namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="ImportViewModel"/>.</summary>
public sealed class ImportViewModelTests
{
    private static readonly ExternalDriveInfo DriveA = new() { RootPath = "/media/a", DisplayName = "Drive A" };
    private static readonly ExternalDriveInfo DriveB = new() { RootPath = "/media/b", DisplayName = "Drive B" };
    private static readonly ImportPackageInfo PackageA = new() { FileName = "a.export.zip", FullPath = "/media/a/a.export.zip" };

    private sealed class Setup
    {
        public Mock<IExternalDriveProvider> DriveProvider { get; } = new();
        public Mock<IImportService> ImportService { get; } = new();

        public ImportViewModel Build() => new(DriveProvider.Object, ImportService.Object);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    /// <summary>A freshly constructed ViewModel has no drives, packages, or pending state.</summary>
    [Fact]
    public void Ctor_InitialState_IsEmptyAndIdle()
    {
        ImportViewModel vm = new Setup().Build();

        Assert.Empty(vm.AvailableDrives);
        Assert.Null(vm.SelectedDrive);
        Assert.Empty(vm.AvailablePackages);
        Assert.False(vm.IsImporting);
        Assert.Null(vm.StatusMessage);
        Assert.Null(vm.PendingConflict);
    }

    // ── RefreshDrivesCommand ─────────────────────────────────────────────────

    /// <summary>RefreshDrivesCommand populates AvailableDrives from the provider.</summary>
    [Fact]
    public void RefreshDrivesCommand_PopulatesAvailableDrives()
    {
        Setup s = new();
        s.DriveProvider.Setup(d => d.GetDrives()).Returns([DriveA, DriveB]);
        ImportViewModel vm = s.Build();

        vm.RefreshDrivesCommand.Execute(null);

        Assert.Equal([DriveA, DriveB], vm.AvailableDrives);
    }

    /// <summary>RefreshDrivesCommand clears SelectedDrive when it is no longer present in the new list.</summary>
    [Fact]
    public void RefreshDrivesCommand_SelectedDriveGone_ClearsSelection()
    {
        Setup s = new();
        s.DriveProvider.SetupSequence(d => d.GetDrives())
            .Returns([DriveA])
            .Returns([DriveB]);
        ImportViewModel vm = s.Build();
        vm.RefreshDrivesCommand.Execute(null);
        vm.SelectedDrive = DriveA;

        vm.RefreshDrivesCommand.Execute(null);

        Assert.Null(vm.SelectedDrive);
    }

    // ── SelectedDrive → AvailablePackages ────────────────────────────────────

    /// <summary>Selecting a drive populates AvailablePackages from the import service.</summary>
    [Fact]
    public void SelectedDrive_Set_PopulatesAvailablePackages()
    {
        Setup s = new();
        s.ImportService.Setup(i => i.GetPackages(DriveA.RootPath)).Returns([PackageA]);
        ImportViewModel vm = s.Build();

        vm.SelectedDrive = DriveA;

        Assert.Equal([PackageA], vm.AvailablePackages);
    }

    /// <summary>Clearing SelectedDrive clears AvailablePackages without calling the import service.</summary>
    [Fact]
    public void SelectedDrive_ClearedToNull_ClearsAvailablePackagesWithoutCallingService()
    {
        Setup s = new();
        s.ImportService.Setup(i => i.GetPackages(DriveA.RootPath)).Returns([PackageA]);
        ImportViewModel vm = s.Build();
        vm.SelectedDrive = DriveA;

        vm.SelectedDrive = null;

        Assert.Empty(vm.AvailablePackages);
        s.ImportService.Verify(i => i.GetPackages(It.IsAny<string>()), Times.Once);
    }

    // ── StartImportCommand ───────────────────────────────────────────────────

    /// <summary>StartImportCommand with a null package does nothing.</summary>
    [Fact]
    public async Task StartImportCommand_NullPackage_DoesNothing()
    {
        Setup s = new();
        ImportViewModel vm = s.Build();

        await vm.StartImportCommand.ExecuteAsync(null);

        Assert.Null(vm.StatusMessage);
        s.ImportService.Verify(i => i.Import(It.IsAny<string>(), It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>()), Times.Never);
    }

    /// <summary>A successful import reports the outcome counts and returns to the idle state.</summary>
    [Fact]
    public async Task StartImportCommand_Success_SetsStatusMessageAndClearsIsImporting()
    {
        Setup s = new();
        s.ImportService
            .Setup(i => i.Import(PackageA.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>()))
            .ReturnsAsync(new ImportSummary { Imported = 3, Skipped = 1, Overwritten = 2 });
        ImportViewModel vm = s.Build();

        await vm.StartImportCommand.ExecuteAsync(PackageA);

        Assert.Equal("Imported 3, overwrote 2, skipped 1", vm.StatusMessage);
        Assert.False(vm.IsImporting);
    }

    /// <summary>IsImporting is true while the import is running and CanStartImport reflects it.</summary>
    [Fact]
    public async Task StartImportCommand_WhileRunning_IsImportingIsTrueAndCommandDisabled()
    {
        Setup s = new();
        TaskCompletionSource importStarted = new();
        TaskCompletionSource<ImportSummary> importGate = new();
        s.ImportService
            .Setup(i => i.Import(PackageA.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>()))
            .Returns(async () =>
            {
                importStarted.SetResult();
                return await importGate.Task;
            });
        ImportViewModel vm = s.Build();

        Task importTask = vm.StartImportCommand.ExecuteAsync(PackageA);
        await importStarted.Task;

        Assert.True(vm.IsImporting);
        Assert.False(vm.StartImportCommand.CanExecute(PackageA));

        importGate.SetResult(new ImportSummary { Imported = 1, Skipped = 0, Overwritten = 0 });
        await importTask;

        Assert.False(vm.IsImporting);
    }

    /// <summary>A failed import sets a status message describing the failure.</summary>
    [Fact]
    public async Task StartImportCommand_ServiceThrows_SetsFailureStatusMessage()
    {
        Setup s = new();
        s.ImportService
            .Setup(i => i.Import(PackageA.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>()))
            .ThrowsAsync(new IOException("disk error"));
        ImportViewModel vm = s.Build();

        await vm.StartImportCommand.ExecuteAsync(PackageA);

        Assert.Equal("Import failed: disk error", vm.StatusMessage);
        Assert.False(vm.IsImporting);
    }

    // ── Conflict resolution ───────────────────────────────────────────────────

    /// <summary>When the import service raises a conflict, PendingConflict is set until ResolveConflictCommand is invoked.</summary>
    [Fact]
    public async Task Conflict_SetsPendingConflict_ClearedByResolveConflictCommand()
    {
        Setup s = new();
        ImportConflict conflict = new() { EntryType = EntryType.Draft, Name = "Plan" };
        s.ImportService
            .Setup(i => i.Import(PackageA.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>()))
            .Returns(async (string _, Func<ImportConflict, Task<DraftNoteConflictResolution>> resolve) =>
            {
                DraftNoteConflictResolution resolution = await resolve(conflict);
                return new ImportSummary { Imported = 0, Skipped = resolution == DraftNoteConflictResolution.KeepExisting ? 1 : 0, Overwritten = resolution == DraftNoteConflictResolution.KeepExisting ? 0 : 1 };
            });
        ImportViewModel vm = s.Build();

        Task importTask = vm.StartImportCommand.ExecuteAsync(PackageA);
        await WaitUntil(() => vm.PendingConflict is not null);

        Assert.Equal(conflict, vm.PendingConflict);

        vm.ResolveConflictCommand.Execute(DraftNoteConflictResolution.Overwrite);
        await importTask;

        Assert.Null(vm.PendingConflict);
        Assert.Equal("Imported 0, overwrote 1, skipped 0", vm.StatusMessage);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(5);
        }
    }
}
