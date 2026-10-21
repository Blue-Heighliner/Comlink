namespace BlueHeighliner.Comlink.Tests.Unit.Public.Utilities;

/// <summary>Unit tests for <see cref="BiDictionary{TLeft, TRight}"/>.</summary>
public sealed class BiDictionaryTests
{
    private static BiDictionary<string, int> Build() => new() { { "one", 1 }, { "two", 2 } };

    /// <summary>A pair is found from either side.</summary>
    [Fact]
    public void Pairs_AreFoundFromEitherSide()
    {
        BiDictionary<string, int> dictionary = Build();

        Assert.Equal(2, dictionary.GetRight("two"));
        Assert.Equal("one", dictionary.GetLeft(1));
        Assert.True(dictionary.TryGetRight("one", out int right));
        Assert.True(dictionary.TryGetLeft(2, out string? left));
        Assert.Equal((1, "two"), (right, left));
        Assert.False(dictionary.TryGetRight("three", out _));
        Assert.False(dictionary.TryGetLeft(3, out _));
        Assert.True(dictionary.ContainsLeft("one") && dictionary.ContainsRight(2));
        Assert.False(dictionary.ContainsLeft("three") || dictionary.ContainsRight(3));
        Assert.Equal(2, dictionary.Count);
        Assert.Equal(["one", "two"], dictionary.Lefts);
        Assert.Equal([1, 2], dictionary.Rights);
        Assert.Equal([new KeyValuePair<string, int>("one", 1), new KeyValuePair<string, int>("two", 2)], dictionary);
    }

    /// <summary>A value that is already in a pair, on either side, cannot be added again.</summary>
    [Fact]
    public void Add_RefusesAValueThatIsAlreadyPaired()
    {
        BiDictionary<string, int> dictionary = Build();

        Assert.Throws<ArgumentException>(() => dictionary.Add("one", 9));
        Assert.Throws<ArgumentException>(() => dictionary.Add("nine", 2));
        Assert.False(dictionary.TryAdd("one", 9));
        Assert.False(dictionary.TryAdd("nine", 2));
        Assert.True(dictionary.TryAdd("three", 3));
        Assert.Equal(3, dictionary.Count);
        Assert.Throws<KeyNotFoundException>(() => dictionary.GetRight("nine"));
        Assert.Throws<KeyNotFoundException>(() => dictionary.GetLeft(9));
    }

    /// <summary>Set replaces the pair either value was in, so both lookups stay in step.</summary>
    [Fact]
    public void Set_ReplacesThePairsEitherValueWasIn()
    {
        BiDictionary<string, int> dictionary = Build();

        dictionary.Set("one", 2);

        Assert.Equal(1, dictionary.Count);
        Assert.Equal(2, dictionary.GetRight("one"));
        Assert.Equal("one", dictionary.GetLeft(2));
        Assert.False(dictionary.ContainsRight(1));
        Assert.False(dictionary.ContainsLeft("two"));
    }

    /// <summary>Removing from either side removes the whole pair, and clearing removes all of them.</summary>
    [Fact]
    public void Remove_RemovesTheWholePair_AndClearRemovesAll()
    {
        BiDictionary<string, int> dictionary = Build();

        Assert.True(dictionary.RemoveLeft("one"));
        Assert.False(dictionary.RemoveLeft("one"));
        Assert.False(dictionary.ContainsRight(1));
        Assert.True(dictionary.RemoveRight(2));
        Assert.False(dictionary.RemoveRight(2));
        Assert.Equal(0, dictionary.Count);

        dictionary.Add("a", 1);
        dictionary.Clear();

        Assert.Equal(0, dictionary.Count);
        Assert.False(dictionary.ContainsRight(1));
    }

    /// <summary>The comparers decide when two values are the same one.</summary>
    [Fact]
    public void Comparers_DecideWhenValuesAreTheSame()
    {
        BiDictionary<string, string> dictionary = new(StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase) { { "Alice", "A" } };

        Assert.Equal("A", dictionary.GetRight("ALICE"));
        Assert.Equal("Alice", dictionary.GetLeft("a"));
        Assert.False(dictionary.TryAdd("alice", "B"));
        Assert.False(dictionary.TryAdd("Bob", "a"));
    }
}
