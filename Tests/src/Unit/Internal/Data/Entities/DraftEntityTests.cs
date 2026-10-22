namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Data.Entities;

/// <summary>Unit tests for how <see cref="DraftEntity"/> is stored.</summary>
public sealed class DraftEntityTests
{
    /// <summary>The level and aspect are stored as their own fields, under their own names.</summary>
    [Fact]
    public void LevelAndAspect_AreStoredUnderTheirOwnNames()
    {
        LiteDB.BsonDocument document = LiteDB.BsonMapper.Global.ToDocument(new DraftEntity { Level = 2, Aspect = 1 });

        Assert.Equal(2, document["Level"].AsInt32);
        Assert.Equal(1, document["Aspect"].AsInt32);
        Assert.False(document.ContainsKey("MessageLevel"));
        Assert.False(document.ContainsKey("MessageAspect"));
    }
}
