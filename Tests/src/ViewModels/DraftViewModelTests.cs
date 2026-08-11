namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="DraftViewModel"/>.</summary>
public sealed class DraftViewModelTests
{
    private static readonly ILoggerFactory NoLogger = LoggerFactory.Create(_ => { });

    private static DraftViewModel Build(
        out Mock<IEntryService> entryMock,
        out Mock<IServiceConnection> connMock,
        DraftEntity? entity = null,
        IReadOnlyList<string>? userNames = null)
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
        return new DraftViewModel(ent, entryMock.Object, connMock.Object, userNames ?? [], NoLogger);
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

    // ── SendCommand ───────────────────────────────────────────────────────────

    /// <summary>SendCommand with no addresses sets StatusMessage and does not send.</summary>
    [Fact]
    public async Task SendCommand_NoAddresses_SetsStatusMessageAndDoesNotSend()
    {
        DraftViewModel vm = Build(out _, out Mock<IServiceConnection> connMock);

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Add at least one recipient", vm.StatusMessage);
        connMock.Verify(c => c.SendMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
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
                It.IsAny<List<AddressRequest>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sendResult);

        MessageEntity sentMessage = new() { MessageId = "MSG-001" };
        entryMock.Setup(e => e.SaveDraft(It.IsAny<DraftEntity>())).Returns(Task.CompletedTask);
        entryMock.Setup(e => e.StoreSentMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<AddressData>>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<UserDeliveryResult>>(), It.IsAny<bool>()))
            .ReturnsAsync(sentMessage);

        await vm.SendCommand.ExecuteAsync(null);

        Assert.True(vm.IsSent);
        Assert.Equal("Sent", vm.StatusMessage);
        Assert.False(vm.IsSaving);
    }
}
