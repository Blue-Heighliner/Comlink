namespace BlueHeighliner.Comlink;

/// <summary>Configures the aspects of one address type (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.AddressType"/>); it is also a <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>, so the next type can follow straight on.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Sets the display label shown for the type: in the address type picker, the per-address badge, and the message view's section headers. Defaults to the enum name (<c>To</c>, <c>Cc</c>, <c>External</c>). It never changes how an address is stored, routed or read.</summary>
    /// <param name="label">The label.</param>
    IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Label(string label);
}
