namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkReceivedContext{TFrame, TPriority, TLevel, TAspect}" />
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
internal sealed class NetworkReceivedContext<TFrame, TPriority, TLevel, TAspect> : NetworkEngineContext<TFrame, TPriority, TLevel, TAspect>, INetworkReceivedContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Creates the context for a frame that was received.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="frame">The frame that was received.</param>
    /// <param name="origin">Where it came from.</param>
    /// <param name="sourceUser">The name of the user it arrived from, or of the external system.</param>
    public NetworkReceivedContext(INetworkEnvironment environment, TFrame frame, FrameOrigin origin, string sourceUser)
        : base(environment)
    {
        Frame = frame;
        Origin = origin;
        SourceUser = GetUser(sourceUser);
    }

    /// <inheritdoc />
    public TFrame Frame { get; }

    /// <inheritdoc />
    public FrameOrigin Origin { get; }

    /// <inheritdoc />
    public UserInfo SourceUser { get; }
}
