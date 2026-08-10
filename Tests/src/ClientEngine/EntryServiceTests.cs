namespace BlueHeighliner.Comlink.Tests.ClientEngine;

/// <summary>Integration tests for <see cref="EntryService"/> using a real LiteDB database.</summary>
public sealed class EntryServiceTests : IDisposable
{
    private static readonly IMessageFormat Format = new TestMessageFormat();

    private readonly string _appName = Guid.NewGuid().ToString();
    private readonly LiteDbContext _ctx;
    private readonly EntryService _service;

    /// <summary>Initializes the test with a real <see cref="LiteDbContext"/> and a fresh <see cref="EntryService"/>.</summary>
    public EntryServiceTests()
    {
        _ctx = new LiteDbContext(new TestAppDataPathProvider(_appName));
        _ctx.Initialize();

        MessageRepository messages = new(_ctx);
        DraftRepository drafts = new(_ctx);
        NoteRepository notes = new(_ctx);
        ActivityLogRepository activityLogs = new(_ctx);
        FolderRepository folders = new(_ctx);
        _service = new EntryService(messages, drafts, notes, activityLogs, folders, new BlueHeighliner.Comlink.Engine.Control.CurrentSiteProvider(), Format);
    }

    /// <summary>Verifies that StoreIncomingMessage creates a message in the Inbox folder.</summary>
    [Fact]
    public async Task StoreIncomingMessageAsync_CreatesMessageInInbox()
    {
        MessageEntity entity = await _service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "SenderSite", "Hello", "Body text",
            [new AddressData { SiteName = "LocalSite", Type = "To" }],
            DateTime.UtcNow);

        Assert.NotNull(entity);
        Assert.Equal("SenderSite", Format.GetFromSite(entity.Message));
        Assert.Equal("Hello", Format.GetSubject(entity.Message));
        Assert.Contains("root-inbox", entity.FolderId);
    }

    /// <summary>Verifies that CreateDraft creates an unsent draft in the Drafts folder.</summary>
    [Fact]
    public async Task CreateDraftAsync_CreatesDraftInDraftsFolder()
    {
        DraftEntity entity = await _service.CreateDraft();

        Assert.NotNull(entity);
        Assert.Contains("root-drafts", entity.FolderId);
        Assert.False(entity.IsSent);
    }

    /// <summary>Verifies that CreateNote creates a note in the Notes folder.</summary>
    [Fact]
    public async Task CreateNoteAsync_CreatesNoteInNotesFolder()
    {
        NoteEntity entity = await _service.CreateNote();

        Assert.NotNull(entity);
        Assert.Contains("root-notes", entity.FolderId);
    }

    /// <summary>Verifies that GetMessages returns the correct count and items for a paginated result.</summary>
    [Fact]
    public async Task GetMessagesAsync_ReturnsPaginatedMessages()
    {
        string inboxId = "root-inbox";
        for (int i = 0; i < 5; i++)
        {
            await _service.StoreIncomingMessage(
                Guid.NewGuid().ToString(), "Sender", $"Subject {i}", "Body",
                [], DateTime.UtcNow.AddMinutes(-i));
        }

        (List<MessageEntity> items, int total) = await _service.GetMessages(inboxId, page: 1);

        Assert.Equal(5, total);
        Assert.Equal(5, items.Count);
    }

    /// <summary>Verifies that GetMessages orders results newest-first by received date.</summary>
    [Fact]
    public async Task GetMessagesAsync_SortsNewestFirst()
    {
        MessageEntity first = await _service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "S", "First", "", [], DateTime.UtcNow.AddHours(-2));
        MessageEntity second = await _service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "S", "Second", "", [], DateTime.UtcNow);

        (List<MessageEntity> items, int _) = await _service.GetMessages("root-inbox", 1);

        Assert.Equal("Second", Format.GetSubject(items[0].Message));
        Assert.Equal("First", Format.GetSubject(items[1].Message));
    }

    /// <summary>Verifies that StoreIncomingMessage fires the MessageInserted event after persisting.</summary>
    [Fact]
    public async Task StoreIncomingMessageAsync_FiresMessageInsertedEvent()
    {
        string? receivedSubject = null;
        _service.MessageInserted += entity =>
        {
            receivedSubject = Format.GetSubject(entity.Message);
            return Task.CompletedTask;
        };

        await _service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "S", "EventTest", "", [], DateTime.UtcNow);

        Assert.Equal("EventTest", receivedSubject);
    }

    /// <summary>A self-addressed message creates an Inbox and an Outbox record sharing the same MessageId; delivery-status updates must only ever touch the Outbox record.</summary>
    [Fact]
    public async Task UpdateDeliveryStatus_SelfAddressedMessage_OnlyUpdatesOutboundRecord()
    {
        string messageId = Guid.NewGuid().ToString("N");
        await _service.StoreIncomingMessage(messageId, "SELF", "Hello", "Body",
            [new AddressData { SiteName = "SELF", Type = "To" }], DateTime.UtcNow);
        await _service.StoreSentMessage(messageId, "Hello", "Body",
            [new AddressData { SiteName = "SELF", Type = "To" }], DateTime.UtcNow,
            [new SiteDeliveryResult { SiteName = "SELF", Success = true, AddressedVia = [] }]);

        MessageEntity? updated = await _service.UpdateDeliveryStatus(messageId, "SELF", DestinationStatus.Confirmed);

        Assert.NotNull(updated);
        Assert.True(updated.IsOutbound);
        Assert.Equal(DestinationStatus.Confirmed, Assert.Single(updated.DeliveryStatuses).Status);

        (List<MessageEntity> inboxItems, _) = await _service.GetMessages("root-inbox", 1);
        MessageEntity inboxCopy = Assert.Single(inboxItems);
        Assert.False(inboxCopy.IsOutbound);
        Assert.Empty(inboxCopy.DeliveryStatuses);
    }

    /// <summary>A successful site result seeds the Outbox record with Confirmed status immediately — a successful send already implies full OFT delivery.</summary>
    [Fact]
    public async Task StoreSentMessage_SuccessfulSiteResult_SeedsConfirmedStatusImmediately()
    {
        MessageEntity entity = await _service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", "Body", [],
            DateTime.UtcNow, [new SiteDeliveryResult { SiteName = "SELF", Success = true, AddressedVia = [] }]);

        Assert.Equal(DestinationStatus.Confirmed, Assert.Single(entity.DeliveryStatuses).Status);
        Assert.True(entity.IsOutbound);
    }

    /// <summary>A failed site result seeds the Outbox record with Failed status immediately.</summary>
    [Fact]
    public async Task StoreSentMessage_FailedSiteResult_SeedsFailedStatusImmediately()
    {
        MessageEntity entity = await _service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", "Body", [],
            DateTime.UtcNow, [new SiteDeliveryResult { SiteName = "UNREACHABLE", Success = false, AddressedVia = [] }]);

        Assert.Equal(DestinationStatus.Failed, Assert.Single(entity.DeliveryStatuses).Status);
        Assert.True(entity.IsOutbound);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ctx.Dispose();
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, _appName);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
