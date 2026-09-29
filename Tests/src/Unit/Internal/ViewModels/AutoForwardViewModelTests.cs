namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="AutoForwardViewModel"/>.</summary>
public sealed class AutoForwardViewModelTests
{
    private static AutoForwardControllerDefinition MakeController(string name, params string[] users)
        => new() { Name = name, Users = users, Filter = _ => true };

    private sealed class Setup
    {
        public Mock<IEngineController> EngineController { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUserProvider { get; } = new();
        public Mock<IAutoForwardTargetsRepository> TargetsRepository { get; } = new();
        public Mock<IServiceConnection> Connection { get; } = new();

        public Setup()
        {
            EngineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[]);
            Connection.Setup(c => c.GetUserNames(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        }

        public AutoForwardViewModel Build() => new(EngineController.Object, CurrentUserProvider.Object, TargetsRepository.Object, Connection.Object);
    }

    /// <summary>A freshly constructed ViewModel has no controllers, no targets, and no selection until Refresh runs.</summary>
    [Fact]
    public void Ctor_InitialState_IsEmpty()
    {
        AutoForwardViewModel vm = new Setup().Build();

        Assert.Empty(vm.AvailableControllers);
        Assert.False(vm.HasControllers);
        Assert.Null(vm.SelectedController);
        Assert.Empty(vm.Targets);
        Assert.False(vm.HasTargets);
        Assert.Empty(vm.AllUserNames);
    }

    /// <summary>RefreshCommand populates AvailableControllers with only the controllers naming the current user, and sets HasControllers accordingly.</summary>
    [Fact]
    public async Task RefreshCommand_PopulatesOnlyControllersTheCurrentUserHasAccessTo()
    {
        Setup s = new();
        s.CurrentUserProvider.Setup(c => c.UserName).Returns("ALICE");
        AutoForwardControllerDefinition accessible = MakeController("Alerts", "ALICE", "BOB");
        AutoForwardControllerDefinition inaccessible = MakeController("Backups", "BOB");
        s.EngineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[accessible, inaccessible]);
        AutoForwardViewModel vm = s.Build();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([accessible], vm.AvailableControllers);
        Assert.True(vm.HasControllers);
    }

