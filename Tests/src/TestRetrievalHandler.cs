namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IRetrievalHandler{TFrame}"/> for <see cref="TestFrame"/>, recognizing frames with <see cref="TestFrame.IsRetrieval"/> set.</summary>
public sealed class TestRetrievalHandler : IRetrievalHandler<TestFrame>
{
    /// <inheritdoc />
    public string Priority { get; init; } = "NORMAL";

    /// <inheritdoc />
    public bool IsValid(TestFrame frame) => frame.IsRetrieval;

    /// <inheritdoc />
    public TestFrame Create(RetrievalCreateContext context)
        => new()
        {
            IsHidden = true,
            IsRetrieval = true,
            RetrievalFrom = context.From,
            RetrievalTo = context.To,
            RetrievalAuthors = [.. context.Authors],
            RetrievalDestinations = [.. context.Destinations],
            RetrievalIds = [.. context.Ids]
        };

    /// <inheritdoc />
    public DateTime? GetFrom(TestFrame frame) => frame.RetrievalFrom;

    /// <inheritdoc />
    public DateTime? GetTo(TestFrame frame) => frame.RetrievalTo;

    /// <inheritdoc />
    public IReadOnlyList<string> GetAuthors(TestFrame frame) => frame.RetrievalAuthors;

    /// <inheritdoc />
    public IReadOnlyList<string> GetDestinations(TestFrame frame) => frame.RetrievalDestinations;

    /// <inheritdoc />
    public IReadOnlyList<string> GetIds(TestFrame frame) => frame.RetrievalIds;
}
