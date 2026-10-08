namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="MessageMapping"/>.</summary>
public sealed class MessageMappingTests
{
    private readonly TestEngineController controller = new();

    private static Message MakeMessage() => new()
    {
        Id = "M1",
        FromUser = "ALICE",
        Body = "Hi",
        Addresses = [new MessageAddress { UserName = "BOB", Type = AddressType.Cc, Information = "info" }],
        SentAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        Priority = TestMessagePriority.Level2,
        Tag = "TAG",
        MessageLevel = TestLevel.Restricted,
        IsAlert = true
    };

    /// <summary>A message survives the trip to its stored form and back.</summary>
    [Fact]
    public void ToData_ThenToMessage_RoundTrips()
    {
        Message original = MakeMessage();

        Message back = controller.ToMessage(controller.ToData(original));

        Assert.Equal(original.Id, back.Id);
        Assert.Equal(original.Priority, back.Priority);
        Assert.Equal(original.MessageLevel, back.MessageLevel);
        Assert.Null(back.MessageAspect);
        Assert.Equal(original.Tag, back.Tag);
        Assert.True(back.IsAlert);
        Assert.Equal(("BOB", AddressType.Cc, "info"), (back.Addresses[0].UserName, back.Addresses[0].Type, back.Addresses[0].Information));
    }

    /// <summary>A message level that is not configured is refused, so it never reaches storage.</summary>
    [Fact]
    public void ToData_UnconfiguredLevel_Throws()
        => Assert.Throws<ArgumentException>(() => controller.ToData(MakeMessage() with { MessageLevel = DayOfWeek.Monday }));

    /// <summary>A level is named from its configuration, and a level or aspect that is none is empty.</summary>
    [Fact]
    public void Names_ComeFromTheConfiguration()
    {
        MessageData data = controller.ToData(MakeMessage());

        Assert.Equal("RESTRICTED", controller.NameOfLevel(data).ToUpperInvariant());
        Assert.Equal(string.Empty, controller.NameOfAspect(data));
        Assert.Equal(string.Empty, controller.NameOfLevel(new MessageData { Id = "X", FromUser = "A", Body = "" }));
    }
}
