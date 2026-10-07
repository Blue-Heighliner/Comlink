namespace BlueHeighliner.Comlink;

/// <summary>
/// A message a custom import format's reader (see <see cref="IImportsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Format{TFormat}"/>) has prepared
/// to send, added via <see cref="IImportFormatContext.AddStagedSend"/>. Shown to the user in the staged
/// send screen for review, and sent only once they press its final send button - never sent automatically.
/// </summary>
public sealed record StagedSendData
{
    /// <summary>Message body text.</summary>
    public required string Body { get; init; }
    /// <summary>Recipient addresses for this send.</summary>
    public required List<AddressRequest> Addresses { get; init; }
    /// <summary>Priority level this message will be sent at, a member of the enum the host stated for its priorities; <see langword="null"/> is the lowest level, and one that is not a configured level fails the send.</summary>
    public Enum? Priority { get; init; }
    /// <summary>Tag identifying the type of this message; see <see cref="IEngineController.GetTag"/>.</summary>
    public string Tag { get; init; } = string.Empty;
    /// <summary>Message level this message will be sent at, a member of the enum the host stated for its message levels, or <see langword="null"/> for none; one that is not a configured level fails the send.</summary>
    public Enum? MessageLevel { get; init; }
}
