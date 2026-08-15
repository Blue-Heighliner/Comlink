namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="ExportViewModel"/>.</summary>
public sealed class ExportViewModelTests
{
    private readonly ExternalDriveInfo driveA = new() { RootPath = "/media/a", DisplayName = "Drive A" };
    private readonly ExternalDriveInfo driveB = new() { RootPath = "/media/b", DisplayName = "Drive B" };

    private sealed class Setup
    {
        public Mock<IExternalDriveProvider> DriveProvider { get; } = new();
        public Mock<IExportService> ExportService { get; } = new();

        public ExportViewModel Build() => new(DriveProvider.Object, ExportService.Object);
    }

    private static EntryItemViewModel MakeEntry(string id, EntryType type = EntryType.Message, bool outbound = false)
        => new(id, $"Title-{id}", type, DateTime.UtcNow, isOutboundMessage: outbound);

    /// <summary>A freshly constructed ViewModel defaults to All scope, an "export" file name, and is not exporting.</summary>
    [Fact]
    public void Ctor_InitialState_DefaultsToAllScopeNotExporting()
    {
        ExportViewModel vm = new Setup().Build();

        Assert.Equal(ExportScope.All, vm.Scope);
        Assert.True(vm.IsAllScope);
        Assert.False(vm.IsSomeScope);
        Assert.Equal("export", vm.FileName);
        Assert.False(vm.IsExporting);
        Assert.Empty(vm.SelectedEntries);
        Assert.False(vm.IsCollectingEntries);
    }

    /// <summary>RefreshDrivesCommand populates AvailableDrives from the provider.</summary>
    [Fact]
    public void RefreshDrivesCommand_PopulatesAvailableDrives()
    {
        Setup s = new();
        s.DriveProvider.Setup(d => d.GetDrives()).Returns([driveA, driveB]);
        ExportViewModel vm = s.Build();

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
        ExportViewModel vm = s.Build();
        vm.RefreshDrivesCommand.Execute(null);
        vm.SelectedDrive = driveA;

        vm.RefreshDrivesCommand.Execute(null);

        Assert.Null(vm.SelectedDrive);
    }

    /// <summary>RefreshDrivesCommand preserves SelectedDrive when it is still present in the new list.</summary>
    [Fact]
    public void RefreshDrivesCommand_SelectedDriveStillPresent_PreservesSelection()
    {
        Setup s = new();
        s.DriveProvider.Setup(d => d.GetDrives()).Returns([driveA, driveB]);
        ExportViewModel vm = s.Build();
        vm.RefreshDrivesCommand.Execute(null);
        vm.SelectedDrive = driveA;

        vm.RefreshDrivesCommand.Execute(null);

        Assert.Equal(driveA, vm.SelectedDrive);
    }

    /// <summary>Setting IsSomeScope switches Scope to Some.</summary>
    [Fact]
    public void IsSomeScope_SetTrue_SwitchesScopeToSome()
    {
        ExportViewModel vm = new Setup().Build();

        vm.IsSomeScope = true;

        Assert.Equal(ExportScope.Some, vm.Scope);
        Assert.True(vm.IsSomeScope);
        Assert.False(vm.IsAllScope);
    }

    /// <summary>Setting IsAllScope switches Scope back to All.</summary>
    [Fact]
    public void IsAllScope_SetTrue_SwitchesScopeToAll()
    {
        ExportViewModel vm = new Setup().Build();
        vm.Scope = ExportScope.Some;

        vm.IsAllScope = true;

        Assert.Equal(ExportScope.All, vm.Scope);
    }

    /// <summary>IsCollectingEntries is true only when Scope is Some and not currently exporting.</summary>
    [Fact]
    public void IsCollectingEntries_TrueOnlyWhenSomeScopeAndNotExporting()
    {
        ExportViewModel vm = new Setup().Build();
        Assert.False(vm.IsCollectingEntries);

        vm.Scope = ExportScope.Some;
        Assert.True(vm.IsCollectingEntries);
    }

    /// <summary>AddEntry appends a new entry to SelectedEntries.</summary>
    [Fact]
    public void AddEntry_NewEntry_AddsToSelectedEntries()
    {
        ExportViewModel vm = new Setup().Build();
        EntryItemViewModel entry = MakeEntry("E1");

        vm.AddEntry(entry);

        Assert.Single(vm.SelectedEntries);
        Assert.Same(entry, vm.SelectedEntries[0]);
    }

    /// <summary>AddEntry does not duplicate an entry already present with the same identity.</summary>
    [Fact]
    public void AddEntry_AlreadyPresent_DoesNotDuplicate()
    {
        ExportViewModel vm = new Setup().Build();
        vm.AddEntry(MakeEntry("E1"));

        vm.AddEntry(MakeEntry("E1"));

        Assert.Single(vm.SelectedEntries);
    }

