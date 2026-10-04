namespace BlueHeighliner.Comlink.Tests.Integration;

/// <summary>Integration tests for <see cref="EntryService"/> using a real LiteDB database.</summary>
public sealed class EntryServiceTests : IDisposable
{
    /// <summary>Initializes the test with a real <see cref="LiteDbContext"/> and a fresh <see cref="EntryService"/>.</summary>
    public EntryServiceTests()
    {
        ctx = new LiteDbContext(new TestAppDataPathProvider(appName));
        ctx.Initialize();

        MessageRepository messages = new(ctx);
        DraftRepository drafts = new(ctx);
        NoteRepository notes = new(ctx);
        ActivityLogRepository activityLogs = new(ctx);
        FolderRepository folders = new(ctx);
        service = new EntryService(messages, drafts, notes, activityLogs, folders, new CurrentUserProvider(), format);
    }

    private readonly IEngineController format = new TestEngineController();
    private readonly string appName = Guid.NewGuid().ToString();
    private readonly LiteDbContext ctx;
    private readonly EntryService service;

    /// <summary>Verifies that StoreIncomingMessage creates a message in the Inbox folder.</summary>
    [Fact]
    public async Task StoreIncomingMessageAsync_CreatesMessageInInbox()
    {
        MessageEntity entity = await service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "SenderUser", "Hello",
            [new AddressData { UserName = "LocalUser", Type = "To" }],
            DateTime.UtcNow);

