namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the aspects of the address types (<see cref="AddressType"/>), continuing the fluent chain of the engine builder. Name a type with <see cref="Type"/> to configure it, then continue with the next, for example <c>.AddressTypes().Type(AddressType.External).Label("OUTSIDE").Display&lt;MyDisplayHandler&gt;()</c>.
/// A type that is not named keeps its defaults.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IAddressTypesBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Selects <paramref name="type"/> to configure its aspects.</summary>
    /// <param name="type">The address type.</param>
    IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Type(AddressType type);
}
