namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkDisconnectedContext{TFrame, TPriority, TLevel, TAspect}" />
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
internal sealed class NetworkDisconnectedContext<TFrame, TPriority, TLevel, TAspect> : NetworkEngineContext<TFrame, TPriority, TLevel, TAspect>, INetworkDisconnectedContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Creates the context for a user who disconnected.</summary>
    /// <param name="environment">What the processor acts on.</param>
    /// <param name="targetUser">The name of the user.</param>
    public NetworkDisconnectedContext(INetworkEnvironment environment, string targetUser)
        : base(environment) => TargetUser = GetUser(targetUser);

    /// <inheritdoc />
    public UserInfo TargetUser { get; }
}
