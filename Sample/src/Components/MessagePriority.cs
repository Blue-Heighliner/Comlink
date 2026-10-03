namespace BlueHeighliner.Comlink.Sample;

/// <summary>The Sample's message priorities, lowest first: users choose among <see cref="Low"/>, <see cref="Medium"/> and <see cref="High"/>, while the handlers assign the other two.</summary>
public enum MessagePriority
{
    /// <summary>The lowest user priority.</summary>
    Low,

    /// <summary>The middle user priority.</summary>
    Medium,

    /// <summary>The system priority retrieval requests are sent with.</summary>
    Retrieval,

    /// <summary>The highest user priority.</summary>
    High,

    /// <summary>The system priority read and receive receipts are sent with.</summary>
    Receipt
}
