namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the frames of the host's frame type <typeparamref name="TFrame"/> that are retrieval requests: what a user sends a server
/// to ask for stored messages. A request is not a message and is never shown to a user. See <see cref="IFrameBuilder{TFrame}.Retrieval{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IRetrievalHandler<TFrame> where TFrame : class
{
    /// <summary>Gets the member of the priority enum (see <see cref="IEngineBuilder.Priorities{TPriority}"/>) naming the level that retrieval requests are sent with, which is how they are ordered against other traffic.</summary>
    Enum Priority { get; }

    /// <summary>Returns whether <paramref name="frame"/> is a retrieval request.</summary>
    /// <param name="frame">The frame to classify.</param>
    bool IsValid(TFrame frame);

    /// <summary>Creates a new retrieval request frame asking for <paramref name="context"/>, for which <see cref="IsValid"/> returns <see langword="true"/>. The engine sets the identifier, sender, addresses and sent time itself.</summary>
    /// <param name="context">The criteria of the request.</param>
    /// <returns>The new frame.</returns>
    TFrame Create(RetrievalCreateContext context);

    /// <summary>Gets the user name of the sender of <paramref name="frame"/>.</summary>
    string GetSender(TFrame frame);

    /// <summary>Sets the user name of the sender of <paramref name="frame"/>, which the engine does for every retrieval request it sends.</summary>
    /// <param name="frame">The retrieval request.</param>
    /// <param name="sender">The sender's user name.</param>
    void SetSender(TFrame frame, string sender);

    /// <summary>Gets the user name <paramref name="frame"/> is for: where the engine sends this retrieval request, and where a server or relay hands it on to. The engine states it when it creates the frame (see <see cref="RetrievalCreateContext.Server"/>).</summary>
    string GetDestination(TFrame frame);

    /// <summary>Gets the earliest sent time <paramref name="frame"/> asks for, or <see langword="null"/> for no lower bound.</summary>
    DateTime? GetFrom(TFrame frame);

    /// <summary>Gets the latest sent time <paramref name="frame"/> asks for, or <see langword="null"/> for no upper bound.</summary>
    DateTime? GetTo(TFrame frame);

    /// <summary>Gets the sender user names <paramref name="frame"/> asks for messages from.</summary>
    IReadOnlyList<string> GetAuthors(TFrame frame);

    /// <summary>Gets the destination user names <paramref name="frame"/> asks for messages sent to.</summary>
    IReadOnlyList<string> GetDestinations(TFrame frame);

    /// <summary>Gets the message identifiers <paramref name="frame"/> asks for.</summary>
    IReadOnlyList<string> GetIds(TFrame frame);
}
