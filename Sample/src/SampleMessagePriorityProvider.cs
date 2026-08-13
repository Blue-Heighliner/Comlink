namespace BlueHeighliner.Comlink.Sample;

/// <summary>Provides the selectable message priority levels for the Sample application.</summary>
public sealed class SampleMessagePriorityProvider : IMessagePriorityProvider
{
    private static readonly IReadOnlyList<MessagePriorityOption> Priorities =
    [
        new MessagePriorityOption { Name = "Low", Value = 0 },
        new MessagePriorityOption { Name = "Medium", Value = 1 },
        new MessagePriorityOption { Name = "High", Value = 2 }
    ];

    /// <inheritdoc />
    public IReadOnlyList<MessagePriorityOption> GetPriorities() => Priorities;
}
