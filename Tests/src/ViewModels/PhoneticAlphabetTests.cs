namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="PhoneticAlphabet"/>.</summary>
public sealed class PhoneticAlphabetTests
{
    /// <summary>Every letter A-Z maps to its correct phonetic word.</summary>
    [Theory]
    [InlineData('A', "ALFA")]
    [InlineData('B', "BRAVO")]
    [InlineData('C', "CHARLIE")]
    [InlineData('D', "DELTA")]
    [InlineData('E', "ECHO")]
    [InlineData('F', "FOXTROT")]
    [InlineData('G', "GOLF")]
    [InlineData('H', "HOTEL")]
    [InlineData('I', "INDIA")]
    [InlineData('J', "JULIETT")]
    [InlineData('K', "KILO")]
    [InlineData('L', "LIMA")]
    [InlineData('M', "MIKE")]
    [InlineData('N', "NOVEMBER")]
    [InlineData('O', "OSCAR")]
    [InlineData('P', "PAPA")]
    [InlineData('Q', "QUEBEC")]
    [InlineData('R', "ROMEO")]
    [InlineData('S', "SIERRA")]
    [InlineData('T', "TANGO")]
    [InlineData('U', "UNIFORM")]
    [InlineData('V', "VICTOR")]
    [InlineData('W', "WHISKEY")]
    [InlineData('X', "XRAY")]
    [InlineData('Y', "YANKEE")]
    [InlineData('Z', "ZULU")]
    public void TryGetWord_Letter_ReturnsPhoneticWord(char letter, string expected)
    {
        bool found = PhoneticAlphabet.TryGetWord(letter, out string word);

        Assert.True(found);
        Assert.Equal(expected, word);
    }

    /// <summary>Every digit 0-9 maps to its correct spelled-out word.</summary>
    [Theory]
    [InlineData('0', "ZERO")]
    [InlineData('1', "ONE")]
    [InlineData('2', "TWO")]
    [InlineData('3', "THREE")]
    [InlineData('4', "FOUR")]
    [InlineData('5', "FIVE")]
    [InlineData('6', "SIX")]
    [InlineData('7', "SEVEN")]
    [InlineData('8', "EIGHT")]
    [InlineData('9', "NINE")]
    public void TryGetWord_Digit_ReturnsSpelledOutNumber(char digit, string expected)
    {
        bool found = PhoneticAlphabet.TryGetWord(digit, out string word);

        Assert.True(found);
        Assert.Equal(expected, word);
    }

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
