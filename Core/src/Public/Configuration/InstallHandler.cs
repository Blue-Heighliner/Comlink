namespace BlueHeighliner.Comlink;

/// <summary>
/// Decides which user an installation code, entered by the user on the install screen, installs. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.Installs{THandler}"/>.
/// Without one, a code is the name of a user in the network configuration file (case-insensitive), and the code <c>CODE</c> installs a user named <c>TEST</c>.
/// </summary>
public interface IInstallHandler
{
    /// <summary>Returns the name of the user that <paramref name="code"/> installs, or <see langword="null"/> when the code is not recognized. Everything else about the user comes from the network configuration file.</summary>
    /// <param name="code">The installation code the user entered.</param>
    string? Install(string code);
}
