namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="SampleFrame"/> as a retrieval request when its <see cref="SampleFrame.IsRetrieval"/> flag is set.</summary>
public sealed class SampleRetrievalHandler : IRetrievalHandler<SampleFrame>
{
    /// <inheritdoc />
    public string Priority => SamplePriorities.Retrieval;

    /// <inheritdoc />
    public bool IsValid(SampleFrame frame) => frame.IsRetrieval;

    /// <inheritdoc />
    public SampleFrame Create(RetrievalCreateContext context)
        => new()
        {
            IsRetrieval = true,
            RetrievalFrom = context.From,
            RetrievalTo = context.To,
            RetrievalAuthors = [.. context.Authors],
            RetrievalDestinations = [.. context.Destinations],
            RetrievalIds = [.. context.Ids]
        };

    /// <inheritdoc />
    public DateTime? GetFrom(SampleFrame frame) => frame.RetrievalFrom;

    /// <inheritdoc />
    public DateTime? GetTo(SampleFrame frame) => frame.RetrievalTo;

    /// <inheritdoc />
    public IReadOnlyList<string> GetAuthors(SampleFrame frame) => frame.RetrievalAuthors;

    /// <inheritdoc />
    public IReadOnlyList<string> GetDestinations(SampleFrame frame) => frame.RetrievalDestinations;

    /// <inheritdoc />
    public IReadOnlyList<string> GetIds(SampleFrame frame) => frame.RetrievalIds;
}
