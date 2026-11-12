namespace BlueHeighliner.Comlink.Tests.Unit.Public.Utilities;

/// <summary>Unit tests for <see cref="StringExtensions"/>.</summary>
public sealed class StringExtensionsTests
{
    /// <summary>Words of Pascal and camel case are separated, capital runs stay together and existing separators are not doubled.</summary>
    [Theory]
    [InlineData("TwoWords", "Two Words")]
    [InlineData("camelCaseText", "camel Case Text")]
    [InlineData("HTTPServer", "HTTP Server")]
    [InlineData("Level2Access", "Level2 Access")]
    [InlineData("ALLCAPS", "ALLCAPS")]
    [InlineData("Already Spaced", "Already Spaced")]
    [InlineData("snake_Case", "snake_Case")]
    [InlineData("Word", "Word")]
    [InlineData("", "")]
    public void AddSpacesToPascalCase_SeparatesWords(string text, string expected) => Assert.Equal(expected, text.AddSpacesToPascalCase());
}
