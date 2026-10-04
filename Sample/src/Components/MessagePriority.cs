namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// The Sample's message priorities. The values are stored in drafts and exports and in every frame, so a member's value must never change or be reused, even if the member stops being used. The send order is not the order here:
/// it is the order <see cref="EngineConfiguration"/> states them in (lowest first), which is how a later addition can slot in anywhere without renumbering.
/// </summary>
public enum MessagePriority
{
    /// <summary>The lowest user priority.</summary>
    Low = 0,

    /// <summary>The middle user priority.</summary>
    Medium = 1,

    /// <summary>The highest user priority.</summary>
    High = 2,

    /// <summary>The system priority retrieval requests are sent with.</summary>
    Retrieval = 3,

    /// <summary>The system priority read and receive receipts are sent with.</summary>
    Receipt = 4
}
