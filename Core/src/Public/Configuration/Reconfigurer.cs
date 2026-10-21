namespace BlueHeighliner.Comlink;

/// <summary>
/// Runs the host's own work each time the engine's configuration has been loaded: when it is first read at start-up and again every time it is refreshed. By then the network configuration file has been read, the services are set up and the current user has been identified,
/// so anything that depends on who is on the network, and who this node is, can be done here. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Reconfigure{TReconfigurer}"/>. It is built through dependency injection from the running engine's services,
/// so it can ask for any of them, such as the <see cref="IUserCatalog"/>.
/// </summary>
public interface IReconfigurer
{
    /// <summary>
    /// Called once the configuration has been loaded and the current user identified: at start-up before the engine starts connecting, and after every refresh once the refresh has been applied (including a refresh that switched to another user, or that
    /// found one after none could be identified). It is never called while there is no current user, such as while the node's certificate cannot be used. The engine waits for it to finish. An exception it throws is logged rather than thrown back, and the engine goes on.
    /// A refresh whose network configuration file cannot be read changes nothing and does not call it.
    /// </summary>
    Task OnReconfigured();
}
