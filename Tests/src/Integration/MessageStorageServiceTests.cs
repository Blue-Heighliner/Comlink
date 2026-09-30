namespace BlueHeighliner.Comlink.Tests.Integration;

/// <summary>Integration tests for <see cref="MessageStorageService"/> against a real LiteDB database.</summary>
public sealed class MessageStorageServiceTests : IDisposable
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    /// <summary>Initializes a fresh database and a service running as the storage server <c>SERVER</c>.</summary>
    public MessageStorageServiceTests()
    {
        ctx = new LiteDbContext(new TestAppDataPathProvider(appName));
        ctx.Initialize();
        controller.Setup(c => c.StorageServers).Returns(["SERVER"]);
        currentUser.SetupGet(p => p.UserName).Returns("SERVER");
        service = new MessageStorageService(new StoredMessageRepository(ctx), controller.Object, currentUser.Object, noLogger);
    }

    private readonly string appName = Guid.NewGuid().ToString();
    private readonly LiteDbContext ctx;
    private readonly Mock<TestEngineController> controller = new() { CallBase = true };
    private readonly Mock<ICurrentUserProvider> currentUser = new();
    private readonly MessageStorageService service;

    /// <inheritdoc />
    public void Dispose()
    {
        ctx.Dispose();
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); }
    }

    private static TestMessage Message(string id, string from, DateTime sentAt, params string[] to) => new()
    {
        MessageId = id,
        FromUser = from,
        Subject = $"Subject {id}",
        Body = $"Body {id}",
        SentAt = sentAt,
        IsAlert = true,
        Priority = 2,
        Tag = "TAG",
        Addresses = [.. to.Select(u => new TestAddressEntry { UserName = u, Type = "To" })]
    };

    private static TestMessage Request(RetrievalCriteria criteria)
    {
        TestMessage request = new() { MessageId = "REQ" };
        new TestEngineController().SetRetrieval(request, criteria);
        return request;
    }

    private static readonly DateTime day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime day2 = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime day3 = new(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc);

    private async Task<List<string>> Ids(string requester, RetrievalCriteria criteria)
        => [.. (await service.Find(requester, Request(criteria))).Select(m => ((TestMessage)m).MessageId)];

    /// <summary>Only a user named in StorageServers keeps messages.</summary>
    [Fact]
    public async Task Store_NotAStorageServer_KeepsNothing()
    {
        currentUser.SetupGet(p => p.UserName).Returns("OTHER");

        await service.Store(Message("M1", "ALICE", day1, "BOB"));

        Assert.False(service.IsEnabled);
        Assert.Empty(ctx.StoredMessages.FindAll());
    }

    /// <summary>A confirmation or a retrieval request is not user content and is never kept.</summary>
    [Fact]
    public async Task Store_ConfirmationOrRetrievalRequest_IsNotKept()
    {
        await service.Store(new TestMessage { MessageId = "C1", ConfirmationMessageId = "M1" });
        await service.Store(Request(new RetrievalCriteria()));

        Assert.Empty(ctx.StoredMessages.FindAll());
    }

    /// <summary>Storing the same message twice keeps one copy.</summary>
    [Fact]
    public async Task Store_SameMessageTwice_KeepsOneCopy()
    {
        await service.Store(Message("M1", "ALICE", day1, "BOB"));
        await service.Store(Message("M1", "ALICE", day1, "BOB"));

        Assert.Single(ctx.StoredMessages.FindAll());
    }

    /// <summary>A retrieval is not limited to the requester's own traffic: it finds every stored message that fits, whoever sent or received it.</summary>
    [Fact]
    public async Task Find_ReturnsMessagesRegardlessOfWhoSentOrReceivedThem()
    {
        await service.Store(Message("SENT", "CAROL", day1, "BOB"));
        await service.Store(Message("DIRECT", "ALICE", day1, "CAROL"));
        await service.Store(Message("OTHERS", "ALICE", day1, "BOB"));

        Assert.Equal(["DIRECT", "OTHERS", "SENT"], (await Ids("ERIN", new RetrievalCriteria())).Order());
    }

    /// <summary>The date range bounds the original sent time, inclusively.</summary>
    [Fact]
    public async Task Find_FiltersByDateRangeInclusively()
    {
        await service.Store(Message("D1", "ALICE", day1, "CAROL"));
        await service.Store(Message("D2", "ALICE", day2, "CAROL"));
        await service.Store(Message("D3", "ALICE", day3, "CAROL"));

        Assert.Equal(["D2", "D3"], await Ids("CAROL", new RetrievalCriteria { From = day2 }));
        Assert.Equal(["D1", "D2"], await Ids("CAROL", new RetrievalCriteria { To = day2 }));
        Assert.Equal(["D2"], await Ids("CAROL", new RetrievalCriteria { From = day2, To = day2 }));
    }

    /// <summary>Authors, destinations and IDs each match by exact name, case-insensitively, and combine with AND.</summary>
    [Fact]
    public async Task Find_FiltersByAuthorDestinationAndIdCaseInsensitively()
    {
        await service.Store(Message("A1", "ALICE", day1, "CAROL", "BOB"));
        await service.Store(Message("A2", "ERIN", day1, "CAROL"));

        Assert.Equal(["A1"], await Ids("CAROL", new RetrievalCriteria { Authors = ["alice"] }));
        Assert.Equal(["A1"], await Ids("CAROL", new RetrievalCriteria { Destinations = ["bob"] }));
        Assert.Equal(["A2"], await Ids("CAROL", new RetrievalCriteria { Ids = ["a2"] }));
        Assert.Empty(await Ids("CAROL", new RetrievalCriteria { Authors = ["ALICE"], Ids = ["A2"] }));
    }

    /// <summary>A found copy keeps the original's identity and content, is never an alert, and is addressed only to the requester.</summary>
    [Fact]
    public async Task Find_CopyKeepsContentClearsAlertAndReaddressesToRequester()
    {
        await service.Store(Message("M1", "ALICE", day1, "CAROL", "BOB"));

        TestMessage copy = (TestMessage)Assert.Single(await service.Find("CAROL", Request(new RetrievalCriteria())));

        Assert.Equal(("M1", "ALICE", "Subject M1", "Body M1"), (copy.MessageId, copy.FromUser, copy.Subject, copy.Body));
        Assert.Equal(day1, copy.SentAt);
        Assert.Equal((2, "TAG"), (copy.Priority, copy.Tag));
        Assert.False(copy.IsAlert);
        TestAddressEntry address = Assert.Single(copy.Addresses);
        Assert.Equal("CAROL", address.UserName);
        Assert.False(copy.IsRetrieval);
    }

    /// <summary>Copies come back oldest first.</summary>
    [Fact]
    public async Task Find_OrdersBySentTime()
    {
        await service.Store(Message("LATE", "ALICE", day3, "CAROL"));
        await service.Store(Message("EARLY", "ALICE", day1, "CAROL"));

        Assert.Equal(["EARLY", "LATE"], await Ids("CAROL", new RetrievalCriteria()));
    }

    /// <summary>A server that is not a storage server finds nothing.</summary>
    [Fact]
    public async Task Find_NotEnabled_ReturnsNothing()
    {
        await service.Store(Message("M1", "ALICE", day1, "CAROL"));

        currentUser.SetupGet(p => p.UserName).Returns("OTHER");
        Assert.Empty(await service.Find("CAROL", Request(new RetrievalCriteria())));
    }
}
