namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="TagRules"/>: what a tag may be, and what a tag box makes of what is typed.</summary>
public sealed class TagRulesTests
{
    /// <summary>Without rules every tag is kept as written and is valid.</summary>
    [Fact]
    public void Unrestricted_KeepsAndAcceptsAnything()
    {
        Assert.Equal("Mixed 1 #tag!", TagRules.Unrestricted.Filter("Mixed 1 #tag!"));
        Assert.Null(TagRules.Unrestricted.Validate("Mixed 1 #tag!"));
        Assert.Null(TagRules.Unrestricted.Validate(""));
    }

    /// <summary>A forced case is applied as the tag is entered, and mixed leaves it.</summary>
    [Fact]
    public void Filter_ForcesTheCase()
    {
        Assert.Equal("ABC DEF", TagRules.Unrestricted with { Case = TagCase.Upper } is { } upper ? upper.Filter("abc Def") : null);
        Assert.Equal("abc def", (TagRules.Unrestricted with { Case = TagCase.Lower }).Filter("ABC Def"));
        Assert.Equal("aBc", TagRules.Unrestricted.Filter("aBc"));
    }

    /// <summary>Symbols, numbers and spaces that are not allowed are removed, and letters are always kept.</summary>
    [Fact]
    public void Filter_RemovesWhatIsNotAllowed()
    {
        Assert.Equal("ab1 c", (TagRules.Unrestricted with { AllowSymbols = false }).Filter("a#b1 c!"));
        Assert.Equal("ab #c", (TagRules.Unrestricted with { AllowNumbers = false }).Filter("a1b #c2"));
        Assert.Equal("ab1#c", (TagRules.Unrestricted with { AllowSpaces = false }).Filter("a b1 #c"));
        Assert.Equal("abc", (TagRules.Unrestricted with { AllowSymbols = false, AllowNumbers = false, AllowSpaces = false }).Filter("a 1b#c"));
    }

    /// <summary>A tag is cut to the maximum length, after what is not allowed is removed.</summary>
    [Fact]
    public void Filter_CutsToTheMaximum()
        => Assert.Equal("abcd", (TagRules.Unrestricted with { MaxLength = 4, AllowSpaces = false }).Filter("ab cdefgh"));

    /// <summary>A tag that is too short, too long, has what is not allowed, or is in the wrong case is refused, with the reason.</summary>
    [Fact]
    public void Validate_ReportsWhyATagIsNotAllowed()
    {
        TagRules rules = new(TagCase.Upper, 2, 5, false, true, false, true);

        Assert.Null(rules.Validate("AB12"));
        Assert.Contains("required", rules.Validate("") ?? "");
        Assert.Contains("at least 2", rules.Validate("A") ?? "");
        Assert.Contains("at most 5", rules.Validate("ABCDEF") ?? "");
        Assert.Contains("symbols, spaces", rules.Validate("A B#") ?? "");
        Assert.Contains("uppercase", rules.Validate("ab") ?? "");
        Assert.Contains("lowercase", (rules with { Case = TagCase.Lower }).Validate("AB") ?? "");
    }

    /// <summary>An empty tag is fine unless a tag is required, and the minimum length only applies to a tag that is given.</summary>
    [Fact]
    public void Validate_EmptyTagIsOnlyAnErrorWhenRequired()
    {
        TagRules optional = new(TagCase.Mixed, 3, null, true, true, true, false);

        Assert.Null(optional.Validate(""));
        Assert.Contains("at least 3", optional.Validate("ab") ?? "");
        Assert.Contains("required", (optional with { IsRequired = true }).Validate("") ?? "");
    }
}
