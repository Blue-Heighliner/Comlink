namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Frame"/> as a retrieval request when its <see cref="Frame.IsRetrieval"/> flag is set.</summary>
public sealed class RetrievalHandler : IRetrievalHandler<Frame>
{
    /// <inheritdoc />
    public Enum Priority => MessagePriority.Retrieval;

    /// <inheritdoc />
    public bool IsValid(Frame frame) => frame.IsRetrieval;

    /// <inheritdoc />
    public Frame Create(RetrievalCreateContext context)
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
    public DateTime? GetFrom(Frame frame) => frame.RetrievalFrom;

    /// <inheritdoc />
    public DateTime? GetTo(Frame frame) => frame.RetrievalTo;

    /// <inheritdoc />
    public IReadOnlyList<string> GetAuthors(Frame frame) => frame.RetrievalAuthors;

    /// <inheritdoc />
    public IReadOnlyList<string> GetDestinations(Frame frame) => frame.RetrievalDestinations;

    /// <inheritdoc />
    public IReadOnlyList<string> GetIds(Frame frame) => frame.RetrievalIds;
}
