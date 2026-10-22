namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Utilities;

/// <summary>Unit tests for <see cref="DataStore"/>, over real files in a temporary folder.</summary>
public sealed class DataStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"comlink-store-{Guid.NewGuid():N}");
    private readonly Mock<IEngineController> controller = new();

    /// <summary>Points the controller at a temporary data folder.</summary>
    public DataStoreTests() => controller.SetupGet(c => c.AppDataPath).Returns(directory);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private DataStore Build() => new(controller.Object);

    private sealed record Office(string City, List<string> Rooms);

    private sealed record Settings(string Name, int Size, Rank Rank, Office Home, Dictionary<string, int> Scores, DateOnly Since, string? Note = null);

    private sealed record Mismatch(bool Size);

    private enum Rank
    {
        Junior,
        Senior
    }

    private static Settings Sample(string name = "A") => new(name, 3, Rank.Senior, new Office("Oslo", ["a", "b"]), new Dictionary<string, int> { ["x"] = 1 }, new DateOnly(2024, 2, 29));

    /// <summary>A record saved comes back equal by value, with its lists, nested records, dictionaries, enums and dates intact, and enums are written by name.</summary>
    [Fact]
    public async Task SaveThenLoad_RoundTripsARecord()
    {
        DataStore store = Build();
        Settings saved = Sample();

        await store.Save("settings", saved);
        Settings? loaded = await store.Load<Settings>("settings");

        Assert.NotNull(loaded);
        Assert.Equal((saved.Name, saved.Size, saved.Rank, saved.Home.City, saved.Scores["x"], saved.Since, saved.Note), (loaded.Name, loaded.Size, loaded.Rank, loaded.Home.City, loaded.Scores["x"], loaded.Since, loaded.Note));
        Assert.Equal(saved.Home.Rooms, loaded.Home.Rooms);
        Assert.Contains("\"Senior\"", await File.ReadAllTextAsync(Directory.GetFiles(Path.Combine(directory, "Store"), "*.json").Single()));
    }

    /// <summary>A record type can change between runs: a stored property the type no longer has is ignored, and one the file does not have keeps its default.</summary>
    [Fact]
    public async Task Load_AsAChangedType_IgnoresExtraAndKeepsDefaultsForMissing()
    {
        DataStore store = Build();
        await store.Save("settings", Sample());

        Office? changed = await store.Load<Office>("settings");

        Assert.NotNull(changed);
        Assert.Null(changed.City);
    }

    /// <summary>Nothing stored under an identifier loads as null, even before the store folder exists.</summary>
    [Fact]
    public async Task Load_NothingStored_IsNull() => Assert.Null(await Build().Load<Settings>("missing"));

    /// <summary>Saving again under the same identifier replaces the record, leaving one file and no temporary files behind, and other identifiers are independent.</summary>
    [Fact]
    public async Task Save_AgainOverwrites_AndIdentifiersAreIndependent()
    {
        DataStore store = Build();

        await store.Save("settings", Sample("first"));
        await store.Save("other", Sample("other"));
        await store.Save("settings", Sample("second"));

        Assert.Equal("second", (await store.Load<Settings>("settings"))!.Name);
        Assert.Equal("other", (await store.Load<Settings>("other"))!.Name);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(directory, "Store")).Length);
        Assert.Empty(Directory.GetFiles(Path.Combine(directory, "Store"), "*.tmp"));
    }

    /// <summary>Case does not matter in an identifier, so both spellings are one record.</summary>
    [Fact]
    public async Task Identifiers_AreCaseInsensitive()
    {
        DataStore store = Build();

        await store.Save("Settings", Sample("A"));
        await store.Save("SETTINGS", Sample("B"));

        Assert.Equal("B", (await store.Load<Settings>("settings"))!.Name);
        Assert.Single(Directory.GetFiles(Path.Combine(directory, "Store")));
    }

    /// <summary>Any text is an identifier: one with path characters or dots stays inside the store folder, and different texts never share a file.</summary>
    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("a/b")]
    [InlineData("C:\\temp\\x")]
    [InlineData("..")]
    [InlineData("con")]
    [InlineData("name with spaces")]
    [InlineData("ünïcode")]
    public async Task Identifiers_WithPathCharacters_StayInsideTheStore(string id)
    {
        DataStore store = Build();

        await store.Save(id, Sample("x"));

        Assert.Equal("x", (await store.Load<Settings>(id))!.Name);
        string file = Assert.Single(Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories));
        Assert.Equal(Path.Combine(directory, "Store"), Path.GetDirectoryName(file));
    }

    /// <summary>Identifiers that look alike but differ do not collide.</summary>
    [Fact]
    public async Task DifferentIdentifiers_NeverShareAFile()
    {
        DataStore store = Build();

        await store.Save("a/b", Sample("slash"));
        await store.Save("a%2Fb", Sample("percent"));
        await store.Save("a_b", Sample("underscore"));

        Assert.Equal(["slash", "percent", "underscore"], [(await store.Load<Settings>("a/b"))!.Name, (await store.Load<Settings>("a%2Fb"))!.Name, (await store.Load<Settings>("a_b"))!.Name]);
        Assert.Equal(3, Directory.GetFiles(Path.Combine(directory, "Store")).Length);
    }

    /// <summary>An empty or blank identifier is refused by both methods, and a null record is refused.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyIdentifier_IsRefused(string id)
    {
        DataStore store = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => store.Save(id, Sample()));
        await Assert.ThrowsAsync<ArgumentException>(() => store.Load<Settings>(id));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.Save<Settings>("x", null!));
    }

    /// <summary>A stored record with a value of the wrong kind for the requested type, or that is not JSON, fails the load and is left as it was, so it is not lost to a later save by mistake.</summary>
    [Fact]
    public async Task Load_UnreadableRecord_ThrowsAndKeepsTheFile()
    {
        DataStore store = Build();
        await store.Save("settings", Sample());
        string file = Directory.GetFiles(Path.Combine(directory, "Store")).Single();
        string before = await File.ReadAllTextAsync(file);

        InvalidDataException wrongType = await Assert.ThrowsAsync<InvalidDataException>(() => store.Load<Mismatch>("settings"));
        Assert.Equal(before, await File.ReadAllTextAsync(file));
        await File.WriteAllTextAsync(file, "{ broken");
        InvalidDataException broken = await Assert.ThrowsAsync<InvalidDataException>(() => store.Load<Settings>("settings"));

        Assert.Contains("settings", wrongType.Message);
        Assert.Contains("Settings", broken.Message);
        Assert.Equal("{ broken", await File.ReadAllTextAsync(file));
    }

    /// <summary>A cancelled save leaves the earlier record untouched and no temporary file behind.</summary>
    [Fact]
    public async Task Save_Cancelled_KeepsTheOldRecord()
    {
        DataStore store = Build();
        await store.Save("settings", Sample("old"));
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Save("settings", Sample("new"), cancelled.Token));

        Assert.Equal("old", (await store.Load<Settings>("settings"))!.Name);
        Assert.Empty(Directory.GetFiles(Path.Combine(directory, "Store"), "*.tmp"));
    }

    /// <summary>Saves made at the same time never corrupt a record: what is loaded afterwards is one of them whole, and a load during the saves always gets a whole record.</summary>
    [Fact]
    public async Task ConcurrentSavesAndLoads_NeverSeeAHalfWrittenRecord()
    {
        DataStore store = Build();
        await store.Save("settings", Sample("0"));

        Task[] saves = [.. Enumerable.Range(1, 30).Select(i => Task.Run(() => store.Save("settings", Sample(i.ToString(System.Globalization.CultureInfo.InvariantCulture)))))];
        Task[] loads = [.. Enumerable.Range(0, 30).Select(_ => Task.Run(async () => Assert.NotNull(await store.Load<Settings>("settings"))))];
        await Task.WhenAll([.. saves, .. loads]);

        Assert.Contains((await store.Load<Settings>("settings"))!.Name, Enumerable.Range(1, 30).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Empty(Directory.GetFiles(Path.Combine(directory, "Store"), "*.tmp"));
    }

    /// <summary>Records are kept in the current user's data folder, so when the user changes each sees only their own.</summary>
    [Fact]
    public async Task Records_FollowTheCurrentUsersFolder()
    {
        string alice = Path.Combine(directory, "ALICE");
        string bob = Path.Combine(directory, "BOB");
        string current = alice;
        controller.SetupGet(c => c.AppDataPath).Returns(() => current);
        DataStore store = Build();

        await store.Save("settings", Sample("alice's"));
        current = bob;
        Assert.Null(await store.Load<Settings>("settings"));
        await store.Save("settings", Sample("bob's"));
        current = alice;

        Assert.Equal("alice's", (await store.Load<Settings>("settings"))!.Name);
        Assert.True(File.Exists(Directory.GetFiles(Path.Combine(bob, "Store")).Single()));
    }
}
