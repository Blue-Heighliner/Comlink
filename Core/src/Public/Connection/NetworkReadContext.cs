namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}.OnRead"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public interface INetworkReadContext<TFrame, TPriority, TLevel, TAspect> : INetworkContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Gets the message the user opened, as stored in the Inbox.</summary>
    Message<TPriority, TLevel, TAspect> Message { get; }
}
