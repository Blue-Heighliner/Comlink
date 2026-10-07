namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Data.Entities;

/// <summary>Unit tests for how <see cref="DraftEntity"/> is stored.</summary>
public sealed class DraftEntityTests
{
    /// <summary>The message level and aspect are stored as their own fields, by the names of the properties.</summary>
    [Fact]
    public void MessageLevelAndAspect_AreStoredUnderTheirOwnNames()
    {
        LiteDB.BsonDocument document = LiteDB.BsonMapper.Global.ToDocument(new DraftEntity { MessageLevel = 2, MessageAspect = 1 });

        Assert.Equal(2, document["MessageLevel"].AsInt32);
        Assert.Equal(1, document["MessageAspect"].AsInt32);
        Assert.False(document.ContainsKey("SecurityLevel"));
    }
}
