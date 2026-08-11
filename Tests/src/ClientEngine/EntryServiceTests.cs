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
        _service = new EntryService(messages, drafts, notes, activityLogs, folders, new BlueHeighliner.Comlink.Engine.Control.CurrentUserProvider(), Format);
    }

    /// <summary>Verifies that StoreIncomingMessage creates a message in the Inbox folder.</summary>
    [Fact]
    public async Task StoreIncomingMessageAsync_CreatesMessageInInbox()
    {
        MessageEntity entity = await _service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "SenderUser", "Hello", "Body text",
            [new AddressData { UserName = "LocalUser", Type = "To" }],
            DateTime.UtcNow);

        Assert.NotNull(entity);
        Assert.Equal("SenderUser", Format.GetFromUser(entity.Message));
        Assert.Equal("Hello", Format.GetSubject(entity.Message));
        Assert.Contains("root-inbox", entity.FolderId);
    }

    /// <summary>StoreIncomingMessage sets ReadStatus to Received.</summary>
    [Fact]
    public async Task StoreIncomingMessageAsync_SetsReadStatusReceived()
    {
        MessageEntity entity = await _service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "SenderUser", "Hello", "Body", [], DateTime.UtcNow);

        Assert.Equal(DestinationStatus.Received, entity.ReadStatus);
    }

    /// <summary>StoreIncomingMessage/StoreSentMessage round-trip the IsAlert flag onto the stored message.</summary>
    [Fact]
    public async Task StoreMessage_IsAlertTrue_RoundTripsOnStoredMessage()
    {
        MessageEntity incoming = await _service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "SenderUser", "Hello", "Body", [], DateTime.UtcNow, isAlert: true);
        Assert.True(Format.GetIsAlert(incoming.Message));

        MessageEntity sent = await _service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", "Body", [], DateTime.UtcNow, [], isAlert: true);
        Assert.True(Format.GetIsAlert(sent.Message));
    }

    /// <summary>MarkMessageRead transitions an Inbox record from Received to Read and fires MessageRead.</summary>
    [Fact]
    public async Task MarkMessageRead_ReceivedMessage_TransitionsToReadAndFiresEvent()
    {
        string messageId = Guid.NewGuid().ToString("N");
        await _service.StoreIncomingMessage(messageId, "Sender", "Subj", "Body", [], DateTime.UtcNow);

        MessageEntity? readEntity = null;
        _service.MessageRead += entity => { readEntity = entity; return Task.CompletedTask; };

        MessageEntity? result = await _service.MarkMessageRead(messageId);

        Assert.NotNull(result);
        Assert.Equal(DestinationStatus.Read, result.ReadStatus);
        Assert.NotNull(readEntity);
        Assert.Equal(messageId, readEntity!.MessageId);
    }

    /// <summary>MarkMessageRead on an already-read message is a no-op that returns null and does not re-fire the event.</summary>
    [Fact]
    public async Task MarkMessageRead_AlreadyRead_IsNoOp()
    {
        string messageId = Guid.NewGuid().ToString("N");
        await _service.StoreIncomingMessage(messageId, "Sender", "Subj", "Body", [], DateTime.UtcNow);
        await _service.MarkMessageRead(messageId);

        int eventCount = 0;
        _service.MessageRead += _ => { eventCount++; return Task.CompletedTask; };

        MessageEntity? result = await _service.MarkMessageRead(messageId);

        Assert.Null(result);
        Assert.Equal(0, eventCount);
    }

    /// <summary>MarkMessageRead for a nonexistent message returns null.</summary>
    [Fact]
    public async Task MarkMessageRead_UnknownMessageId_ReturnsNull()
    {
        MessageEntity? result = await _service.MarkMessageRead("does-not-exist");
        Assert.Null(result);
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
            [new AddressData { UserName = "SELF", Type = "To" }], DateTime.UtcNow);
        await _service.StoreSentMessage(messageId, "Hello", "Body",
            [new AddressData { UserName = "SELF", Type = "To" }], DateTime.UtcNow,
            [new UserDeliveryResult { UserName = "SELF", Success = true, AddressedVia = [] }]);

        MessageEntity? updated = await _service.UpdateDeliveryStatus(messageId, "SELF", DestinationStatus.Confirmed);

        Assert.NotNull(updated);
        Assert.True(updated.IsOutbound);
        Assert.Equal(DestinationStatus.Confirmed, Assert.Single(updated.DeliveryStatuses).Status);

        (List<MessageEntity> inboxItems, _) = await _service.GetMessages("root-inbox", 1);
        MessageEntity inboxCopy = Assert.Single(inboxItems);
        Assert.False(inboxCopy.IsOutbound);
        Assert.Empty(inboxCopy.DeliveryStatuses);
    }

    /// <summary>A successful user result seeds the Outbox record with Confirmed status immediately — a successful send already implies full OFT delivery.</summary>
    [Fact]
    public async Task StoreSentMessage_SuccessfulUserResult_SeedsConfirmedStatusImmediately()
    {
        MessageEntity entity = await _service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", "Body", [],
            DateTime.UtcNow, [new UserDeliveryResult { UserName = "SELF", Success = true, AddressedVia = [] }]);

        Assert.Equal(DestinationStatus.Confirmed, Assert.Single(entity.DeliveryStatuses).Status);
        Assert.True(entity.IsOutbound);
    }

    /// <summary>A failed user result seeds the Outbox record with Failed status immediately.</summary>
    [Fact]
    public async Task StoreSentMessage_FailedUserResult_SeedsFailedStatusImmediately()
    {
        MessageEntity entity = await _service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", "Body", [],
            DateTime.UtcNow, [new UserDeliveryResult { UserName = "UNREACHABLE", Success = false, AddressedVia = [] }]);

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