    /// <summary>AddEntry treats Inbox and Outbox records sharing an Id as distinct entries.</summary>
    [Fact]
    public void AddEntry_SameIdDifferentDirection_AddsBoth()
    {
        ExportViewModel vm = new Setup().Build();
        vm.AddEntry(MakeEntry("M1", outbound: false));

        vm.AddEntry(MakeEntry("M1", outbound: true));

        Assert.Equal(2, vm.SelectedEntries.Count);
    }

    /// <summary>RemoveEntryCommand removes the given entry from SelectedEntries.</summary>
    [Fact]
    public void RemoveEntryCommand_RemovesEntry()
    {
        ExportViewModel vm = new Setup().Build();
        EntryItemViewModel entry = MakeEntry("E1");
        vm.AddEntry(entry);

        vm.RemoveEntryCommand.Execute(entry);

        Assert.Empty(vm.SelectedEntries);
    }

    /// <summary>ClearEntriesCommand removes every entry from SelectedEntries.</summary>
    [Fact]
    public void ClearEntriesCommand_RemovesAllEntries()
    {
        ExportViewModel vm = new Setup().Build();
        vm.AddEntry(MakeEntry("E1"));
        vm.AddEntry(MakeEntry("E2"));

        vm.ClearEntriesCommand.Execute(null);

        Assert.Empty(vm.SelectedEntries);
    }

    /// <summary>ClearEntriesCommand is a no-op when SelectedEntries is already empty.</summary>
    [Fact]
    public void ClearEntriesCommand_AlreadyEmpty_IsNoOp()
    {
        ExportViewModel vm = new Setup().Build();

        Exception? ex = Record.Exception(() => vm.ClearEntriesCommand.Execute(null));

        Assert.Null(ex);
        Assert.Empty(vm.SelectedEntries);
    }

    /// <summary>ClearEntriesCommand cannot execute while an export is running.</summary>
    [Fact]
    public async Task ClearEntriesCommand_WhileExporting_CannotExecute()
    {
        Setup s = new();
        TaskCompletionSource exportStarted = new();
        s.ExportService.Setup(e => e.GetAllEntryRefs()).ReturnsAsync([]);
        s.ExportService
            .Setup(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (IReadOnlyList<ExportEntryRef> _, string _, CancellationToken token) =>
            {
                exportStarted.SetResult();
                await Task.Delay(Timeout.Infinite, token);
            });
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;

        Task exportTask = vm.StartExportCommand.ExecuteAsync(null);
        await exportStarted.Task;

        Assert.False(vm.ClearEntriesCommand.CanExecute(null));

        vm.CancelExportCommand.Execute(null);
        await exportTask;
    }

