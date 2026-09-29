namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="ImportViewModel"/>.</summary>
public sealed class ImportViewModelTests
{
    private readonly ExternalDriveInfo driveA = new() { RootPath = "/media/a", DisplayName = "Drive A" };
    private readonly ExternalDriveInfo driveB = new() { RootPath = "/media/b", DisplayName = "Drive B" };
    private readonly ImportPackageInfo packageA = new() { FileName = "a.export.zip", FullPath = "/media/a/a.export.zip" };

    private sealed class Setup
    {
        public Mock<IExternalDriveProvider> DriveProvider { get; } = new();
        public Mock<IImportService> ImportService { get; } = new();
        public Mock<IStagedSendViewModel> StagedSend { get; } = new();
        public Mock<IEngineController> EngineController { get; } = new();

        public Setup() => EngineController.Setup(e => e.ImportFormats).Returns([]);

        public ImportViewModel Build() => new(DriveProvider.Object, ImportService.Object, StagedSend.Object, EngineController.Object);
    }

    /// <summary>A freshly constructed ViewModel has no drives, packages, or pending state.</summary>
    [Fact]
    public void Ctor_InitialState_IsEmptyAndIdle()
    {
        ImportViewModel vm = new Setup().Build();

        Assert.Empty(vm.AvailableDrives);
        Assert.Null(vm.SelectedDrive);
        Assert.Equal("Package", vm.SelectedFormat.Label);
        Assert.Empty(vm.AvailablePackages);
        Assert.False(vm.IsImporting);
        Assert.Null(vm.StatusMessage);
        Assert.Null(vm.PendingConflict);
    }

    /// <summary>AvailableFormats lists the built-in package option first, then each configured import format by name.</summary>
    [Fact]
    public void Ctor_WithConfiguredImportFormats_ListsPackageFirstThenEachByName()
    {
        ImportFormatDefinition csv = new() { Name = "CSV", Read = (_, _, _) => Task.CompletedTask };
        Setup s = new();
        s.EngineController.Setup(e => e.ImportFormats).Returns([csv]);

        ImportViewModel vm = s.Build();

        Assert.Equal(["Package", "CSV"], vm.AvailableFormats.Select(f => f.Label));
        Assert.Same(csv, vm.AvailableFormats[1].Format);
        Assert.Null(vm.AvailableFormats[0].Format);
    }

    /// <summary>Changing SelectedFormat re-queries AvailablePackages with the newly selected format.</summary>
    [Fact]
    public void SelectedFormat_Changed_RefreshesAvailablePackagesWithNewFormat()
    {
        ImportFormatDefinition csv = new() { Name = "CSV", Read = (_, _, _) => Task.CompletedTask };
        ImportPackageInfo csvFile = new() { FileName = "a.csv", FullPath = "/media/a/a.csv" };
        Setup s = new();
        s.EngineController.Setup(e => e.ImportFormats).Returns([csv]);
        s.ImportService.Setup(i => i.GetPackages(driveA.RootPath, csv)).Returns([csvFile]);
        ImportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;

        vm.SelectedFormat = vm.AvailableFormats[1];

        Assert.Equal([csvFile], vm.AvailablePackages);
    }

    /// <summary>RefreshDrivesCommand populates AvailableDrives from the provider.</summary>
    [Fact]
    public void RefreshDrivesCommand_PopulatesAvailableDrives()
    {
        Setup s = new();
        s.DriveProvider.Setup(d => d.GetDrives()).Returns([driveA, driveB]);
        ImportViewModel vm = s.Build();

        vm.RefreshDrivesCommand.Execute(null);

        Assert.Equal([driveA, driveB], vm.AvailableDrives);
    }

    /// <summary>RefreshDrivesCommand clears SelectedDrive when it is no longer present in the new list.</summary>
    [Fact]
    public void RefreshDrivesCommand_SelectedDriveGone_ClearsSelection()
    {
        Setup s = new();
        s.DriveProvider.SetupSequence(d => d.GetDrives())
            .Returns([driveA])
            .Returns([driveB]);
        ImportViewModel vm = s.Build();
        vm.RefreshDrivesCommand.Execute(null);
        vm.SelectedDrive = driveA;

        vm.RefreshDrivesCommand.Execute(null);

        Assert.Null(vm.SelectedDrive);
    }

    /// <summary>Selecting a drive populates AvailablePackages from the import service.</summary>
    [Fact]
    public void SelectedDrive_Set_PopulatesAvailablePackages()
    {
        Setup s = new();
        s.ImportService.Setup(i => i.GetPackages(driveA.RootPath)).Returns([packageA]);
        ImportViewModel vm = s.Build();

        vm.SelectedDrive = driveA;

        Assert.Equal([packageA], vm.AvailablePackages);
    }

    /// <summary>Clearing SelectedDrive clears AvailablePackages without calling the import service.</summary>
    [Fact]
    public void SelectedDrive_ClearedToNull_ClearsAvailablePackagesWithoutCallingService()
    {
        Setup s = new();
        s.ImportService.Setup(i => i.GetPackages(driveA.RootPath)).Returns([packageA]);
        ImportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;

        vm.SelectedDrive = null;

        Assert.Empty(vm.AvailablePackages);
        s.ImportService.Verify(i => i.GetPackages(It.IsAny<string>()), Times.Once);
    }

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
            .Setup(i => i.Import(packageA.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>()))
            .ReturnsAsync(new ImportSummary { Imported = 3, Skipped = 1, Overwritten = 2 });
        ImportViewModel vm = s.Build();

