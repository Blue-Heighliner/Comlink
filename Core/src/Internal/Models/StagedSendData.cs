namespace BlueHeighliner.Comlink;

/// <summary>A message an import format prepared to send, as the engine holds it, with the host's priority and message level as plain enum members.</summary>
internal sealed record StagedSendData
{
    /// <summary>Gets the message body text.</summary>
    public required string Body { get; init; }

    /// <summary>Gets the recipient addresses for this send.</summary>
    public required List<AddressRequest> Addresses { get; init; }

    /// <summary>Gets the priority this message will be sent at, or <see langword="null"/> for the lowest.</summary>
    public Enum? Priority { get; init; }

    /// <summary>Gets the tag identifying the type of this message.</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>Gets the message level this message will be sent at, or <see langword="null"/> for none.</summary>
    public Enum? MessageLevel { get; init; }
}
