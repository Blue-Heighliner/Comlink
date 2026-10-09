namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="DraftViewModel"/>.</summary>
public sealed class DraftViewModelTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static IEngineController MakeEngineController(
        string alertText = "ALERT",
        bool tagsEnabled = true, string tagLabel = "Tag", Func<Enum, Enum?, Enum?, string, bool>? isAllowed = null)
    {
        Mock<IEngineController> mock = new();
        mock.Setup(a => a.AlertLabel).Returns(alertText);
        mock.Setup(p => p.Priorities).Returns([
            new MessagePriorityOption { Name = "ROUTINE", Value = 0, Key = TestMessagePriority.Routine },
            new MessagePriorityOption { Name = "FLASH", Value = 3, Key = TestMessagePriority.Flash },
            new MessagePriorityOption { Name = "RECEIPT", Value = 9, Mode = PriorityMode.System, Key = TestMessagePriority.Receipt }
        ]);
        mock.Setup(t => t.TagsEnabled).Returns(tagsEnabled);
        mock.Setup(t => t.TagLabel).Returns(tagLabel);
        mock.Setup(t => t.DraftTagRules).Returns(TagRules.Unrestricted);
        mock.Setup(p => p.IsDraftAllowed(It.IsAny<IEngineContext>(), It.IsAny<Enum>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<string>())).Returns((IEngineContext _, Enum priority, Enum? level, Enum? aspect, string tag) => isAllowed?.Invoke(priority, level, aspect, tag) ?? true);
        mock.Setup(a => a.AddressTypes).Returns([
            new AddressTypeOption { Type = AddressType.To, Label = "To" },
            new AddressTypeOption { Type = AddressType.Cc, Label = "Cc" },
            new AddressTypeOption { Type = AddressType.External, Label = "External" }
        ]);
        mock.Setup(a => a.MessageLevels).Returns([]);
        mock.Setup(a => a.MessageAspects).Returns([]);
        return mock.Object;
    }

    private static Func<Enum, Enum?, Enum?, string, bool> Blocking(string? tag, TestMessagePriority? priority)
        => (candidate, _, _, candidateTag) => !((tag is null || tag == candidateTag) && (priority is null || candidate.Equals(priority)));

    private static DraftViewModel Build(
        out Mock<IEntryService> entryMock,
        out Mock<IEngineConnection> connMock,
        DraftEntity? entity = null,
        IReadOnlyList<string>? userNames = null,
        string alertText = "ALERT",
        bool tagsEnabled = true,
        string tagLabel = "Tag",
        Func<Enum, Enum?, Enum?, string, bool>? isAllowed = null)
    {
        entryMock = new Mock<IEntryService>();
        connMock = new Mock<IEngineConnection>();
        connMock.Setup(c => c.GetUserNames(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());
        DraftEntity ent = entity ?? new DraftEntity
        {
            Body = "Hello",
            Addresses = [],
            FolderId = "root-drafts"
        };
        return new DraftViewModel(ent, entryMock.Object, connMock.Object, userNames ?? [], noLogger,
            MakeEngineController(alertText, tagsEnabled, tagLabel, isAllowed));
    }

    private static DraftViewModel BuildDeletable(out Mock<IEntryService> entryMock, bool canDelete)
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.CanDelete(FolderType.Drafts)).Returns(canDelete);
        entryMock = new Mock<IEntryService>();
        Mock<IEngineConnection> conn = new();
        DraftEntity entity = new() { Body = "B", Addresses = [], FolderId = "root-drafts" };
        return new DraftViewModel(entity, entryMock.Object, conn.Object, [], noLogger, controller.Object, confirmationWindow: TimeSpan.FromMinutes(1));
    }

    /// <summary>DeleteCommand arms on the first press and deletes the draft on the second, then raises Deleted.</summary>
    [Fact]
    public async Task Delete_TwoPresses_ArmsThenDeletesDraft()
    {
        DraftViewModel vm = BuildDeletable(out Mock<IEntryService> entryMock, canDelete: true);
        bool deleted = false;
        vm.Deleted += () => { deleted = true; return Task.CompletedTask; };

        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.True(vm.IsConfirmingDelete);
        Assert.Equal("CONFIRM DELETE", vm.DeleteButtonText);
        Assert.False(deleted);
        entryMock.Verify(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>()), Times.Never);

        await vm.DeleteCommand.ExecuteAsync(null);
        entryMock.Verify(s => s.DeleteEntry(vm.Id, EntryType.Draft, false), Times.Once);
        Assert.True(deleted);
        Assert.False(vm.IsConfirmingDelete);
    }

    /// <summary>CanDelete follows the host's rule for drafts, and a draft that cannot be deleted never is.</summary>
    [Fact]
    public async Task Delete_WhenHostForbidsDraftDeletion_NeverDeletes()
    {
        DraftViewModel vm = BuildDeletable(out Mock<IEntryService> entryMock, canDelete: false);

        await vm.DeleteCommand.ExecuteAsync(null);
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.False(vm.CanDelete);
        entryMock.Verify(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>Constructor sets IsSent from entity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_SetsIsSentFromEntity(bool isSent)
    {
        DraftEntity entity = new() { IsSent = isSent };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        Assert.Equal(isSent, vm.IsSent);
    }

    /// <summary>PlsoMode defaults to Off — it is editor-session-only UI state, never read from the entity.</summary>
    [Fact]
    public void Constructor_PlsoModeDefaultsToOff()
    {
        DraftViewModel vm = Build(out _, out _);
        Assert.Equal(PlsoMode.Off, vm.PlsoMode);
    }

    /// <summary>AvailablePriorities is populated from IEngineController.Priorities.</summary>
    [Fact]
    public void Constructor_AvailablePrioritiesFromProvider()
    {
        DraftViewModel vm = Build(out _, out _);
        Assert.Equal(["ROUTINE", "FLASH"], vm.AvailablePriorities.Select(p => p.Name).ToList());
    }

    /// <summary>SelectedPriority defaults to the option matching the entity's stored Priority.</summary>
    [Fact]
    public void Constructor_SelectedPriorityMatchesEntityPriority()
    {
        DraftEntity entity = new() { Priority = (int)TestMessagePriority.Flash };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        Assert.Equal("FLASH", vm.SelectedPriority.Name);
        Assert.Equal(3, vm.SelectedPriority.Value);
    }

    /// <summary>SelectedPriority falls back to the first available option when the entity's Priority matches none.</summary>
    [Fact]
    public void Constructor_SelectedPriorityFallsBackToFirstWhenNoMatch()
    {
        DraftEntity entity = new() { Priority = 99 };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        Assert.Equal("ROUTINE", vm.SelectedPriority.Name);
    }

    /// <summary>Constructor sets Tag from entity.</summary>
    [Fact]
    public void Constructor_SetsTagFromEntity()
    {
        DraftEntity entity = new() { Tag = "URGENT" };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        Assert.Equal("URGENT", vm.Tag);
    }

    /// <summary>TagsEnabled is sourced from IEngineController.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_TagsEnabledFromTagConfiguration(bool enabled)
    {
        DraftViewModel vm = Build(out _, out _, tagsEnabled: enabled);
        Assert.Equal(enabled, vm.TagsEnabled);
    }

    /// <summary>TagLabel is sourced from IEngineController.TagLabel, so a host can rename the tag input.</summary>
    [Fact]
    public void Constructor_TagLabelFromTagConfiguration()
    {
        DraftViewModel vm = Build(out _, out _, tagLabel: "Category");
        Assert.Equal("Category", vm.TagLabel);
    }

    /// <summary>AvailablePriorities excludes a priority blocked for the entity's stored tag, even at construction.</summary>
    [Fact]
    public void Constructor_AvailablePrioritiesExcludesPriorityBlockedForStoredTag()
    {
        DraftEntity entity = new() { Tag = "URGENT", Priority = (int)TestMessagePriority.Flash };
        Func<Enum, Enum?, Enum?, string, bool> blocks = Blocking("URGENT", TestMessagePriority.Flash);
        DraftViewModel vm = Build(out _, out _, entity: entity, isAllowed: blocks);

        Assert.DoesNotContain(vm.AvailablePriorities, p => p.Name == "FLASH");
        Assert.Equal("ROUTINE", vm.SelectedPriority.Name);
    }

    /// <summary>Setting Tag to a value blocked for the currently selected priority is rejected, reverting to the previous tag.</summary>
    [Fact]
    public void Tag_SetToValueBlockedForCurrentPriority_RevertsToPreviousValue()
    {
        Func<Enum, Enum?, Enum?, string, bool> blocks = Blocking("SPAM", null);
        DraftViewModel vm = Build(out _, out _, isAllowed: blocks);

        vm.Tag = "SPAM";

        Assert.Equal(string.Empty, vm.Tag);
    }

    /// <summary>Setting Tag to a value not blocked for the current priority is accepted.</summary>
    [Fact]
    public void Tag_SetToUnblockedValue_IsAccepted()
    {
        Func<Enum, Enum?, Enum?, string, bool> blocks = Blocking("SPAM", null);
        DraftViewModel vm = Build(out _, out _, isAllowed: blocks);

        vm.Tag = "URGENT";

        Assert.Equal("URGENT", vm.Tag);
    }

    /// <summary>A system priority is never offered to the user composing a draft.</summary>
    [Fact]
    public void AvailablePriorities_ExcludeSystemPriorities()
    {
        DraftViewModel vm = Build(out _, out _);

        Assert.Equal(["ROUTINE", "FLASH"], vm.AvailablePriorities.Select(p => p.Name));
    }

    /// <summary>Setting Tag to a value that blocks another (not currently selected) priority hides that priority from AvailablePriorities.</summary>
    [Fact]
    public void Tag_SetToValueBlockingAnotherPriority_RemovesItFromAvailablePriorities()
    {
        Func<Enum, Enum?, Enum?, string, bool> blocks = Blocking("URGENT", TestMessagePriority.Flash);
        DraftViewModel vm = Build(out _, out _, isAllowed: blocks);
        Assert.Contains(vm.AvailablePriorities, p => p.Name == "FLASH");

        vm.Tag = "URGENT";

        Assert.Equal("URGENT", vm.Tag);
        Assert.DoesNotContain(vm.AvailablePriorities, p => p.Name == "FLASH");
        Assert.Equal("ROUTINE", vm.SelectedPriority.Name);
    }

    /// <summary>Reverting a rejected Tag change does not lose a previously accepted valid tag.</summary>
    [Fact]
    public void Tag_RejectedChangeAfterAcceptedChange_RevertsToLastAcceptedValue()
    {
        Func<Enum, Enum?, Enum?, string, bool> blocks = Blocking("SPAM", null);
        DraftViewModel vm = Build(out _, out _, isAllowed: blocks);
        vm.Tag = "URGENT";

        vm.Tag = "SPAM";

        Assert.Equal("URGENT", vm.Tag);
    }

    /// <summary>AlertLabel is sourced from IEngineController.AlertLabel — the same text used in the title bar's alert box.</summary>
    [Fact]
    public void Constructor_AlertLabelFromAlertConfiguration()
    {
        DraftViewModel vm = Build(out _, out _, alertText: "!ALERT!");
        Assert.Equal("!ALERT!", vm.AlertLabel);
    }

    /// <summary>PlsoMode is freely settable to any of its three states.</summary>
    [Theory]
    [InlineData((int)PlsoMode.Off)]
    [InlineData((int)PlsoMode.On)]
    [InlineData((int)PlsoMode.Spaces)]
    public void PlsoMode_CanBeSet(int mode)
    {
        DraftViewModel vm = Build(out _, out _);

        vm.PlsoMode = (PlsoMode)mode;

        Assert.Equal((PlsoMode)mode, vm.PlsoMode);
    }

    /// <summary>PlsoButtonText reflects the current PlsoMode.</summary>
    [Theory]
    [InlineData((int)PlsoMode.Off, "PLSO OFF")]
    [InlineData((int)PlsoMode.On, "PLSO ON")]
    [InlineData((int)PlsoMode.Spaces, "PLSO SPACES")]
    public void PlsoButtonText_ReflectsPlsoMode(int mode, string expected)
    {
        DraftViewModel vm = Build(out _, out _);

        vm.PlsoMode = (PlsoMode)mode;

        Assert.Equal(expected, vm.PlsoButtonText);
    }

    /// <summary>Constructor loads addresses from entity.</summary>
    [Fact]
    public void Constructor_LoadsAddressesFromEntity()
    {
        DraftEntity entity = new()
        {
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }]
        };
        DraftViewModel vm = Build(out _, out _, entity: entity);

        Assert.Single(vm.Addresses);
        Assert.Equal("ALPHA", vm.Addresses[0].UserName);
    }

    /// <summary>AllUserNames reflects the constructor argument.</summary>
    [Fact]
    public void Constructor_SetsAllUserNames()
    {
        DraftViewModel vm = Build(out _, out _, userNames: ["ALPHA", "BETA"]);
        Assert.Equal(["ALPHA", "BETA"], vm.AllUserNames);
    }

    /// <summary>AddressTypes reflects To, Cc and External, each paired with its display label.</summary>
    [Fact]
    public void AddressTypes_IsToCcAndExternal()
    {
        DraftViewModel vm = Build(out _, out _);
        Assert.Equal([AddressType.To, AddressType.Cc, AddressType.External], vm.AddressTypes.Select(t => t.Type));
        Assert.Equal(["To", "Cc", "External"], vm.AddressTypes.Select(t => t.Label));
    }

    /// <summary>An address added with custom instructions keeps them (trimmed), and the field is cleared for the next one.</summary>
    [Fact]
    public void AddAddressCommand_WithInformation_KeepsItAndClearsTheField()
    {
        DraftViewModel vm = Build(out _, out _);
        vm.NewAddressUser = "OMAHA";
        vm.NewAddressType = vm.AddressTypes.Single(t => t.Type is AddressType.External);
        vm.NewAddressInformation = "  Deliver to Eastside Office  ";

        vm.AddAddressCommand.Execute(null);

        AddressData address = Assert.Single(vm.Addresses);
        Assert.Equal("External", address.Type);
        Assert.Equal("Deliver to Eastside Office", address.Information);
        Assert.Equal(string.Empty, vm.NewAddressInformation);
    }

    /// <summary>An address added with no instructions has an empty Information.</summary>
    [Fact]
    public void AddAddressCommand_WithoutInformation_HasEmptyInformation()
    {
        DraftViewModel vm = Build(out _, out _);
        vm.NewAddressUser = "BRAVO";

        vm.AddAddressCommand.Execute(null);

        Assert.Equal(string.Empty, Assert.Single(vm.Addresses).Information);
    }

    /// <summary>Id is a non-empty string.</summary>
    [Fact]
    public void Id_IsNonEmpty()
    {
        DraftViewModel vm = Build(out _, out _);
        Assert.NotEmpty(vm.Id);
    }

    /// <summary>Setting NewAddressUser to lowercase auto-uppercases it.</summary>
    [Fact]
    public void NewAddressUser_Lowercase_AutoUppercased()
    {
        DraftViewModel vm = Build(out _, out _);
        vm.NewAddressUser = "alpha";
        Assert.Equal("ALPHA", vm.NewAddressUser);
    }

    /// <summary>Setting NewAddressUser to already-uppercase leaves it unchanged.</summary>
    [Fact]
    public void NewAddressUser_AlreadyUppercase_Unchanged()
    {
        DraftViewModel vm = Build(out _, out _);
        vm.NewAddressUser = "ALPHA";
        Assert.Equal("ALPHA", vm.NewAddressUser);
    }

    /// <summary>AddAddressCommand with a valid user adds to Addresses.</summary>
    [Fact]
    public void AddAddressCommand_ValidUser_AddsAddress()
    {
        DraftViewModel vm = Build(out _, out _);
        vm.NewAddressUser = "BRAVO";
        vm.NewAddressType = vm.AddressTypes.Single(t => t.Type is AddressType.Cc);

        vm.AddAddressCommand.Execute(null);

        Assert.Single(vm.Addresses);
        Assert.Equal("BRAVO", vm.Addresses[0].UserName);
        Assert.Equal("Cc", vm.Addresses[0].Type);
        Assert.Equal(string.Empty, vm.NewAddressUser);
    }

    /// <summary>AddAddressCommand with a blank user does nothing.</summary>
    [Fact]
    public void AddAddressCommand_BlankUser_DoesNothing()
    {
        DraftViewModel vm = Build(out _, out _);
        vm.NewAddressUser = "   ";

        vm.AddAddressCommand.Execute(null);

        Assert.Empty(vm.Addresses);
    }

    /// <summary>RemoveAddressCommand removes the specified address.</summary>
    [Fact]
    public void RemoveAddressCommand_RemovesAddress()
    {
        DraftEntity entity = new()
        {
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }]
        };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        AddressData addr = vm.Addresses[0];

        vm.RemoveAddressCommand.Execute(addr);

        Assert.Empty(vm.Addresses);
    }

    /// <summary>InsertFillIn adds a new entry to the FillIns dictionary.</summary>
    [Fact]
    public void InsertFillIn_AddsFillInToDict()
    {
        DraftViewModel vm = Build(out _, out _);
        Assert.Empty(vm.FillIns);

        vm.InsertFillIn(0);

        Assert.Single(vm.FillIns);
    }

    /// <summary>Multiple InsertFillIn calls add multiple distinct entries.</summary>
    [Fact]
    public void InsertFillIn_MultipleCalls_AddsMultipleDistinct()
    {
        DraftViewModel vm = Build(out _, out _);

        vm.InsertFillIn(0);
        vm.InsertFillIn(vm.BodyDocument.TextLength);

        Assert.Equal(2, vm.FillIns.Count);
    }

    private static DraftViewModel BuildWithLineWidth(out Mock<IEntryService> entryMock, int? width, string body)
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.DraftLineWidth).Returns(width is null ? null : new LineWidthRange(width, 1, null));
        entryMock = new Mock<IEntryService>();
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        DraftViewModel vm = new(new DraftEntity { Body = body, Addresses = [], FolderId = "root-drafts" }, entryMock.Object, new Mock<IEngineConnection>().Object, [], noLogger, controller.Object);
        vm.BodyDocument.Text = body;
        return vm;
    }

    private static DraftViewModel BuildWithHandler(out Mock<IEntryService> entryMock, out Mock<IEngineConnection> connMock, DraftEntity? entity = null)
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.DraftLineWidth).Returns(new LineWidthRange(30, 20, 40));
        controller.Setup(c => c.IsAlert(It.IsAny<DraftContent>())).Returns<DraftContent>(draft => draft.Tag == "ALERT");
        controller.Setup(c => c.GetDraftHeader(It.IsAny<DraftContent>())).Returns<DraftContent>(draft => draft.Tag.Length > 0 ? $"HEADER {draft.Tag} {draft.Tag == "ALERT"} {draft.Addresses.Count} {draft.LineWidth}" : null);
        entryMock = new Mock<IEntryService>();
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        connMock = new Mock<IEngineConnection>();
        return new DraftViewModel(entity ?? new DraftEntity { Body = "body", Addresses = [], FolderId = "root-drafts" }, entryMock.Object, connMock.Object, [], noLogger, controller.Object);
    }

    /// <summary>The header is asked for again whenever the tag, alert flag, recipients or line width change, and is null while the handler says so.</summary>
    [Fact]
    public void Header_IsRecheckedWhenTheDraftChanges()
    {
        DraftViewModel vm = BuildWithHandler(out _, out _);
        Assert.Null(vm.Header);

        vm.Tag = "URGENT";
        Assert.Equal("HEADER URGENT False 0 30", vm.Header);

        vm.Addresses.Add(new AddressData { UserName = "BOB", Type = "To" });
        Assert.Equal("HEADER URGENT False 1 30", vm.Header);

        vm.Tag = "ALERT";
        Assert.Equal("HEADER ALERT True 1 30", vm.Header);
        Assert.True(vm.IsAlert);

        vm.LineWidthValue = 40;
        Assert.Equal("HEADER ALERT True 1 40", vm.Header);

        vm.Tag = "";
        Assert.Null(vm.Header);
        Assert.False(vm.IsAlert);
    }

    /// <summary>The line width starts at the handler's default or the draft's own, is kept within the range, and is saved with the draft.</summary>
    [Fact]
    public async Task LineWidth_StartsAtTheDefault_IsClampedAndSaved()
    {
        DraftViewModel fresh = BuildWithHandler(out Mock<IEntryService> entryMock, out _);
        DraftViewModel stored = BuildWithHandler(out _, out _, new DraftEntity { Body = "b", Addresses = [], FolderId = "root-drafts", LineWidth = 35 });
        Assert.Equal((30, 35, true), (fresh.LineWidth, stored.LineWidth, fresh.IsLineWidthAvailable));

        fresh.LineWidthValue = 5;
        Assert.Equal(20, fresh.LineWidth);
        fresh.LineWidthValue = 500;
        Assert.Equal(40, fresh.LineWidth);
        fresh.LineWidthValue = null;
        Assert.Equal(40, fresh.LineWidth);

        await fresh.SaveCommand.ExecuteAsync(null);
        entryMock.Verify(e => e.SaveDraft(It.Is<DraftEntity>(d => d.LineWidth == 40)), Times.Once);
    }

    private static DraftViewModel BuildWithTagRules(out Mock<IEngineConnection> connMock, TagRules rules)
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.DraftTagRules).Returns(rules);
        connMock = new Mock<IEngineConnection>();
        return new DraftViewModel(new DraftEntity { Body = "b", Addresses = [], FolderId = "root-drafts", Tag = "kept" }, new Mock<IEntryService>().Object, connMock.Object, [], noLogger, controller.Object);
    }

    /// <summary>A tag is made into one the rules allow as it is entered, whatever is typed or pasted, and what was stored is filtered when the draft opens.</summary>
    [Fact]
    public void Tag_IsFilteredByTheRules()
    {
        DraftViewModel vm = BuildWithTagRules(out _, new TagRules(TagCase.Upper, 0, 6, false, true, false));
        Assert.Equal("KEPT", vm.Tag);
        Assert.Equal(6, vm.TagMaxLength);

        vm.Tag = "ab #1 cdefg";
        Assert.Equal("AB1CDE", vm.Tag);
    }

    /// <summary>LiteDB reads an empty tag back as null, so a stored draft with no tag opens, with an empty one.</summary>
    [Fact]
    public void Constructor_NullStoredTag_OpensWithAnEmptyTag()
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        DraftEntity stored = new() { Body = "b", Addresses = [], FolderId = "root-drafts", Tag = null! };

        DraftViewModel vm = new(stored, new Mock<IEntryService>().Object, new Mock<IEngineConnection>().Object, [], noLogger, controller.Object);

        Assert.Equal(string.Empty, vm.Tag);
    }

    /// <summary>A draft without a tag is not sent when a tag is required, and says so.</summary>
    [Fact]
    public async Task Send_MissingRequiredTag_IsNotSent()
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.DraftTagRules).Returns(TagRules.Unrestricted with { IsRequired = true });
        Mock<IEngineConnection> connMock = new();
        DraftViewModel vm = new(new DraftEntity { Body = "b", Addresses = [], FolderId = "root-drafts" }, new Mock<IEntryService>().Object, connMock.Object, [], noLogger, controller.Object);
        vm.Addresses.Add(new AddressData { UserName = "BOB", Type = "To" });

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Contains("required", vm.StatusMessage);
        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Recipients are grouped by address type in the order of the types, and moving one swaps it with the next of its own type, skipping the others.</summary>
    [Fact]
    public void AddressGroups_GroupByTypeAndMoveWithinIt()
    {
        DraftViewModel vm = Build(out _, out _);
        vm.Addresses.Add(new AddressData { UserName = "A", Type = "Cc" });
        vm.Addresses.Add(new AddressData { UserName = "B", Type = "To" });
        vm.Addresses.Add(new AddressData { UserName = "C", Type = "Cc" });
        vm.Addresses.Add(new AddressData { UserName = "D", Type = "To" });

        Assert.Equal([("To", "B,D"), ("Cc", "A,C")], vm.AddressGroups.Select(group => (group.Label, string.Join(',', group.Items.Select(address => address.UserName)))));

        vm.MoveAddressUpCommand.Execute(vm.Addresses.Single(address => address.UserName == "D"));
        Assert.Equal("D,B", string.Join(',', vm.AddressGroups[0].Items.Select(address => address.UserName)));

        vm.MoveAddressDownCommand.Execute(vm.Addresses.Single(address => address.UserName == "A"));
        Assert.Equal("C,A", string.Join(',', vm.AddressGroups[1].Items.Select(address => address.UserName)));

        vm.MoveAddressDownCommand.Execute(vm.Addresses.Single(address => address.UserName == "A"));
        Assert.Equal("C,A", string.Join(',', vm.AddressGroups[1].Items.Select(address => address.UserName)));
    }

    /// <summary>Leaving a draft saves what was written, but only when something changed, and never once it was sent.</summary>
    [Fact]
    public async Task SaveChanges_SavesOnlyWhatChanged()
    {
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out _);
        entryMock.Setup(e => e.SaveDraftQuietly(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);

        await vm.SaveChanges();
        entryMock.Verify(e => e.SaveDraftQuietly(It.IsAny<DraftEntity>()), Times.Never);

        vm.BodyDocument.Text = "new text";
        await vm.SaveChanges();
        entryMock.Verify(e => e.SaveDraftQuietly(It.Is<DraftEntity>(draft => draft.Body == "new text")), Times.Once);

        await vm.SaveChanges();
        entryMock.Verify(e => e.SaveDraftQuietly(It.IsAny<DraftEntity>()), Times.Once);

        vm.Addresses.Add(new AddressData { UserName = "BOB", Type = "To" });
        await vm.SaveChanges();
        entryMock.Verify(e => e.SaveDraftQuietly(It.IsAny<DraftEntity>()), Times.Exactly(2));
    }

    /// <summary>A draft whose tag is shorter than the minimum is not sent, and says why.</summary>
    [Fact]
    public async Task Send_TagShorterThanTheMinimum_IsNotSent()
    {
        DraftViewModel vm = BuildWithTagRules(out Mock<IEngineConnection> connMock, new TagRules(TagCase.Mixed, 6, null, true, true, true));
        vm.Addresses.Add(new AddressData { UserName = "BOB", Type = "To" });

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Contains("at least 6", vm.StatusMessage);
        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The line width can never be less than the longest line of the header, even when the handler's minimum is smaller, and a header wider than the handler's maximum wins.</summary>
    [Fact]
    public void LineWidth_IsNeverLessThanTheHeader()
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.DraftLineWidth).Returns(new LineWidthRange(25, 10, 40));
        controller.Setup(c => c.GetDraftHeader(It.IsAny<DraftContent>())).Returns<DraftContent>(draft => draft.Tag.Length > 0 ? "first\n" + new string('h', draft.Tag.Length) : null);
        DraftViewModel vm = new(new DraftEntity { Body = "b", Addresses = [], FolderId = "root-drafts" }, new Mock<IEntryService>().Object, new Mock<IEngineConnection>().Object, [], noLogger, controller.Object);
        Assert.Equal((10m, 25), (vm.LineWidthMinimum, vm.LineWidth));

        vm.Tag = new string('t', 30);
        Assert.Equal((30m, 30), (vm.LineWidthMinimum, vm.LineWidth));

        vm.LineWidthValue = 12;
        Assert.Equal(30, vm.LineWidth);

        vm.Tag = new string('t', 50);
        Assert.Equal((50m, 50, 50m), (vm.LineWidthMinimum, vm.LineWidth, vm.LineWidthMaximum));

        vm.Tag = "";
        Assert.Equal((10m, 50), (vm.LineWidthMinimum, vm.LineWidth));
        vm.LineWidthValue = 12;
        Assert.Equal(12, vm.LineWidth);
    }

    /// <summary>The header is put in front of the message when it is sent, while the saved draft keeps only what the user wrote.</summary>
    [Fact]
    public async Task Send_PutsTheHeaderInFrontOfTheBody()
    {
        DraftViewModel vm = BuildWithHandler(out Mock<IEntryService> entryMock, out Mock<IEngineConnection> connMock);
        connMock.Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResult { MessageId = "M" });
        entryMock.Setup(e => e.FindMessage(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new MessageEntity());
        vm.Tag = "URGENT";
        vm.Addresses.Add(new AddressData { UserName = "BOB", Type = "To" });
        vm.BodyDocument.Text = "the body";

        await vm.SendCommand.ExecuteAsync(null);

        connMock.Verify(c => c.SendMessage("HEADER URGENT False 1 30\nthe body", It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), "URGENT", It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()), Times.Once);
        entryMock.Verify(e => e.SaveDraft(It.Is<DraftEntity>(d => d.Body == "the body")), Times.AtLeastOnce);
    }

    /// <summary>The line width only changes how the draft is shown: the saved body and the text are never given line breaks, whatever the width.</summary>
    [Fact]
    public async Task LineWidth_NeverChangesTheText()
    {
        DraftViewModel vm = BuildWithLineWidth(out Mock<IEntryService> entryMock, 10, "aaaa bbbb cccc dddd");

        await vm.SaveCommand.ExecuteAsync(null);
        vm.LineWidthValue = 5;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("aaaa bbbb cccc dddd", vm.BodyDocument.Text);
        entryMock.Verify(e => e.SaveDraft(It.Is<DraftEntity>(d => d.Body == "aaaa bbbb cccc dddd")), Times.Exactly(2));
    }

    /// <summary>SaveCommand calls entryService.SaveDraft and sets StatusMessage to "Saved".</summary>
    [Fact]
    public async Task SaveCommand_CallsSaveDraftAndSetsStatusMessage()
    {
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out _);
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);

        await vm.SaveCommand.ExecuteAsync(null);

        entryMock.Verify(e => e.SaveDraft(It.IsAny<DraftEntity>()), Times.Once);
        Assert.Equal("Saved", vm.StatusMessage);
        Assert.False(vm.IsSaving);
    }

    /// <summary>SaveCommand persists the currently selected priority's Value onto the draft entity.</summary>
    [Fact]
    public async Task SaveCommand_PersistsSelectedPriorityOnEntity()
    {
        DraftEntity entity = new() { FolderId = "root-drafts" };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out _, entity: entity);
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        vm.SelectedPriority = vm.AvailablePriorities.Single(p => p.Name == "FLASH");

        await vm.SaveCommand.ExecuteAsync(null);

        entryMock.Verify(e => e.SaveDraft(It.Is<DraftEntity>(d => d.Priority == (int)TestMessagePriority.Flash)), Times.Once);
    }

    /// <summary>SaveCommand persists the current Tag value onto the draft entity.</summary>
    [Fact]
    public async Task SaveCommand_PersistsTagOnEntity()
    {
        DraftEntity entity = new() { FolderId = "root-drafts" };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out _, entity: entity);
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        vm.Tag = "URGENT";

        await vm.SaveCommand.ExecuteAsync(null);

        entryMock.Verify(e => e.SaveDraft(It.Is<DraftEntity>(d => d.Tag == "URGENT")), Times.Once);
    }

    /// <summary>SendCommand with no addresses sets StatusMessage and does not send.</summary>
    [Fact]
    public async Task SendCommand_NoAddresses_SetsStatusMessageAndDoesNotSend()
    {
        DraftViewModel vm = Build(out _, out Mock<IEngineConnection> connMock);

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Add at least one recipient", vm.StatusMessage);
        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>SendCommand reports that no user is installed when the send is refused for that reason, rather than failing on a missing result.</summary>
    [Fact]
    public async Task SendCommand_NoUserInstalled_SaysSoAndKeepsDraftUnsent()
    {
        DraftEntity entity = new() { Body = "World", Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }], FolderId = "root-drafts" };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out Mock<IEngineConnection> connMock, entity: entity);
        connMock.Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendMessageResult?)null);

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Cannot send until a user is installed", vm.StatusMessage);
        Assert.False(vm.IsSent);
    }

    /// <summary>SendCommand with addresses and successful send sets IsSent and StatusMessage.</summary>
    [Fact]
    public async Task SendCommand_WithAddresses_SendsAndSetsIsSent()
    {
        DraftEntity entity = new()
        {
            Body = "World",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }],
            FolderId = "root-drafts"
        };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out Mock<IEngineConnection> connMock, entity: entity);

        SendMessageResult sendResult = new()
        {
            MessageId = "MSG-001"
        };
        connMock.Setup(c => c.SendMessage(It.IsAny<string>(),
                It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendResult);

        MessageEntity sentMessage = new() { MessageId = "MSG-001" };
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        entryMock.Setup(e => e.FindMessage(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new MessageEntity());

        await vm.SendCommand.ExecuteAsync(null);

        Assert.True(vm.IsSent);
        Assert.Equal("Sent", vm.StatusMessage);
        Assert.False(vm.IsSaving);
    }

    /// <summary>SendCommand passes the selected priority's Value to both SendMessage and StoreSentMessage.</summary>
    [Fact]
    public async Task SendCommand_PassesSelectedPriorityToSendAndStore()
    {
        DraftEntity entity = new()
        {
            Body = "World",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }],
            FolderId = "root-drafts"
        };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out Mock<IEngineConnection> connMock, entity: entity);
        vm.SelectedPriority = vm.AvailablePriorities.Single(p => p.Name == "FLASH");

        SendMessageResult sendResult = new()
        {
            MessageId = "MSG-001"
        };
        connMock.Setup(c => c.SendMessage(It.IsAny<string>(),
                It.IsAny<List<AddressRequest>>(), TestMessagePriority.Flash, It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendResult);

        MessageEntity sentMessage = new() { MessageId = "MSG-001" };
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        entryMock.Setup(e => e.FindMessage(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new MessageEntity());

        await vm.SendCommand.ExecuteAsync(null);

        connMock.Verify(c => c.SendMessage(It.IsAny<string>(),
            It.IsAny<List<AddressRequest>>(), TestMessagePriority.Flash, It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(vm.IsSent);
    }

    /// <summary>SendCommand passes the current Tag to both SendMessage and StoreSentMessage.</summary>
    [Fact]
    public async Task SendCommand_PassesTagToSendAndStore()
    {
        DraftEntity entity = new()
        {
            Body = "World",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }],
            FolderId = "root-drafts"
        };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out Mock<IEngineConnection> connMock, entity: entity);
        vm.Tag = "URGENT";

        SendMessageResult sendResult = new()
        {
            MessageId = "MSG-001"
        };
        connMock.Setup(c => c.SendMessage(It.IsAny<string>(),
                It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), "URGENT", It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendResult);

        MessageEntity sentMessage = new() { MessageId = "MSG-001" };
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        entryMock.Setup(e => e.FindMessage(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new MessageEntity());

        await vm.SendCommand.ExecuteAsync(null);

        connMock.Verify(c => c.SendMessage(It.IsAny<string>(),
            It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), "URGENT", It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(vm.IsSent);
    }

    /// <summary>
    /// SendCommand refuses to send when the draft is a blocked combination, as a defense-in-depth
    /// safety net behind the live UI-level prevention (for instance a handler whose answer changes after the draft was opened).
    /// </summary>
    [Fact]
    public async Task SendCommand_BlockedTagPriorityCombination_SetsStatusMessageAndDoesNotSend()
    {
        DraftEntity entity = new()
        {
            Body = "World",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }],
            FolderId = "root-drafts"
        };
        bool blocked = false;
        Func<Enum, Enum?, Enum?, string, bool> blocks = (_, _, _, _) => !blocked;
        DraftViewModel vm = Build(out _, out Mock<IEngineConnection> connMock, entity: entity, isAllowed: blocks);
        blocked = true;

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("This combination is not allowed", vm.StatusMessage);
        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A new draft is not stored while unaltered or blank, and is inserted once it has content.</summary>
    [Fact]
    public async Task NewDraft_IsStoredOnlyOnceAlteredAndNotBlank()
    {
        Mock<IEntryService> entryMock = new();
        Mock<IEngineConnection> connMock = new();
        DraftEntity entity = new() { Body = string.Empty, Addresses = [], FolderId = "root-drafts" };
        DraftViewModel vm = new(entity, entryMock.Object, connMock.Object, [], noLogger, MakeEngineController(), isNew: true);

        await vm.SaveChanges();
        vm.BodyDocument.Text = "  ";
        await vm.SaveChanges();
        await vm.SaveCommand.ExecuteAsync(null);
        entryMock.Verify(e => e.InsertDraft(It.IsAny<DraftEntity>()), Times.Never);
        Assert.Equal("Nothing to save", vm.StatusMessage);

        TaskCompletionSource inserted = new();
        entryMock.Setup(e => e.InsertDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask).Callback(() => inserted.TrySetResult());
        vm.Name = "Named";
        await inserted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await vm.SaveChanges();
        entryMock.Verify(e => e.InsertDraft(It.Is<DraftEntity>(draft => draft.Name == "Named")), Times.Once);
    }

    /// <summary>Duplicating copies the draft as it is on screen, does not save the original, and reports the copy.</summary>
    [Fact]
    public async Task Duplicate_CopiesCurrentStateWithoutSavingTheOriginal()
    {
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out _);
        DraftEntity copy = new() { Id = new ObjectId(), Body = "edited", Addresses = [], FolderId = "root-drafts" };
        entryMock.Setup(e => e.DuplicateDraft(It.IsAny<DraftEntity>())).ReturnsAsync(copy);
        string? shown = null;
        vm.Duplicated += id =>
        {
            shown = id;
            return Task.CompletedTask;
        };

        vm.BodyDocument.Text = "edited";
        await vm.DuplicateCommand.ExecuteAsync(null);

        entryMock.Verify(e => e.DuplicateDraft(It.Is<DraftEntity>(draft => draft.Body == "edited")), Times.Once);
        entryMock.Verify(e => e.SaveDraft(It.IsAny<DraftEntity>()), Times.Never);
        entryMock.Verify(e => e.SaveDraftQuietly(It.IsAny<DraftEntity>()), Times.Never);
        Assert.Equal(copy.Id.ToString(), shown);
    }

    /// <summary>A name is saved trimmed, and clearing it saves null.</summary>
    [Fact]
    public async Task Name_IsSavedAndCleared()
    {
        DraftEntity entity = new() { Body = "Hello", Addresses = [], FolderId = "root-drafts" };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out _, entity);
        entryMock.Setup(e => e.SaveDraftQuietly(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);

        vm.Name = " Plan ";
        await vm.SaveChanges();
        Assert.Equal("Plan", entity.Name);

        vm.Name = string.Empty;
        await vm.SaveChanges();
        Assert.Null(entity.Name);
    }

    /// <summary>Editing the first line or the name reports the new list title straight away.</summary>
    [Fact]
    public void TitleChanged_FollowsFirstLineThenName()
    {
        DraftViewModel vm = Build(out _, out _);
        List<string> titles = [];
        vm.TitleChanged += titles.Add;

        vm.BodyDocument.Text = "First\nSecond";
        vm.Name = "Named";
        vm.Name = "";

        Assert.Equal(["First", "Named", "First"], titles);
    }

    private static (DraftViewModel Vm, Mock<IEngineConnection> Connection, Mock<IEntryService> Entries, DraftEntity Entity) BuildWithAspects(int? storedAspect = null)
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.MessageAspects).Returns([new MessageAspect { Name = "ENCRYPTED", Key = TestAspect.Encrypted }, new MessageAspect { Name = "SIGNED", Key = TestAspect.Signed }]);
        Mock<IEntryService> entries = new();
        Mock<IEngineConnection> connection = new();
        connection.Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResult { MessageId = "M1" });
        DraftEntity entity = new() { Body = "B", Addresses = [new AddressData { UserName = "BOB", Type = "To" }], FolderId = "root-drafts", MessageAspect = storedAspect };
        return (new DraftViewModel(entity, entries.Object, connection.Object, [], noLogger, controller.Object), connection, entries, entity);
    }

    /// <summary>With aspects configured the picker offers none first and then each aspect, and a draft with no stored aspect starts on none; without aspects it is empty.</summary>
    [Fact]
    public void MessageAspects_PickerOffersNoneThenEachAspect()
    {
        (DraftViewModel vm, _, _, _) = BuildWithAspects();

        Assert.Equal(["ENCRYPTED", "SIGNED"], vm.AvailableMessageAspects.Skip(1).Select(option => option.Label));
        Assert.Null(vm.AvailableMessageAspects[0].Aspect);
        Assert.Equal(string.Empty, vm.AvailableMessageAspects[0].Label);
        Assert.Null(vm.SelectedMessageAspect?.Aspect);
        Assert.Empty(Build(out _, out _).AvailableMessageAspects);
    }

    /// <summary>An aspect no allowed combination uses is not offered, and choosing a priority that blocks the chosen aspect moves the aspect to one that is allowed.</summary>
    [Fact]
    public void MessageAspects_Blocked_AreNotOfferedAndSelectionMoves()
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.MessageAspects).Returns([new MessageAspect { Name = "ENCRYPTED", Key = TestAspect.Encrypted }, new MessageAspect { Name = "SIGNED", Key = TestAspect.Signed }]);
        controller.Setup(c => c.IsDraftAllowed(It.IsAny<IEngineContext>(), It.IsAny<Enum>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<string>()))
            .Returns((IEngineContext _, Enum priority, Enum? level, Enum? aspect, string tag) => !TestAspect.Signed.Equals(aspect) && !(priority.Equals(TestMessagePriority.Flash) && TestAspect.Encrypted.Equals(aspect)));
        DraftEntity entity = new() { Body = "B", Addresses = [], FolderId = "root-drafts", MessageAspect = (int)TestAspect.Encrypted };
        DraftViewModel vm = new(entity, Mock.Of<IEntryService>(), Mock.Of<IEngineConnection>(), [], noLogger, controller.Object);
        Assert.Equal(["", "ENCRYPTED"], vm.AvailableMessageAspects.Select(option => option.Label));
        Assert.Equal("ENCRYPTED", vm.SelectedMessageAspect?.Aspect?.Name);

        vm.SelectedPriority = vm.AvailablePriorities.Single(p => p.Name == "FLASH");

        Assert.Equal("FLASH", vm.SelectedPriority.Name);
        Assert.Null(vm.SelectedMessageAspect?.Aspect);
    }

    /// <summary>A message level the draft handler refuses with the current tag is not offered, and a tag that blocks the chosen level moves the level to one that is allowed.</summary>
    [Fact]
    public void MessageLevels_BlockedWithTag_AreNotOfferedAndSelectionMoves()
    {
        Mock<IEngineController> controller = Mock.Get(MakeEngineController());
        controller.Setup(c => c.MessageLevels).Returns([new MessageLevel { Name = "PUBLIC", Color = "#000000", Key = TestLevel.Public }, new MessageLevel { Name = "SECRET", Color = "#000000", Key = TestLevel.Secret }]);
        controller.Setup(c => c.IsDraftAllowed(It.IsAny<IEngineContext>(), It.IsAny<Enum>(), It.IsAny<Enum?>(), It.IsAny<Enum?>(), It.IsAny<string>()))
            .Returns((IEngineContext _, Enum priority, Enum? level, Enum? aspect, string tag) => !(tag == "LEAK" && TestLevel.Secret.Equals(level)));
        DraftEntity entity = new() { Body = "B", Addresses = [], FolderId = "root-drafts", MessageLevel = (int)TestLevel.Secret };
        DraftViewModel vm = new(entity, Mock.Of<IEntryService>(), Mock.Of<IEngineConnection>(), [], noLogger, controller.Object, currentMessageLevel: "SECRET");
        Assert.Equal("SECRET", vm.SelectedMessageLevel?.Name);

        vm.Tag = "LEAK";

        Assert.Equal("LEAK", vm.Tag);
        Assert.Equal(["PUBLIC"], vm.AvailableMessageLevels.Select(level => level.Name));
        Assert.Equal("PUBLIC", vm.SelectedMessageLevel?.Name);
    }

    /// <summary>A stored aspect is selected again when the draft is opened.</summary>
    [Fact]
    public void MessageAspects_StoredAspectIsSelected()
    {
        (DraftViewModel vm, _, _, _) = BuildWithAspects(storedAspect: (int)TestAspect.Signed);

        Assert.Equal("SIGNED", vm.SelectedMessageAspect?.Aspect?.Name);
    }

    /// <summary>The chosen aspect is sent with the message, and with none chosen none is sent.</summary>
    [Fact]
    public async Task Send_PassesTheSelectedMessageAspect()
    {
        (DraftViewModel vm, Mock<IEngineConnection> connection, _, DraftEntity entity) = BuildWithAspects();
        vm.SelectedMessageAspect = vm.AvailableMessageAspects.Single(option => option.Label == "ENCRYPTED");

        await vm.SendCommand.ExecuteAsync(null);

        connection.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), TestAspect.Encrypted, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal((int)TestAspect.Encrypted, entity.MessageAspect);

        (DraftViewModel plain, Mock<IEngineConnection> plainConnection, _, _) = BuildWithAspects();
        await plain.SendCommand.ExecuteAsync(null);
        plainConnection.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<Enum?>(), It.IsAny<string>(), It.IsAny<Enum?>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