        await vm.StartImportCommand.ExecuteAsync(packageA);

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
            .Setup(i => i.Import(packageA.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>()))
            .Returns(async () =>
            {
                importStarted.SetResult();
                return await importGate.Task;
            });
        ImportViewModel vm = s.Build();

        Task importTask = vm.StartImportCommand.ExecuteAsync(packageA);
        await importStarted.Task;

        Assert.True(vm.IsImporting);
        Assert.False(vm.StartImportCommand.CanExecute(packageA));

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
            .Setup(i => i.Import(packageA.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>()))
            .ThrowsAsync(new IOException("disk error"));
        ImportViewModel vm = s.Build();

        await vm.StartImportCommand.ExecuteAsync(packageA);

        Assert.Equal("Import failed: disk error", vm.StatusMessage);
        Assert.False(vm.IsImporting);
    }

    /// <summary>When the import service raises a conflict, PendingConflict is set until ResolveConflictCommand is invoked.</summary>
    [Fact]
    public async Task Conflict_SetsPendingConflict_ClearedByResolveConflictCommand()
    {
        Setup s = new();
        ImportConflict conflict = new() { EntryType = EntryType.Draft, Name = "Plan" };
        s.ImportService
            .Setup(i => i.Import(packageA.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>(), null, default))
            .Returns(async (string _, Func<ImportConflict, Task<DraftNoteConflictResolution>> resolve, ImportFormatDefinition? _, CancellationToken _) =>
            {
                DraftNoteConflictResolution resolution = await resolve(conflict);
                return new ImportSummary { Imported = 0, Skipped = resolution == DraftNoteConflictResolution.KeepExisting ? 1 : 0, Overwritten = resolution == DraftNoteConflictResolution.KeepExisting ? 0 : 1 };
            });
        ImportViewModel vm = s.Build();

        Task importTask = vm.StartImportCommand.ExecuteAsync(packageA);
        await WaitUntil(() => vm.PendingConflict is not null);

        Assert.Equal(conflict, vm.PendingConflict);

        vm.ResolveConflictCommand.Execute(DraftNoteConflictResolution.Overwrite);
        await importTask;

        Assert.Null(vm.PendingConflict);
        Assert.Equal("Imported 0, overwrote 1, skipped 0", vm.StatusMessage);
    }

    /// <summary>A successful custom-format import that produced staged sends enqueues them with the format's mode/delay and raises StagedSendsReady.</summary>
    [Fact]
    public async Task StartImportCommand_CustomFormatWithStagedSends_EnqueuesAndRaisesStagedSendsReady()
    {
        ImportFormatDefinition csv = new() { Name = "CSV", Read = (_, _, _) => Task.CompletedTask, StagedSendMode = StagedSendMode.Simultaneous, StagedSendDelay = TimeSpan.FromSeconds(2) };
        List<StagedSendData> staged = [new StagedSendData { Subject = "S", Body = "B", Addresses = [] }];
        ImportPackageInfo csvFile = new() { FileName = "a.csv", FullPath = "/media/a/a.csv" };
        Setup s = new();
        s.EngineController.Setup(e => e.ImportFormats).Returns([csv]);
        s.ImportService
            .Setup(i => i.Import(csvFile.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>(), csv, default))
            .ReturnsAsync(new ImportSummary { Imported = 1, Skipped = 0, Overwritten = 0, StagedSends = staged });
        ImportViewModel vm = s.Build();
        vm.SelectedFormat = vm.AvailableFormats[1];
        bool raised = false;
        vm.StagedSendsReady += () => { raised = true; return Task.CompletedTask; };

        await vm.StartImportCommand.ExecuteAsync(csvFile);

        s.StagedSend.Verify(v => v.Enqueue(staged, StagedSendMode.Simultaneous, TimeSpan.FromSeconds(2)), Times.Once);
        Assert.True(raised);
    }

    /// <summary>A successful custom-format import that produced no staged sends does not touch the staged send ViewModel or raise the event.</summary>
    [Fact]
    public async Task StartImportCommand_CustomFormatNoStagedSends_DoesNotEnqueueOrRaiseEvent()
    {
        ImportFormatDefinition csv = new() { Name = "CSV", Read = (_, _, _) => Task.CompletedTask };
        ImportPackageInfo csvFile = new() { FileName = "a.csv", FullPath = "/media/a/a.csv" };
        Setup s = new();
        s.EngineController.Setup(e => e.ImportFormats).Returns([csv]);
        s.ImportService
            .Setup(i => i.Import(csvFile.FullPath, It.IsAny<Func<ImportConflict, Task<DraftNoteConflictResolution>>>(), csv, default))
            .ReturnsAsync(new ImportSummary { Imported = 1, Skipped = 0, Overwritten = 0 });
        ImportViewModel vm = s.Build();
        vm.SelectedFormat = vm.AvailableFormats[1];
        bool raised = false;
        vm.StagedSendsReady += () => { raised = true; return Task.CompletedTask; };

        await vm.StartImportCommand.ExecuteAsync(csvFile);

        s.StagedSend.Verify(v => v.Enqueue(It.IsAny<IReadOnlyList<StagedSendData>>(), It.IsAny<StagedSendMode>(), It.IsAny<TimeSpan?>()), Times.Never);
        Assert.False(raised);
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