        Assert.NotNull(entity);
        Assert.Equal("SenderUser", format.GetFromUser(entity.Message));
        Assert.Equal("Hello", format.GetBody(entity.Message));
        Assert.Contains("root-inbox", entity.FolderId);
    }

    /// <summary>StoreIncomingMessage sets ReadStatus to Received.</summary>
    [Fact]
    public async Task StoreIncomingMessageAsync_SetsReadStatusReceived()
    {
        MessageEntity entity = await service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "SenderUser", "Hello", [], DateTime.UtcNow);

        Assert.Equal(DestinationStatus.Received, entity.ReadStatus);
    }

    /// <summary>StoreIncomingMessage/StoreSentMessage round-trip the IsAlert flag onto the stored message.</summary>
    [Fact]
    public async Task StoreMessage_IsAlertTrue_RoundTripsOnStoredMessage()
    {
        MessageEntity incoming = await service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "SenderUser", "Hello", [], DateTime.UtcNow, isAlert: true);
        Assert.True(format.GetIsAlert(incoming.Message));

        MessageEntity sent = await service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", [], DateTime.UtcNow, [], isAlert: true);
        Assert.True(format.GetIsAlert(sent.Message));
    }

    /// <summary>StoreIncomingMessage/StoreSentMessage round-trip the Priority number onto the stored message.</summary>
    [Fact]
    public async Task StoreMessage_Priority_RoundTripsOnStoredMessage()
    {
        MessageEntity incoming = await service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "SenderUser", "Hello", [], DateTime.UtcNow, priority: TestMessagePriority.Level2);
        Assert.Equal(2, format.GetPriority(incoming.Message));

        MessageEntity sent = await service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", [], DateTime.UtcNow, [], priority: TestMessagePriority.Level3);
        Assert.Equal(3, format.GetPriority(sent.Message));
    }

    /// <summary>MarkMessageRead transitions an Inbox record from Received to Read and fires MessageRead.</summary>
    [Fact]
    public async Task MarkMessageRead_ReceivedMessage_TransitionsToReadAndFiresEvent()
    {
        string messageId = Guid.NewGuid().ToString("N");
        await service.StoreIncomingMessage(messageId, "Sender", "Subj", [], DateTime.UtcNow);

        MessageEntity? readEntity = null;
        service.MessageRead += entity => { readEntity = entity; return Task.CompletedTask; };

        MessageEntity? result = await service.MarkMessageRead(messageId);

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
        await service.StoreIncomingMessage(messageId, "Sender", "Subj", [], DateTime.UtcNow);
        await service.MarkMessageRead(messageId);

        int eventCount = 0;
        service.MessageRead += _ => { eventCount++; return Task.CompletedTask; };

        MessageEntity? result = await service.MarkMessageRead(messageId);

        Assert.Null(result);
        Assert.Equal(0, eventCount);
    }

    /// <summary>MarkMessageRead for a nonexistent message returns null.</summary>
    [Fact]
    public async Task MarkMessageRead_UnknownMessageId_ReturnsNull()
    {
        MessageEntity? result = await service.MarkMessageRead("does-not-exist");
        Assert.Null(result);
    }

    /// <summary>Verifies that CreateDraft creates an unsent draft in the Drafts folder.</summary>
    [Fact]
    public async Task CreateDraftAsync_CreatesDraftInDraftsFolder()
    {
        DraftEntity entity = await service.CreateDraft();

        Assert.NotNull(entity);
        Assert.Contains("root-drafts", entity.FolderId);
        Assert.False(entity.IsSent);
    }

    /// <summary>Verifies that CreateNote creates a note in the Notes folder.</summary>
    [Fact]
    public async Task CreateNoteAsync_CreatesNoteInNotesFolder()
    {
        NoteEntity entity = await service.CreateNote();

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
            await service.StoreIncomingMessage(
                Guid.NewGuid().ToString(), "Sender", $"Body {i}",
                [], DateTime.UtcNow.AddMinutes(-i));
        }

        (List<MessageEntity> items, int total) = await service.GetMessages(inboxId, page: 1);

        Assert.Equal(5, total);
        Assert.Equal(5, items.Count);
    }

    /// <summary>Verifies that GetMessages orders results newest-first by received date.</summary>
    [Fact]
    public async Task GetMessagesAsync_SortsNewestFirst()
    {
        MessageEntity first = await service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "S", "First", [], DateTime.UtcNow.AddHours(-2));
        MessageEntity second = await service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "S", "Second", [], DateTime.UtcNow);

        (List<MessageEntity> items, int _) = await service.GetMessages("root-inbox", 1);

        Assert.Equal("Second", format.GetBody(items[0].Message));
        Assert.Equal("First", format.GetBody(items[1].Message));
    }

    /// <summary>A message search matches case-insensitively against the body, and excludes messages that don't match.</summary>
    [Fact]
    public async Task GetMessagesAsync_SearchMatchesBodyCaseInsensitively()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "Sender", "Quarterly Report", [], DateTime.UtcNow);
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "Sender", "Lunch plans", [], DateTime.UtcNow);

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { Search = "report" });

        Assert.Equal(1, total);
        Assert.Equal("Quarterly Report", format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>A message search also matches the sender, the tag, and the priority label, not just the body.</summary>
    [Theory]
    [InlineData("Sender", "ALPHA")]
    [InlineData("URGENT", "ALPHA")]
    [InlineData("Normal", "ALPHA")]
    public async Task GetMessagesAsync_SearchMatchesSenderTagAndPriority(string search, string expectedBody)
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "Sender", expectedBody, [], DateTime.UtcNow, priority: TestMessagePriority.Normal, tag: "URGENT");
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "BRAVO", "Unrelated", [], DateTime.UtcNow, priority: TestMessagePriority.Level9);

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { Search = search });

        Assert.Equal(1, total);
        Assert.Equal(expectedBody, format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>A message search with no matches returns an empty page and a zero total rather than falling back to the unfiltered folder.</summary>
    [Fact]
    public async Task GetMessagesAsync_SearchWithNoMatches_ReturnsEmpty()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "Sender", "Hello", [], DateTime.UtcNow);

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { Search = "nonexistent" });

        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    /// <summary>A draft search matches the body; an empty or whitespace-only search behaves as no search.</summary>
    [Theory]
    [InlineData("Budget", 1)]
    [InlineData("", 2)]
    [InlineData("   ", 2)]
    [InlineData("nonexistent", 0)]
    public async Task GetDraftsAsync_SearchMatchesBody(string search, int expectedTotal)
    {
        DraftEntity budget = await service.CreateDraft();
        budget.Body = "Budget review";
        await service.SaveDraft(budget);
        DraftEntity other = await service.CreateDraft();
        other.Body = "Team lunch";
        await service.SaveDraft(other);

        (List<DraftEntity> items, int total) = await service.GetDrafts("root-drafts", 1, alphabetical: false, filter: new EntryFilter { Search = search });

        Assert.Equal(expectedTotal, total);
        Assert.Equal(expectedTotal, items.Count);
    }

    /// <summary>A note search matches the body text.</summary>
    [Fact]
    public async Task GetNotesAsync_SearchMatchesBody()
    {
        NoteEntity match = await service.CreateNote();
        match.Body = "Remember to call the client back";
        await service.SaveNote(match);
        NoteEntity other = await service.CreateNote();
        other.Body = "Grocery list";
        await service.SaveNote(other);

        (List<NoteEntity> items, int total) = await service.GetNotes("root-notes", 1, alphabetical: false, filter: new EntryFilter { Search = "client" });

        Assert.Equal(1, total);
        Assert.Equal("Remember to call the client back", Assert.Single(items).Body);
    }

    /// <summary>A message filter's SecurityLevel matches exactly, case-insensitively, not as a substring.</summary>
    [Fact]
    public async Task GetMessagesAsync_FilterMatchesSecurityLevelExactly()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Restricted memo", [], DateTime.UtcNow, securityLevel: "RESTRICTED");
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Public memo", [], DateTime.UtcNow, securityLevel: "PUBLIC");

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { SecurityLevel = "restricted" });

        Assert.Equal(1, total);
        Assert.Equal("Restricted memo", format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>IncomingMessageExists reports whether the Inbox holds the ID, and an Outbox-only record does not count.</summary>
    [Fact]
    public async Task IncomingMessageExists_ReflectsInboxRecordsOnly()
    {
        await service.StoreIncomingMessage("IN1", "S", "Subject", [], DateTime.UtcNow);
        await service.StoreSentMessage("OUT1", "Subject", [], DateTime.UtcNow, []);

        Assert.True(await service.IncomingMessageExists("IN1"));
        Assert.False(await service.IncomingMessageExists("OUT1"));
        Assert.False(await service.IncomingMessageExists("NOPE"));
    }

    /// <summary>A message filter's Author matches the sender by case-insensitive substring.</summary>
    [Fact]
    public async Task GetMessagesAsync_FilterAuthor_MatchesSenderSubstring()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "ALICE", "From alice", [], DateTime.UtcNow);
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "BOB", "From bob", [], DateTime.UtcNow);

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { Author = "lic" });

        Assert.Equal(1, total);
        Assert.Equal("From alice", format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>A message filter's Destination matches any addressee by case-insensitive substring.</summary>
    [Fact]
    public async Task GetMessagesAsync_FilterDestination_MatchesAnyAddressee()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "To carol", [new AddressData { UserName = "DAVE" }, new AddressData { UserName = "CAROL" }], DateTime.UtcNow);
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "To dave only", [new AddressData { UserName = "DAVE" }], DateTime.UtcNow);

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { Destination = "carol" });

        Assert.Equal(1, total);
        Assert.Equal("To carol", format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>A draft filter's Destination matches any addressee by case-insensitive substring.</summary>
    [Fact]
    public async Task GetDraftsAsync_FilterDestination_MatchesAnyAddressee()
    {
        DraftEntity match = await service.CreateDraft();
        match.Body = "Match";
        match.Addresses = [new AddressData { UserName = "ERIN" }];
        await service.SaveDraft(match);
        DraftEntity other = await service.CreateDraft();
        other.Body = "Other";
        other.Addresses = [new AddressData { UserName = "FRANK" }];
        await service.SaveDraft(other);

        (List<DraftEntity> items, int total) = await service.GetDrafts("root-drafts", 1, alphabetical: false, filter: new EntryFilter { Destination = "eri" });

        Assert.Equal(1, total);
        Assert.Equal("Match", Assert.Single(items).Body);
    }

    /// <summary>A message filter's Priority matches the exact priority level.</summary>
    [Fact]
    public async Task GetMessagesAsync_FilterMatchesPriorityExactly()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "High priority", [], DateTime.UtcNow, priority: TestMessagePriority.Level2);
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Low priority", [], DateTime.UtcNow, priority: TestMessagePriority.Normal);

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { Priority = TestMessagePriority.Level2 });

        Assert.Equal(1, total);
        Assert.Equal("High priority", format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>A message filter's AlertOnly excludes every non-alert message.</summary>
    [Fact]
    public async Task GetMessagesAsync_FilterAlertOnly_ExcludesNonAlerts()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Urgent", [], DateTime.UtcNow, isAlert: true);
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Routine", [], DateTime.UtcNow, isAlert: false);

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { AlertOnly = true });

        Assert.Equal(1, total);
        Assert.Equal("Urgent", format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>A message filter's DateFrom/DateTo bound the received date inclusively, by date only.</summary>
    [Fact]
    public async Task GetMessagesAsync_FilterByDateRange_MatchesExactInstantBounds()
    {
        // DateFrom/DateTo compare as exact instants, in whatever kind LiteDB round-trips ReceivedAt as (its
        // BsonMapper converts to local time on read) - so the boundaries here are built the same way a
        // DatePicker/TimePicker's own local-calendar selection would be, rather than in UTC terms.
        DateTime today = DateTime.Now.Date;
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Today early", [], today.AddHours(1));
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Today late", [], today.AddHours(23));
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Yesterday", [], today.AddDays(-1));
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Tomorrow", [], today.AddDays(1));

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1,
            filter: new EntryFilter { DateFrom = today, DateTo = today.AddDays(1).AddTicks(-1) });

        Assert.Equal(2, total);
        Assert.Equal(["Today early", "Today late"], items.Select(i => format.GetBody(i.Message)).OrderBy(s => s));
    }

    /// <summary>A message filter's DateFrom/DateTo also bound an exact time of day, not just the calendar date.</summary>
    [Fact]
    public async Task GetMessagesAsync_FilterByDateRange_MatchesExactTimeOfDay()
    {
        DateTime today = DateTime.Now.Date;
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Before window", [], today.AddHours(9));
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "In window", [], today.AddHours(13));
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "After window", [], today.AddHours(18));

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1,
            filter: new EntryFilter { DateFrom = today.AddHours(12), DateTo = today.AddHours(14) });

        Assert.Equal(1, total);
        Assert.Equal("In window", format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>Multiple filter criteria combine with AND semantics: a message must satisfy every stated criterion.</summary>
    [Fact]
    public async Task GetMessagesAsync_MultipleCriteria_CombineWithAnd()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "Match", [], DateTime.UtcNow, priority: TestMessagePriority.Level2, securityLevel: "RESTRICTED");
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "WrongPriority", [], DateTime.UtcNow, priority: TestMessagePriority.Normal, securityLevel: "RESTRICTED");
        await service.StoreIncomingMessage(Guid.NewGuid().ToString(), "S", "WrongLevel", [], DateTime.UtcNow, priority: TestMessagePriority.Level2, securityLevel: "PUBLIC");

        (List<MessageEntity> items, int total) = await service.GetMessages("root-inbox", 1, filter: new EntryFilter { Priority = TestMessagePriority.Level2, SecurityLevel = "RESTRICTED" });

        Assert.Equal(1, total);
        Assert.Equal("Match", format.GetBody(Assert.Single(items).Message));
    }

    /// <summary>A draft filter matches SecurityLevel, Priority and AlertOnly directly against the stored fields, the same way a message filter matches the decoded message.</summary>
    [Fact]
    public async Task GetDraftsAsync_FilterMatchesSecurityLevelPriorityAndAlert()
    {
        DraftEntity match = await service.CreateDraft();
        match.Body = "Match";
        match.Priority = "LEVEL2";
        match.SecurityLevel = "RESTRICTED";
        match.IsAlert = true;
        await service.SaveDraft(match);
        DraftEntity other = await service.CreateDraft();
        other.Body = "Other";
        await service.SaveDraft(other);

        (List<DraftEntity> items, int total) = await service.GetDrafts("root-drafts", 1, alphabetical: false,
            filter: new EntryFilter { Priority = TestMessagePriority.Level2, SecurityLevel = "RESTRICTED", AlertOnly = true });

        Assert.Equal(1, total);
        Assert.Equal("Match", Assert.Single(items).Body);
    }

    /// <summary>A note filter's DateFrom/DateTo bound the last-modified date inclusively, by date only.</summary>
    [Fact]
    public async Task GetNotesAsync_FilterByDateRange_IsInclusiveByDateOnly()
    {
        NoteEntity note = await service.CreateNote();
        note.Body = "Today";
        await service.SaveNote(note);

        (List<NoteEntity> items, int total) = await service.GetNotes("root-notes", 1, alphabetical: false,
            filter: new EntryFilter { DateFrom = DateTime.Now.Date.AddDays(1) });

        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    /// <summary>Verifies that StoreIncomingMessage fires the MessageInserted event after persisting.</summary>
    [Fact]
    public async Task StoreIncomingMessageAsync_FiresMessageInsertedEvent()
    {
        string? receivedBody = null;
        service.MessageInserted += entity =>
        {
            receivedBody = format.GetBody(entity.Message);
            return Task.CompletedTask;
        };

        await service.StoreIncomingMessage(
            Guid.NewGuid().ToString(), "S", "EventTest", [], DateTime.UtcNow);

        Assert.Equal("EventTest", receivedBody);
    }

    /// <summary>A self-addressed message creates an Inbox and an Outbox record sharing the same MessageId; delivery-status updates must only ever touch the Outbox record.</summary>
    [Fact]
    public async Task UpdateDeliveryStatus_SelfAddressedMessage_OnlyUpdatesOutboundRecord()
    {
        string messageId = Guid.NewGuid().ToString("N");
        await service.StoreIncomingMessage(messageId, "SELF", "Hello",
            [new AddressData { UserName = "SELF", Type = "To" }], DateTime.UtcNow);
        await service.StoreSentMessage(messageId, "Hello",
            [new AddressData { UserName = "SELF", Type = "To" }], DateTime.UtcNow,
            [new UserDeliveryResult { UserName = "SELF", Success = true, AddressedVia = [] }]);

        MessageEntity? updated = await service.UpdateDeliveryStatus(messageId, "SELF", DestinationStatus.Received);

        Assert.NotNull(updated);
        Assert.True(updated.IsOutbound);
        Assert.Equal(DestinationStatus.Received, Assert.Single(updated.DeliveryStatuses).Status);

        (List<MessageEntity> inboxItems, _) = await service.GetMessages("root-inbox", 1);
        MessageEntity inboxCopy = Assert.Single(inboxItems);
        Assert.False(inboxCopy.IsOutbound);
        Assert.Empty(inboxCopy.DeliveryStatuses);
    }

    /// <summary>A successful user result seeds the Outbox record with Sent status; Received only follows the destination's receive receipt.</summary>
    [Fact]
    public async Task StoreSentMessage_SuccessfulUserResult_SeedsSentStatusImmediately()
    {
        MessageEntity entity = await service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", [],
            DateTime.UtcNow, [new UserDeliveryResult { UserName = "SELF", Success = true, AddressedVia = [] }]);

        Assert.Equal(DestinationStatus.Sent, Assert.Single(entity.DeliveryStatuses).Status);
        Assert.True(entity.IsOutbound);
    }

    /// <summary>A failed user result seeds the Outbox record with Failed status immediately.</summary>
    [Fact]
    public async Task StoreSentMessage_FailedUserResult_SeedsFailedStatusImmediately()
    {
        MessageEntity entity = await service.StoreSentMessage(
            Guid.NewGuid().ToString("N"), "Subj", [],
            DateTime.UtcNow, [new UserDeliveryResult { UserName = "UNREACHABLE", Success = false, AddressedVia = [] }]);

        Assert.Equal(DestinationStatus.Failed, Assert.Single(entity.DeliveryStatuses).Status);
        Assert.True(entity.IsOutbound);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ctx.Dispose();
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, appName);
        if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); }
    }

    private async Task<string> StoreSentTo(string user, bool success)
    {
        string messageId = Guid.NewGuid().ToString("N");
        await service.StoreSentMessage(messageId, "Hello",
            [new AddressData { UserName = user, Type = "To" }], DateTime.UtcNow,
            [new UserDeliveryResult { UserName = user, Success = success, AddressedVia = [] }]);
        return messageId;
    }

    /// <summary>Status events for one send arrive in any order; a late "Sent" never moves an already Received status back.</summary>
    [Fact]
    public async Task UpdateDeliveryStatus_LateEarlierStatus_IsIgnored()
    {
        string messageId = await StoreSentTo("BOB", success: true);
        await service.UpdateDeliveryStatus(messageId, "BOB", DestinationStatus.Received);

        MessageEntity? updated = await service.UpdateDeliveryStatus(messageId, "BOB", DestinationStatus.Sent);

        Assert.Equal(DestinationStatus.Received, Assert.Single(updated!.DeliveryStatuses).Status);
        (List<MessageEntity> outbox, _) = await service.GetMessages("root-outbox", 1);
        Assert.Equal(DestinationStatus.Received, Assert.Single(Assert.Single(outbox).DeliveryStatuses).Status);
    }

    /// <summary>A status that arrives before the Outbox record is stored is kept and applied when the record is stored.</summary>
    [Fact]
    public async Task UpdateDeliveryStatus_BeforeStore_IsAppliedWhenStored()
    {
        string messageId = Guid.NewGuid().ToString("N");

        Assert.Null(await service.UpdateDeliveryStatus(messageId, "BOB", DestinationStatus.Received));
        MessageEntity entity = await service.StoreSentMessage(messageId, "Hello",
            [new AddressData { UserName = "BOB", Type = "To" }], DateTime.UtcNow,
            [new UserDeliveryResult { UserName = "BOB", Success = true, AddressedVia = [] }]);

        Assert.Equal(DestinationStatus.Received, Assert.Single(entity.DeliveryStatuses).Status);
    }

    /// <summary>A status that moves forward is applied, including a read receipt after a send that was reported failed.</summary>
    [Theory]
    [InlineData(true, DestinationStatus.Read)]
    [InlineData(false, DestinationStatus.Read)]
    public async Task UpdateDeliveryStatus_LaterStatus_IsApplied(bool success, DestinationStatus later)
    {
        string messageId = await StoreSentTo("BOB", success);

        MessageEntity? updated = await service.UpdateDeliveryStatus(messageId, "BOB", later);

        Assert.Equal(later, Assert.Single(updated!.DeliveryStatuses).Status);
    }

    /// <summary>Users are matched case-insensitively, as everywhere else in routing, so a receipt from "bob" updates "BOB".</summary>
    [Fact]
    public async Task UpdateDeliveryStatus_DifferentCase_UpdatesSameUser()
    {
        string messageId = await StoreSentTo("BOB", success: true);

        MessageEntity? updated = await service.UpdateDeliveryStatus(messageId, "bob", DestinationStatus.Read);

        DeliveryStatus status = Assert.Single(updated!.DeliveryStatuses);
        Assert.Equal("BOB", status.UserName);
        Assert.Equal(DestinationStatus.Read, status.Status);
    }

    /// <summary>DeleteFolderContents deletes every message, draft, and note in the folder and leaves other folders alone.</summary>
    [Fact]
    public async Task DeleteFolderContents_DeletesMessagesDraftsAndNotesOfThatFolderOnly()
    {
        await service.StoreIncomingMessage(Guid.NewGuid().ToString("N"), "A", "S", [], DateTime.UtcNow);
        await service.CreateDraft();
        await service.CreateNote();
        NoteEntity untouched = await service.CreateNote();
        untouched.FolderId = "somewhere-else";
        await service.SaveNote(untouched);

        await service.DeleteFolderContents("root-inbox");
        await service.DeleteFolderContents("root-drafts");
        await service.DeleteFolderContents("root-notes");

        Assert.Equal(0, (await service.GetMessages("root-inbox", 1)).Total);
        Assert.Empty((await service.GetDrafts("root-drafts", 1, false)).Items);
        Assert.Empty((await service.GetNotes("root-notes", 1, false)).Items);
        Assert.Equal(untouched.Id, Assert.Single((await service.GetNotes("somewhere-else", 1, false)).Items).Id);
    }
}
