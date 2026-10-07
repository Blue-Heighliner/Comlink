namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures how connections are made, continuing the fluent chain of the engine builder: <see cref="Msmt"/> for every IP connection and <see cref="Hdlc"/> for every serial one,
/// for example <c>.Connections().Msmt(msmt).Hdlc(hdlc).Display&lt;MyDisplayHandler&gt;()</c>. Anything not stated keeps its default.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>
    /// States the MSMT settings used for every IP connection, inbound and outbound, including the interface listener: timeouts,
    /// keep-alive and session lifetimes. The identity certificate and trusted authorities are still the engine's.
    /// Defaults to the MSMT package defaults.
    /// </summary>
    /// <param name="options">The settings to use.</param>
    IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Msmt(MsmtConnectionOptions options);

    /// <summary>
    /// States the MicroGate options used for every serial connection: line encoding, CRC, clocking, frame size, windowing and
    /// retransmission, which must match the station at the other end of the cable. The HDLC address is not an option; it comes from each serial
    /// connection point. Defaults to the HDLC peer defaults.
    /// </summary>
    /// <param name="options">The options to use.</param>
    IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Hdlc(HdlcPeerOptions options);
}
