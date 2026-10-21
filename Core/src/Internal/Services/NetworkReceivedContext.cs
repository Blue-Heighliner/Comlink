namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkReceivedContext{TFrame}" />
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="Frame">The frame that was received.</param>
/// <param name="Origin">Where it came from.</param>
/// <param name="SourceUser">The user it arrived from.</param>
internal sealed record NetworkReceivedContext<TFrame>(TFrame Frame, FrameOrigin Origin, UserInfo SourceUser) : INetworkReceivedContext<TFrame> where TFrame : class;