    /// <summary>StartExportCommand with no drive selected sets a status message and does not call the export service.</summary>
    [Fact]
    public async Task StartExportCommand_NoDriveSelected_SetsStatusMessageAndDoesNotExport()
    {
        Setup s = new();
        ExportViewModel vm = s.Build();

        await vm.StartExportCommand.ExecuteAsync(null);

        Assert.Equal("Select a drive", vm.StatusMessage);
        s.ExportService.Verify(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>StartExportCommand with a blank file name sets a status message and does not export.</summary>
    [Fact]
    public async Task StartExportCommand_BlankFileName_SetsStatusMessageAndDoesNotExport()
    {
        Setup s = new();
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;
        vm.FileName = "   ";

        await vm.StartExportCommand.ExecuteAsync(null);

        Assert.Equal("Enter a file name", vm.StatusMessage);
        s.ExportService.Verify(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>StartExportCommand with Some scope and no collected entries sets a status message and does not export.</summary>
    [Fact]
    public async Task StartExportCommand_SomeScopeNoEntries_SetsStatusMessageAndDoesNotExport()
    {
        Setup s = new();
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;
        vm.Scope = ExportScope.Some;

        await vm.StartExportCommand.ExecuteAsync(null);

        Assert.Equal("Select at least one entry to export", vm.StatusMessage);
        s.ExportService.Verify(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>StartExportCommand with All scope fetches every entry ref and exports to the selected drive.</summary>
    [Fact]
    public async Task StartExportCommand_AllScope_ExportsEveryEntryRefToSelectedDrive()
    {
        Setup s = new();
        List<ExportEntryRef> allRefs = [new ExportEntryRef { Id = "M1", EntryType = EntryType.Message }];
        s.ExportService.Setup(e => e.GetAllEntryRefs()).ReturnsAsync(allRefs);
        s.ExportService.Setup(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;
        vm.FileName = "backup";

        await vm.StartExportCommand.ExecuteAsync(null);

        s.ExportService.Verify(e => e.Export(allRefs, Path.Combine(driveA.RootPath, "backup" + IExportService.PackageExtension), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(vm.IsExporting);
        Assert.Contains("Exported", vm.StatusMessage);
    }

    /// <summary>StartExportCommand with Some scope exports exactly the collected entries and clears the list on success.</summary>
    [Fact]
    public async Task StartExportCommand_SomeScope_ExportsCollectedEntriesAndClearsList()
    {
        Setup s = new();
        s.ExportService.Setup(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;
        vm.Scope = ExportScope.Some;
        vm.AddEntry(MakeEntry("E1"));
        vm.AddEntry(MakeEntry("E2", EntryType.Draft));

        await vm.StartExportCommand.ExecuteAsync(null);

        s.ExportService.Verify(e => e.Export(
            It.Is<IReadOnlyList<ExportEntryRef>>(refs => refs.Count == 2 && refs.Any(r => r.Id == "E1") && refs.Any(r => r.Id == "E2")),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(vm.SelectedEntries);
        s.ExportService.Verify(e => e.GetAllEntryRefs(), Times.Never);
    }

    /// <summary>A path separator in the file name is replaced so the resulting zip path stays a single file name segment.</summary>
    [Fact]
    public async Task StartExportCommand_FileNameWithPathSeparator_IsSanitized()
    {
        Setup s = new();
        s.ExportService.Setup(e => e.GetAllEntryRefs()).ReturnsAsync([]);
        s.ExportService.Setup(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;
        vm.FileName = "a/b";

        await vm.StartExportCommand.ExecuteAsync(null);

        s.ExportService.Verify(e => e.Export(
            It.IsAny<IReadOnlyList<ExportEntryRef>>(),
            Path.Combine(driveA.RootPath, "a_b" + IExportService.PackageExtension),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A cancelled export sets an appropriate status message, leaves IsExporting false, and preserves the collected entries.</summary>
    [Fact]
    public async Task StartExportCommand_Cancelled_SetsStatusMessageAndPreservesEntries()
    {
        Setup s = new();
        s.ExportService.Setup(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;
        vm.Scope = ExportScope.Some;
        vm.AddEntry(MakeEntry("E1"));

        await vm.StartExportCommand.ExecuteAsync(null);

        Assert.Equal("Export cancelled", vm.StatusMessage);
        Assert.False(vm.IsExporting);
        Assert.Single(vm.SelectedEntries);
    }

    /// <summary>A failed export sets a status message describing the failure and leaves IsExporting false.</summary>
    [Fact]
    public async Task StartExportCommand_ServiceThrows_SetsFailureStatusMessage()
    {
        Setup s = new();
        s.ExportService.Setup(e => e.GetAllEntryRefs()).ReturnsAsync([]);
        s.ExportService.Setup(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;

        await vm.StartExportCommand.ExecuteAsync(null);

        Assert.Equal("Export failed: disk full", vm.StatusMessage);
        Assert.False(vm.IsExporting);
    }

    /// <summary>CancelExportCommand cannot execute while no export is running.</summary>
    [Fact]
    public void CancelExportCommand_NotExporting_CannotExecute()
    {
        ExportViewModel vm = new Setup().Build();

        Assert.False(vm.CancelExportCommand.CanExecute(null));
    }

    /// <summary>Invoking CancelExportCommand while an export is running cancels the token passed to the export service.</summary>
    [Fact]
    public async Task CancelExportCommand_WhileExporting_CancelsExportToken()
    {
        Setup s = new();
        TaskCompletionSource exportStarted = new();
        CancellationToken? capturedToken = null;
        s.ExportService.Setup(e => e.GetAllEntryRefs()).ReturnsAsync([]);
        s.ExportService
            .Setup(e => e.Export(It.IsAny<IReadOnlyList<ExportEntryRef>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<ExportEntryRef> _, string _, CancellationToken token) =>
            {
                capturedToken = token;
                exportStarted.SetResult();
                return Task.Delay(Timeout.Infinite, token);
            });
        ExportViewModel vm = s.Build();
        vm.SelectedDrive = driveA;

        Task exportTask = vm.StartExportCommand.ExecuteAsync(null);
        await exportStarted.Task;

        Assert.True(vm.CancelExportCommand.CanExecute(null));
        vm.CancelExportCommand.Execute(null);
        await exportTask;

        Assert.True(capturedToken!.Value.IsCancellationRequested);
        Assert.Equal("Export cancelled", vm.StatusMessage);
    }
}
