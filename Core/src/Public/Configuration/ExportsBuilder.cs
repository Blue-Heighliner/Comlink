namespace BlueHeighliner.Comlink;

/// <summary>Configures the export formats of the client's export screen, continuing the fluent chain of the engine builder, for example <c>.Exports().Format&lt;MyExportFormat&gt;().KioskMode()</c>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the security levels.</typeparam>
public interface IExportsBuilder<TFrame, TPacket, TPriority, TLevel> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>Adds a custom export format, shown as an option alongside the built-in JSON format in the client's export screen (see <see cref="IExportFormat"/>). Adding another with the same name replaces the earlier one in place.</summary>
    /// <typeparam name="TFormat">The format type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IExportsBuilder<TFrame, TPacket, TPriority, TLevel> Format<TFormat>() where TFormat : IExportFormat;
}
