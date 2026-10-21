namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="PhoneticAlphabet"/>.</summary>
public sealed class PhoneticAlphabetTests
{

    /// <summary>Lowercase letters resolve the same as uppercase.</summary>
    [Fact]
    public void TryGetWord_LowercaseLetter_IsCaseInsensitive()
    {
        bool found = PhoneticAlphabet.TryGetWord('g', out string word);

        Assert.True(found);
        Assert.Equal("GOLF", word);
    }

    /// <summary>Punctuation and whitespace have no phonetic word and return false with an empty result.</summary>
    [Theory]
    [InlineData(' ')]
    [InlineData('.')]
    [InlineData(',')]
    [InlineData('-')]
    [InlineData('\n')]
    public void TryGetWord_NonAlphanumeric_ReturnsFalse(char character)
    {
        bool found = PhoneticAlphabet.TryGetWord(character, out string word);

        Assert.False(found);
        Assert.Equal(string.Empty, word);
    }

    /// <summary>IsWord recognizes every phonetic word, case-insensitively.</summary>
    [Theory]
    [InlineData("GOLF")]
    [InlineData("golf")]
    [InlineData("NOVEMBER")]
    [InlineData("ONE")]
    public void IsWord_KnownWord_ReturnsTrue(string candidate)
    {
        Assert.True(PhoneticAlphabet.IsWord(candidate));
    }

    /// <summary>IsWord returns false for text that is not a phonetic word.</summary>
    [Theory]
    [InlineData("HELLO")]
    [InlineData("")]
    [InlineData("GOL")]
    public void IsWord_UnknownText_ReturnsFalse(string candidate)
    {
        Assert.False(PhoneticAlphabet.IsWord(candidate));
    }

    /// <summary>Lengths contains the shortest and longest word lengths in descending order.</summary>
    [Fact]
    public void Lengths_IsDescendingAndContainsExtremes()
    {
        IReadOnlyList<int> lengths = PhoneticAlphabet.Lengths;

        Assert.Equal(lengths.OrderByDescending(l => l), lengths);
        Assert.Contains(3, lengths); // ONE, SIX
        Assert.Contains(8, lengths); // NOVEMBER
    }
}
