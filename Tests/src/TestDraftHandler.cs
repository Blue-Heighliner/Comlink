namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IDraftHandler{TPriority, TLevel}"/>: lines start 60 wide and may be 20 to 80, and a draft with a tag has the header <c>TAG: </c> and the tag.</summary>
public sealed class TestDraftHandler : IDraftHandler<TestMessagePriority, TestLevel>
{
    /// <inheritdoc />
    public int? DefaultLineWidth => 60;

    /// <inheritdoc />
    public int MinLineWidth => 20;

    /// <inheritdoc />
    public int? MaxLineWidth => 80;

    /// <inheritdoc />
    public string? GetHeader(DraftState<TestMessagePriority, TestLevel> state) => state.Tag.Length > 0 ? $"TAG: {state.Tag}" : null;
}

/// <summary>Test <see cref="IDraftHandler{TPriority, TLevel}"/> that only states what tags may be: uppercase, two to six characters, letters and numbers only, and what a new draft starts with.</summary>
public sealed class TestTagRulesDraftHandler : IDraftHandler<TestMessagePriority, TestLevel>
{
    /// <inheritdoc />
    public TagCase TagCase => TagCase.Upper;

    /// <inheritdoc />
    public bool IsTagRequired => true;

    /// <inheritdoc />
    public string? DefaultTag => "new tag!";

    /// <inheritdoc />
    public TestMessagePriority? DefaultPriority => TestMessagePriority.Level3;

    /// <inheritdoc />
    public TestLevel? DefaultSecurityLevel => TestLevel.Restricted;

    /// <inheritdoc />
    public int MinTagLength => 2;

    /// <inheritdoc />
    public int? MaxTagLength => 6;

    /// <inheritdoc />
    public bool AllowTagSymbols => false;

    /// <inheritdoc />
    public bool AllowTagSpaces => false;
}

/// <summary>Test <see cref="IDraftHandler{TPriority, TLevel}"/> that only turns tags off.</summary>
public sealed class TestNoTagsDraftHandler : IDraftHandler<TestMessagePriority, TestLevel>
{
    /// <inheritdoc />
    public bool EnableTags => false;
}

/// <summary>Test <see cref="IDraftHandler{TPriority, TLevel}"/> that only states a header, so the draft view offers no line width.</summary>
public sealed class TestHeaderOnlyDraftHandler : IDraftHandler<TestMessagePriority, TestLevel>
{
    /// <inheritdoc />
    public string? GetHeader(DraftState<TestMessagePriority, TestLevel> state) => "HEADER";
}

/// <summary>Test <see cref="IDraftHandler{TPriority, TLevel}"/> that only states a maximum, which is then also where a draft starts.</summary>
public sealed class TestMaxOnlyDraftHandler : IDraftHandler<TestMessagePriority, TestLevel>
{
    /// <inheritdoc />
    public int? MaxLineWidth => 40;
}
