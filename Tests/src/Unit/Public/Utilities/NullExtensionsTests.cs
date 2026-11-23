namespace BlueHeighliner.Comlink.Tests.Unit.Public.Utilities;

/// <summary>Unit tests for <see cref="NullExtensions"/>.</summary>
public sealed class NullExtensionsTests
{
    /// <summary>A value that is there comes back as it is, whether a reference or a struct.</summary>
    [Fact]
    public void NotNull_ReturnsTheValue()
    {
        string? text = "x";
        int? number = 5;

        Assert.Equal("x", text.NotNull());
        Assert.Equal(5, number.NotNull());
    }

    /// <summary>Null throws with the message given, or one naming the type.</summary>
    [Fact]
    public void NotNull_Null_Throws()
    {
        string? text = null;

        Assert.Equal("missing", Assert.Throws<InvalidOperationException>(() => text.NotNull("missing")).Message);
        Assert.Contains("String", Assert.Throws<InvalidOperationException>(() => text.NotNull()).Message);
    }
}
