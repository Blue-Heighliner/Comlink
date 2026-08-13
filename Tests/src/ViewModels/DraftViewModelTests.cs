namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="DraftViewModel"/>.</summary>
public sealed class DraftViewModelTests
{
    private static readonly ILoggerFactory NoLogger = LoggerFactory.Create(_ => { });

    private static IMessagePriorityProvider MakePriorityProvider()
    {
        Mock<IMessagePriorityProvider> mock = new();
        mock.Setup(p => p.GetPriorities()).Returns([
            new MessagePriorityOption { Name = "ROUTINE", Value = 0 },
            new MessagePriorityOption { Name = "FLASH", Value = 3 }
        ]);
        return mock.Object;
    }

    private static IAlertConfiguration MakeAlertConfiguration(string alertText = "ALERT")
    {
        Mock<IAlertConfiguration> mock = new();
        mock.Setup(a => a.AlertText).Returns(alertText);
        return mock.Object;
    }

    private static IAlertComposeConfiguration MakeAlertComposeConfiguration(bool composeAlertsEnabled = true)
    {
        Mock<IAlertComposeConfiguration> mock = new();
        mock.Setup(a => a.ComposeAlertsEnabled).Returns(composeAlertsEnabled);
        return mock.Object;
    }

    private static IMessageTagConfiguration MakeTagConfiguration(bool tagsEnabled = true, string tagLabel = "Tag")
    {
        Mock<IMessageTagConfiguration> mock = new();
        mock.Setup(t => t.TagsEnabled).Returns(tagsEnabled);
        mock.Setup(t => t.TagLabel).Returns(tagLabel);
        return mock.Object;
    }

    private static IMessageTagPriorityPolicy MakeTagPriorityPolicy(IReadOnlyList<TagPriorityBlock>? blocks = null)
    {
        Mock<IMessageTagPriorityPolicy> mock = new();
        mock.Setup(p => p.GetBlockedCombinations()).Returns(blocks ?? []);
        return mock.Object;
    }

