namespace BlueHeighliner.Comlink.Tests.Integration;

/// <summary>Integration tests for <see cref="MessageStorageService"/> against a real LiteDB database.</summary>
public sealed class MessageStorageServiceTests : IDisposable
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    /// <summary>Initializes a fresh database and a service over it.</summary>
    public MessageStorageServiceTests()
    {
        ctx = new LiteDbContext(new TestAppDataPathProvider(appName));
        ctx.Initialize();
        service = new MessageStorageService(new StoredMessageRepository(ctx), new TestEngineController(), noLogger);
    }

    private readonly string appName = Guid.NewGuid().ToString();
    private readonly LiteDbContext ctx;
    private readonly MessageStorageService service;

    /// <inheritdoc />
    public void Dispose()
    {
        ctx.Dispose();
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static Message Message(string id, string from, DateTime sentAt, params string[] to) => new()
    {
        Id = id,
        FromUser = from,
        Body = $"Body {id}",
        SentAt = sentAt,
        IsAlert = true,
        Priority = TestMessagePriority.Level2,
        Tag = "TAG",
        MessageLevel = TestLevel.Restricted,
        Addresses = [.. to.Select(user => new MessageAddress { UserName = user, Type = AddressType.To })]
    };

    private static readonly DateTime day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime day2 = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime day3 = new(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc);

    private async Task<List<string>> Ids(RetrievalCriteria criteria) => [.. (await service.Find(criteria)).Select(message => message.Id)];

    /// <summary>Storing the same message twice keeps one copy.</summary>
    [Fact]
    public async Task Store_SameMessageTwice_KeepsOneCopy()
    {
        await service.Store(Message("M1", "ALICE", day1, "BOB"));
        await service.Store(Message("M1", "ALICE", day1, "BOB"));

        Assert.Single(ctx.StoredMessages.FindAll());
    }

    /// <summary>A retrieval is not limited to anyone's own traffic: it finds every stored message that fits, whoever sent or received it.</summary>
    [Fact]
    public async Task Find_ReturnsMessagesRegardlessOfWhoSentOrReceivedThem()
    {
        await service.Store(Message("SENT", "CAROL", day1, "BOB"));
        await service.Store(Message("DIRECT", "ALICE", day1, "CAROL"));
        await service.Store(Message("OTHERS", "ALICE", day1, "BOB"));

        Assert.Equal(["DIRECT", "OTHERS", "SENT"], (await Ids(new RetrievalCriteria())).Order());
    }

    /// <summary>The date range bounds the original sent time, inclusively, and what is found is ordered by it.</summary>
    [Fact]
    public async Task Find_FiltersByDateRangeInclusively()
    {
        await service.Store(Message("D3", "ALICE", day3, "CAROL"));
        await service.Store(Message("D1", "ALICE", day1, "CAROL"));
        await service.Store(Message("D2", "ALICE", day2, "CAROL"));

        Assert.Equal(["D2", "D3"], await Ids(new RetrievalCriteria { From = day2 }));
        Assert.Equal(["D1", "D2"], await Ids(new RetrievalCriteria { To = day2 }));
        Assert.Equal(["D2"], await Ids(new RetrievalCriteria { From = day2, To = day2 }));
    }

    /// <summary>Authors, destinations and IDs each match by exact name, case-insensitively, and combine with AND.</summary>
    [Fact]
    public async Task Find_FiltersByAuthorDestinationAndIdCaseInsensitively()
    {
        await service.Store(Message("A1", "ALICE", day1, "CAROL", "BOB"));
        await service.Store(Message("A2", "ERIN", day1, "CAROL"));

        Assert.Equal(["A1"], await Ids(new RetrievalCriteria { Authors = ["alice"] }));
        Assert.Equal(["A1"], await Ids(new RetrievalCriteria { Destinations = ["bob"] }));
        Assert.Equal(["A2"], await Ids(new RetrievalCriteria { Ids = ["a2"] }));
        Assert.Empty(await Ids(new RetrievalCriteria { Authors = ["ALICE"], Ids = ["A2"] }));
    }

    /// <summary>A found message is the message that was stored, with its content, priority, level and addresses.</summary>
    [Fact]
    public async Task Find_ReturnsTheStoredMessage()
    {
        await service.Store(Message("M1", "ALICE", day1, "CAROL", "BOB"));

        Message found = Assert.Single(await service.Find(new RetrievalCriteria()));

        Assert.Equal(("M1", "ALICE", "Body M1"), (found.Id, found.FromUser, found.Body));
        Assert.Equal(day1, found.SentAt.ToUniversalTime());
        Assert.Equal((TestMessagePriority.Level2, "TAG", TestLevel.Restricted), (found.Priority, found.Tag, found.MessageLevel));
        Assert.Equal(["CAROL", "BOB"], found.Addresses.Select(address => address.UserName));
    }
}
