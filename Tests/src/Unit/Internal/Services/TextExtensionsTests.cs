namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="TextExtensions"/>.</summary>
public sealed class TextExtensionsTests
{
    /// <summary>The first line is returned trimmed, and only the first.</summary>
    [Theory]
    [InlineData("Hello\nWorld", "Hello")]
    [InlineData("  Padded  \r\nNext", "Padded")]
    [InlineData("Single", "Single")]
    public void FirstLine_ReturnsTrimmedFirstLine(string text, string expected) => Assert.Equal(expected, text.FirstLine);

    /// <summary>Null, empty and blank-first-line text give an empty string.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\nSecond")]
    public void FirstLine_BlankOrNull_ReturnsEmpty(string? text) => Assert.Equal(string.Empty, text.FirstLine);
}