    private static DraftViewModel Build(
        out Mock<IEntryService> entryMock,
        out Mock<IServiceConnection> connMock,
        DraftEntity? entity = null,
        IReadOnlyList<string>? userNames = null,
        string alertText = "ALERT",
        bool composeAlertsEnabled = true,
        bool tagsEnabled = true,
        string tagLabel = "Tag",
        IReadOnlyList<TagPriorityBlock>? blockedCombinations = null)
    {
        entryMock = new Mock<IEntryService>();
        connMock = new Mock<IServiceConnection>();
        connMock.Setup(c => c.GetUserNames(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());
        DraftEntity ent = entity ?? new DraftEntity
        {
            Subject = "Initial Subject",
            Body = "Hello",
            Addresses = [],
            FolderId = "root-drafts"
        };
        return new DraftViewModel(ent, entryMock.Object, connMock.Object, userNames ?? [], NoLogger, MakePriorityProvider(),
            MakeAlertConfiguration(alertText), MakeAlertComposeConfiguration(composeAlertsEnabled),
            MakeTagConfiguration(tagsEnabled, tagLabel), MakeTagPriorityPolicy(blockedCombinations));
    }

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>Constructor sets Subject from entity.</summary>
    [Fact]
    public void Constructor_SetsSubjectFromEntity()
    {
        DraftViewModel vm = Build(out _, out _);
        Assert.Equal("Initial Subject", vm.Subject);
    }

    /// <summary>Constructor sets IsSent from entity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_SetsIsSentFromEntity(bool isSent)
    {
        DraftEntity entity = new() { Subject = "X", IsSent = isSent };
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

    /// <summary>AvailablePriorities is populated from IMessagePriorityProvider.GetPriorities().</summary>
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
        DraftEntity entity = new() { Subject = "X", Priority = 3 };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        Assert.Equal("FLASH", vm.SelectedPriority.Name);
        Assert.Equal(3, vm.SelectedPriority.Value);
    }

    /// <summary>SelectedPriority falls back to the first available option when the entity's Priority matches none.</summary>
    [Fact]
    public void Constructor_SelectedPriorityFallsBackToFirstWhenNoMatch()
    {
        DraftEntity entity = new() { Subject = "X", Priority = 99 };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        Assert.Equal("ROUTINE", vm.SelectedPriority.Name);
    }

    /// <summary>Constructor sets Tag from entity.</summary>
    [Fact]
    public void Constructor_SetsTagFromEntity()
    {
        DraftEntity entity = new() { Subject = "X", Tag = "URGENT" };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        Assert.Equal("URGENT", vm.Tag);
    }

    /// <summary>TagsEnabled is sourced from IMessageTagConfiguration.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_TagsEnabledFromTagConfiguration(bool enabled)
    {
        DraftViewModel vm = Build(out _, out _, tagsEnabled: enabled);
        Assert.Equal(enabled, vm.TagsEnabled);
    }

    /// <summary>TagLabel is sourced from IMessageTagConfiguration.TagLabel, so a host can rename the tag input.</summary>
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
        DraftEntity entity = new() { Subject = "X", Tag = "URGENT", Priority = 3 };
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "URGENT", Priority = 3 }];
        DraftViewModel vm = Build(out _, out _, entity: entity, blockedCombinations: blocks);

        Assert.DoesNotContain(vm.AvailablePriorities, p => p.Name == "FLASH");
        Assert.Equal("ROUTINE", vm.SelectedPriority.Name);
    }

    /// <summary>Setting Tag to a value blocked for the currently selected priority is rejected, reverting to the previous tag.</summary>
    [Fact]
    public void Tag_SetToValueBlockedForCurrentPriority_RevertsToPreviousValue()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM", Priority = null }];
        DraftViewModel vm = Build(out _, out _, blockedCombinations: blocks);

        vm.Tag = "SPAM";

        Assert.Equal(string.Empty, vm.Tag);
    }

    /// <summary>Setting Tag to a value not blocked for the current priority is accepted.</summary>
    [Fact]
    public void Tag_SetToUnblockedValue_IsAccepted()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM", Priority = null }];
        DraftViewModel vm = Build(out _, out _, blockedCombinations: blocks);

        vm.Tag = "URGENT";

        Assert.Equal("URGENT", vm.Tag);
    }

    /// <summary>Setting Tag to a value that blocks another (not currently selected) priority hides that priority from AvailablePriorities.</summary>
    [Fact]
    public void Tag_SetToValueBlockingAnotherPriority_RemovesItFromAvailablePriorities()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "URGENT", Priority = 3 }];
        DraftViewModel vm = Build(out _, out _, blockedCombinations: blocks);
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
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM", Priority = null }];
        DraftViewModel vm = Build(out _, out _, blockedCombinations: blocks);
        vm.Tag = "URGENT";

        vm.Tag = "SPAM";

        Assert.Equal("URGENT", vm.Tag);
    }

    /// <summary>AlertLabel is sourced from IAlertConfiguration.AlertText — the same text used in the title bar's alert box.</summary>
    [Fact]
    public void Constructor_AlertLabelFromAlertConfiguration()
    {
        DraftViewModel vm = Build(out _, out _, alertText: "!ALERT!");
        Assert.Equal("!ALERT!", vm.AlertLabel);
    }

    /// <summary>ComposeAlertsEnabled is sourced from IAlertComposeConfiguration.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_ComposeAlertsEnabledFromAlertComposeConfiguration(bool enabled)
    {
        DraftViewModel vm = Build(out _, out _, composeAlertsEnabled: enabled);
        Assert.Equal(enabled, vm.ComposeAlertsEnabled);
    }

    /// <summary>PlsoMode is freely settable to any of its three states.</summary>
    [Theory]
    [InlineData(PlsoMode.Off)]
    [InlineData(PlsoMode.On)]
    [InlineData(PlsoMode.Spaces)]
    public void PlsoMode_CanBeSet(PlsoMode mode)
    {
        DraftViewModel vm = Build(out _, out _);

        vm.PlsoMode = mode;

        Assert.Equal(mode, vm.PlsoMode);
    }

    /// <summary>PlsoButtonText reflects the current PlsoMode.</summary>
    [Theory]
    [InlineData(PlsoMode.Off, "PLSO OFF")]
    [InlineData(PlsoMode.On, "PLSO ON")]
    [InlineData(PlsoMode.Spaces, "PLSO SPACES")]
    public void PlsoButtonText_ReflectsPlsoMode(PlsoMode mode, string expected)
    {
        DraftViewModel vm = Build(out _, out _);

        vm.PlsoMode = mode;

        Assert.Equal(expected, vm.PlsoButtonText);
    }

    /// <summary>Constructor loads addresses from entity.</summary>
    [Fact]
    public void Constructor_LoadsAddressesFromEntity()
    {
        DraftEntity entity = new()
        {
            Subject = "Sub",
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

    /// <summary>AddressTypes is ["To", "Cc"].</summary>
    [Fact]
    public void AddressTypes_IsToAndCc()
    {
        DraftViewModel vm = Build(out _, out _);
        Assert.Equal(["To", "Cc"], vm.AddressTypes);
    }

    /// <summary>Id is a non-empty string.</summary>
    [Fact]
    public void Id_IsNonEmpty()
    {
        DraftViewModel vm = Build(out _, out _);
        Assert.NotEmpty(vm.Id);
    }

    // ── NewAddressUser auto-uppercase ─────────────────────────────────────────

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

    // ── AddAddressCommand ─────────────────────────────────────────────────────

    /// <summary>AddAddressCommand with a valid user adds to Addresses.</summary>
    [Fact]
    public void AddAddressCommand_ValidUser_AddsAddress()
    {
        DraftViewModel vm = Build(out _, out _);
        vm.NewAddressUser = "BRAVO";
        vm.NewAddressType = "Cc";

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

    // ── RemoveAddressCommand ──────────────────────────────────────────────────

    /// <summary>RemoveAddressCommand removes the specified address.</summary>
    [Fact]
    public void RemoveAddressCommand_RemovesAddress()
    {
        DraftEntity entity = new()
        {
            Subject = "S",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }]
        };
        DraftViewModel vm = Build(out _, out _, entity: entity);
        AddressData addr = vm.Addresses[0];

        vm.RemoveAddressCommand.Execute(addr);

        Assert.Empty(vm.Addresses);
    }

    // ── InsertFillIn ──────────────────────────────────────────────────────────

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

    // ── SaveCommand ───────────────────────────────────────────────────────────

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
        DraftEntity entity = new() { Subject = "X", FolderId = "root-drafts" };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out _, entity: entity);
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        vm.SelectedPriority = vm.AvailablePriorities.Single(p => p.Name == "FLASH");

        await vm.SaveCommand.ExecuteAsync(null);

        entryMock.Verify(e => e.SaveDraft(It.Is<DraftEntity>(d => d.Priority == 3)), Times.Once);
    }

    /// <summary>SaveCommand persists the current Tag value onto the draft entity.</summary>
    [Fact]
    public async Task SaveCommand_PersistsTagOnEntity()
    {
        DraftEntity entity = new() { Subject = "X", FolderId = "root-drafts" };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out _, entity: entity);
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        vm.Tag = "URGENT";

        await vm.SaveCommand.ExecuteAsync(null);

        entryMock.Verify(e => e.SaveDraft(It.Is<DraftEntity>(d => d.Tag == "URGENT")), Times.Once);
    }

    // ── SendCommand ───────────────────────────────────────────────────────────

    /// <summary>SendCommand with no addresses sets StatusMessage and does not send.</summary>
    [Fact]
    public async Task SendCommand_NoAddresses_SetsStatusMessageAndDoesNotSend()
    {
        DraftViewModel vm = Build(out _, out Mock<IServiceConnection> connMock);

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Add at least one recipient", vm.StatusMessage);
        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>SendCommand with addresses and successful send sets IsSent and StatusMessage.</summary>
    [Fact]
    public async Task SendCommand_WithAddresses_SendsAndSetsIsSent()
    {
        DraftEntity entity = new()
        {
            Subject = "Hello",
            Body = "World",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }],
            FolderId = "root-drafts"
        };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out Mock<IServiceConnection> connMock, entity: entity);

        SendMessageResult sendResult = new()
        {
            MessageId = "MSG-001",
            UserResults = [new UserDeliveryResult { UserName = "ALPHA", Success = true, AddressedVia = [] }]
        };
        connMock.Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendResult);

        MessageEntity sentMessage = new() { MessageId = "MSG-001" };
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        entryMock.Setup(e => e.StoreSentMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<AddressData>>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<UserDeliveryResult>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(sentMessage);

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
            Subject = "Hello",
            Body = "World",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }],
            FolderId = "root-drafts"
        };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out Mock<IServiceConnection> connMock, entity: entity);
        vm.SelectedPriority = vm.AvailablePriorities.Single(p => p.Name == "FLASH");

        SendMessageResult sendResult = new()
        {
            MessageId = "MSG-001",
            UserResults = [new UserDeliveryResult { UserName = "ALPHA", Success = true, AddressedVia = [] }]
        };
        connMock.Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), 3, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendResult);

        MessageEntity sentMessage = new() { MessageId = "MSG-001" };
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        entryMock.Setup(e => e.StoreSentMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<AddressData>>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<UserDeliveryResult>>(), It.IsAny<bool>(), 3, It.IsAny<string>()))
            .ReturnsAsync(sentMessage);

        await vm.SendCommand.ExecuteAsync(null);

        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), 3, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        entryMock.Verify(e => e.StoreSentMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<AddressData>>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyList<UserDeliveryResult>>(), It.IsAny<bool>(), 3, It.IsAny<string>()), Times.Once);
        Assert.True(vm.IsSent);
    }

    /// <summary>SendCommand passes the current Tag to both SendMessage and StoreSentMessage.</summary>
    [Fact]
    public async Task SendCommand_PassesTagToSendAndStore()
    {
        DraftEntity entity = new()
        {
            Subject = "Hello",
            Body = "World",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }],
            FolderId = "root-drafts"
        };
        DraftViewModel vm = Build(out Mock<IEntryService> entryMock, out Mock<IServiceConnection> connMock, entity: entity);
        vm.Tag = "URGENT";

        SendMessageResult sendResult = new()
        {
            MessageId = "MSG-001",
            UserResults = [new UserDeliveryResult { UserName = "ALPHA", Success = true, AddressedVia = [] }]
        };
        connMock.Setup(c => c.SendMessage(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), "URGENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendResult);

        MessageEntity sentMessage = new() { MessageId = "MSG-001" };
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        entryMock.Setup(e => e.StoreSentMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<AddressData>>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<UserDeliveryResult>>(), It.IsAny<bool>(), It.IsAny<int>(), "URGENT"))
            .ReturnsAsync(sentMessage);

        await vm.SendCommand.ExecuteAsync(null);

        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), "URGENT", It.IsAny<CancellationToken>()), Times.Once);
        entryMock.Verify(e => e.StoreSentMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<AddressData>>(), It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyList<UserDeliveryResult>>(), It.IsAny<bool>(), It.IsAny<int>(), "URGENT"), Times.Once);
        Assert.True(vm.IsSent);
    }

    /// <summary>
    /// SendCommand refuses to send when Tag/SelectedPriority form a blocked combination, as a defense-in-depth
    /// safety net behind the live UI-level prevention (which SelectedPriority's plain setter does not itself enforce).
    /// </summary>
    [Fact]
    public async Task SendCommand_BlockedTagPriorityCombination_SetsStatusMessageAndDoesNotSend()
    {
        DraftEntity entity = new()
        {
            Subject = "Hello",
            Body = "World",
            Addresses = [new AddressData { UserName = "ALPHA", Type = "To" }],
            FolderId = "root-drafts"
        };
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "URGENT", Priority = 3 }];
        DraftViewModel vm = Build(out _, out Mock<IServiceConnection> connMock, entity: entity, blockedCombinations: blocks);
        vm.Tag = "URGENT";
        vm.SelectedPriority = new MessagePriorityOption { Name = "FLASH", Value = 3 };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("This tag/priority combination is not allowed", vm.StatusMessage);
        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
