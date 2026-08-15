namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>
/// Standard phonetic spelling alphabet and spelled-out digit lookup, used by the draft body editor's PLSO
/// (Phonetic Language Spell Out) mode to substitute a typed letter or digit with its phonetic word.
/// </summary>
public static class PhoneticAlphabet
{
    private static readonly Dictionary<char, string> Words = new()
    {
        ['A'] = "ALFA",
        ['B'] = "BRAVO",
        ['C'] = "CHARLIE",
        ['D'] = "DELTA",
        ['E'] = "ECHO",
        ['F'] = "FOXTROT",
        ['G'] = "GOLF",
        ['H'] = "HOTEL",
        ['I'] = "INDIA",
        ['J'] = "JULIETT",
        ['K'] = "KILO",
        ['L'] = "LIMA",
        ['M'] = "MIKE",
        ['N'] = "NOVEMBER",
        ['O'] = "OSCAR",
        ['P'] = "PAPA",
        ['Q'] = "QUEBEC",
        ['R'] = "ROMEO",
        ['S'] = "SIERRA",
        ['T'] = "TANGO",
        ['U'] = "UNIFORM",
        ['V'] = "VICTOR",
        ['W'] = "WHISKEY",
        ['X'] = "XRAY",
        ['Y'] = "YANKEE",
        ['Z'] = "ZULU",
        ['0'] = "ZERO",
        ['1'] = "ONE",
        ['2'] = "TWO",
        ['3'] = "THREE",
        ['4'] = "FOUR",
        ['5'] = "FIVE",
        ['6'] = "SIX",
        ['7'] = "SEVEN",
        ['8'] = "EIGHT",
        ['9'] = "NINE"
    };

    private static readonly HashSet<string> wordSet = new(Words.Values, StringComparer.OrdinalIgnoreCase);

    private static readonly List<int> wordLengths =
        Words.Values.Select(w => w.Length).Distinct().OrderByDescending(l => l).ToList();

    /// <summary>
    /// Attempts to look up the phonetic spell-out word for a single letter or digit character
    /// (case-insensitive). Returns <see langword="false"/> for any character with no phonetic word
    /// (punctuation, whitespace, etc.).
    /// </summary>
    /// <param name="character">The letter or digit to look up.</param>
    /// <param name="word">The uppercase phonetic word, or an empty string if not found.</param>
    public static bool TryGetWord(char character, out string word)
    {
        bool found = Words.TryGetValue(char.ToUpperInvariant(character), out string? value);
        word = value ?? string.Empty;
        return found;
    }

    /// <summary>Gets a value indicating whether <paramref name="candidate"/> is one of the phonetic words (case-insensitive).</summary>
    /// <param name="candidate">The text to test.</param>
    public static bool IsWord(string candidate) => wordSet.Contains(candidate);

    /// <summary>Gets the distinct lengths, in characters, of all phonetic words, ordered longest first.</summary>
    public static IReadOnlyList<int> Lengths => wordLengths;
}
