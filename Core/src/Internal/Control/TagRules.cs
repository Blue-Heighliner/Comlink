namespace BlueHeighliner.Comlink;

/// <summary>What the host's draft handler allows a message tag to be: its letter case, length, and whether it may hold symbols, numbers and spaces.</summary>
/// <param name="Case">Which letter case tags are kept in.</param>
/// <param name="MinLength">The fewest characters a tag that is given may have.</param>
/// <param name="MaxLength">The most characters a tag may have, or <see langword="null"/> for no maximum.</param>
/// <param name="AllowSymbols">Whether a tag may hold symbols and punctuation.</param>
/// <param name="AllowNumbers">Whether a tag may hold digits.</param>
/// <param name="AllowSpaces">Whether a tag may hold spaces.</param>
/// <param name="IsRequired">Whether a message must have a tag.</param>
internal sealed record TagRules(TagCase Case, int MinLength, int? MaxLength, bool AllowSymbols, bool AllowNumbers, bool AllowSpaces, bool IsRequired = false)
{
    /// <summary>Gets the rules when the host states none: any tag, of any length, in any case.</summary>
    public static TagRules Unrestricted { get; } = new(TagCase.Mixed, 0, null, true, true, true, false);

    /// <summary>Returns <paramref name="tag"/> as the rules make it: in the forced case, without the characters that are not allowed, and cut to the maximum length. What a tag box does to what is typed or pasted.</summary>
    /// <param name="tag">The tag as entered.</param>
    public string Filter(string tag)
    {
        string kept = new([.. (tag ?? string.Empty).Where(IsAllowed)]);
        string cased = Case switch { TagCase.Lower => kept.ToLowerInvariant(), TagCase.Upper => kept.ToUpperInvariant(), _ => kept };
        return MaxLength is { } max && cased.Length > max ? cased[..max] : cased;
    }

    /// <summary>Returns why <paramref name="tag"/> is not a tag these rules allow, or <see langword="null"/> when it is one.</summary>
    /// <param name="tag">The tag.</param>
    public string? Validate(string tag)
    {
        if (tag.Length == 0)
        {
            return IsRequired ? "A tag is required" : null;
        }
        if (tag.Length < MinLength)
        {
            return $"A tag must have at least {MinLength} characters";
        }
        if (MaxLength is { } max && tag.Length > max)
        {
            return $"A tag may have at most {max} characters";
        }
        if (!tag.All(IsAllowed))
        {
            return "A tag may not contain " + string.Join(", ", new[] { AllowSymbols ? null : "symbols", AllowNumbers ? null : "numbers", AllowSpaces ? null : "spaces" }.Where(kind => kind is not null));
        }

        return Case switch
        {
            TagCase.Lower when tag != tag.ToLowerInvariant() => "A tag must be in lowercase",
            TagCase.Upper when tag != tag.ToUpperInvariant() => "A tag must be in uppercase",
            _ => null
        };
    }

    private bool IsAllowed(char character)
        => char.IsLetter(character) || (char.IsDigit(character) ? AllowNumbers : char.IsWhiteSpace(character) ? AllowSpaces : AllowSymbols);
}
