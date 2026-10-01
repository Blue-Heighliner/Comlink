namespace BlueHeighliner.Comlink.Services;

/// <summary>
/// A message a custom import format's reader (see <see cref="IEngineBuilder.ImportFormat(string, StagedSendMode, Nullable{TimeSpan}, Func{Stream, IImportFormatContext, CancellationToken, Task})"/>) has prepared
/// to send, added via <see cref="Control.IImportFormatContext.AddStagedSend"/>. Shown to the user in the staged
/// send screen for review, and sent only once they press its final send button - never sent automatically.
/// </summary>
public sealed record StagedSendData
{
    /// <summary>Message subject line.</summary>
    public required string Subject { get; init; }
    /// <summary>Message body text.</summary>
    public required string Body { get; init; }
    /// <summary>Recipient addresses for this send.</summary>
    public required List<AddressRequest> Addresses { get; init; }
    /// <summary>Whether this message will be sent as an alert.</summary>
    public bool IsAlert { get; init; }
    /// <summary>Priority number this message will be sent at; see <see cref="Control.IEngineController.GetPriority"/>.</summary>
    public int Priority { get; init; }
    /// <summary>Tag identifying the type of this message; see <see cref="Control.IEngineController.GetTag"/>.</summary>
    public string Tag { get; init; } = string.Empty;
    /// <summary>Security level name this message will be sent at, or an empty string for no security level.</summary>
    public string SecurityLevel { get; init; } = string.Empty;
}