    /// <summary>RefreshCommand with no accessible controllers leaves HasControllers false.</summary>
    [Fact]
    public async Task RefreshCommand_NoAccessibleControllers_LeavesHasControllersFalse()
    {
        Setup s = new();
        s.CurrentUserProvider.Setup(c => c.UserName).Returns("ALICE");
        s.EngineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[MakeController("Backups", "BOB")]);
        AutoForwardViewModel vm = s.Build();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasControllers);
    }

    /// <summary>RefreshCommand access-checking is case-insensitive on the user name.</summary>
    [Fact]
    public async Task RefreshCommand_UserNameMatchIsCaseInsensitive()
    {
        Setup s = new();
        s.CurrentUserProvider.Setup(c => c.UserName).Returns("alice");
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        s.EngineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[controller]);
        AutoForwardViewModel vm = s.Build();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([controller], vm.AvailableControllers);
    }

    /// <summary>RefreshCommand selects the first available controller and populates AllUserNames.</summary>
    [Fact]
    public async Task RefreshCommand_SelectsFirstControllerAndPopulatesAllUserNames()
    {
        Setup s = new();
        s.CurrentUserProvider.Setup(c => c.UserName).Returns("ALICE");
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        s.EngineController.Setup(e => e.AutoForwardControllers).Returns((IReadOnlyList<AutoForwardControllerDefinition>)[controller]);
        s.Connection.Setup(c => c.GetUserNames(It.IsAny<CancellationToken>())).ReturnsAsync(["ALICE", "BOB"]);
        AutoForwardViewModel vm = s.Build();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Same(controller, vm.SelectedController);
        Assert.Equal(["ALICE", "BOB"], vm.AllUserNames);
    }

    /// <summary>Selecting a controller loads its previously saved target list.</summary>
    [Fact]
    public async Task SelectedController_Set_LoadsSavedTargets()
    {
        Setup s = new();
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        s.TargetsRepository.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["BOB", "CAROL"] });
        AutoForwardViewModel vm = s.Build();

        vm.SelectedController = controller;
        await WaitUntil(() => vm.Targets.Count > 0);

        Assert.Equal(["BOB", "CAROL"], vm.Targets);
        Assert.True(vm.HasTargets);
    }

    /// <summary>Selecting a controller with no saved targets leaves Targets empty rather than throwing.</summary>
    [Fact]
    public async Task SelectedController_Set_NoSavedTargets_LeavesTargetsEmpty()
    {
        Setup s = new();
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        s.TargetsRepository.Setup(t => t.Get("Alerts")).ReturnsAsync((AutoForwardTargetsEntity?)null);
        AutoForwardViewModel vm = s.Build();

        vm.SelectedController = controller;
        await Task.Delay(20);

        Assert.Empty(vm.Targets);
    }

    /// <summary>Selecting null clears Targets without querying the repository.</summary>
    [Fact]
    public async Task SelectedController_SetToNull_ClearsTargets()
    {
        Setup s = new();
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        s.TargetsRepository.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["BOB"] });
        AutoForwardViewModel vm = s.Build();
        vm.SelectedController = controller;
        await WaitUntil(() => vm.Targets.Count > 0);

        vm.SelectedController = null;
        await Task.Delay(20);

        Assert.Empty(vm.Targets);
        Assert.False(vm.HasTargets);
    }

    /// <summary>AddTargetCommand adds the trimmed new target user to the list and saves it, then clears the input.</summary>
    [Fact]
    public async Task AddTargetCommand_AddsTrimmedUserAndSaves()
    {
        Setup s = new();
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        AutoForwardViewModel vm = s.Build();
        vm.SelectedController = controller;
        await Task.Delay(20);
        vm.NewTargetUser = "  BOB  ";

        await vm.AddTargetCommand.ExecuteAsync(null);

        Assert.Equal(["BOB"], vm.Targets);
        Assert.Equal(string.Empty, vm.NewTargetUser);
        s.TargetsRepository.Verify(t => t.Save("Alerts", It.Is<List<string>>(l => l.SequenceEqual(new[] { "BOB" }))), Times.Once);
    }

    /// <summary>AddTargetCommand does not add a duplicate (case-insensitive) of an already-present target.</summary>
    [Fact]
    public async Task AddTargetCommand_DuplicateUser_DoesNotAddOrSave()
    {
        Setup s = new();
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        s.TargetsRepository.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["BOB"] });
        AutoForwardViewModel vm = s.Build();
        vm.SelectedController = controller;
        await WaitUntil(() => vm.Targets.Count > 0);
        vm.NewTargetUser = "bob";

        await vm.AddTargetCommand.ExecuteAsync(null);

        Assert.Equal(["BOB"], vm.Targets);
        s.TargetsRepository.Verify(t => t.Save(It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }

    /// <summary>AddTargetCommand with a blank new target user does nothing.</summary>
    [Fact]
    public async Task AddTargetCommand_BlankUser_DoesNothing()
    {
        Setup s = new();
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        AutoForwardViewModel vm = s.Build();
        vm.SelectedController = controller;
        await Task.Delay(20);
        vm.NewTargetUser = "   ";

        await vm.AddTargetCommand.ExecuteAsync(null);

        Assert.Empty(vm.Targets);
        s.TargetsRepository.Verify(t => t.Save(It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }

    /// <summary>AddTargetCommand with no selected controller does nothing.</summary>
    [Fact]
    public async Task AddTargetCommand_NoSelectedController_DoesNothing()
    {
        Setup s = new();
        AutoForwardViewModel vm = s.Build();
        vm.NewTargetUser = "BOB";

        await vm.AddTargetCommand.ExecuteAsync(null);

        Assert.Empty(vm.Targets);
        s.TargetsRepository.Verify(t => t.Save(It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }

    /// <summary>RemoveTargetCommand removes the given user from the target list and saves it.</summary>
    [Fact]
    public async Task RemoveTargetCommand_RemovesUserAndSaves()
    {
        Setup s = new();
        AutoForwardControllerDefinition controller = MakeController("Alerts", "ALICE");
        s.TargetsRepository.Setup(t => t.Get("Alerts")).ReturnsAsync(new AutoForwardTargetsEntity { Id = "Alerts", Targets = ["BOB", "CAROL"] });
        AutoForwardViewModel vm = s.Build();
        vm.SelectedController = controller;
        await WaitUntil(() => vm.Targets.Count > 0);

        await vm.RemoveTargetCommand.ExecuteAsync("BOB");

        Assert.Equal(["CAROL"], vm.Targets);
        s.TargetsRepository.Verify(t => t.Save("Alerts", It.Is<List<string>>(l => l.SequenceEqual(new[] { "CAROL" }))), Times.Once);
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
