namespace BlueHeighliner.Comlink;

/// <summary>
/// Lets the host take part in the engine's configuration: it says which user a certificate means, and runs its own work each time the configuration has been loaded, when it is first read at start-up and again every time it is refreshed. State one with
/// <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Configure{TConfigurationHandler}"/>. It is built through dependency injection, and every member is optional. <see cref="Identify"/> is asked before the engine exists, so a handler that
/// states it is built once from the bootstrap container (logging and the services the host registered) and can use nothing the engine itself registers there, while <see cref="OnReconfigured"/> is called on one built from the running engine's services, so it can ask for any of them,
/// such as the <see cref="IUserCatalog{TLevel, TProfile}"/>.
/// </summary>
public interface IConfigurationHandler
{
    /// <summary>
    /// Says which user the node is, from the subject common names of its user certificate, in the order the certificate lists them (never empty). Asked at start-up and again by every refresh. The user it returns is matched without regard to case to a user of the network configuration file;
    /// a name that is none of them means the node cannot be identified, which is shown to the user like any other problem with the certificates, and so does an exception it throws. The default is the first common name.
    /// </summary>
    /// <param name="commonNames">The subject common names of the user certificate.</param>
    /// <returns>The name of the user.</returns>
    string Identify(IReadOnlyList<string> commonNames) => commonNames[0];

    /// <summary>
    /// Called once the configuration has been loaded and the current user identified: at start-up before the engine starts connecting, and after every refresh once the refresh has been applied (including a refresh that switched to another user, or that
    /// found one after none could be identified). It is never called while there is no current user, such as while the node's certificate cannot be used. The engine waits for it to finish. An exception it throws is logged rather than thrown back, and the engine goes on.
    /// A refresh whose network configuration file cannot be read changes nothing and does not call it. The default does nothing.
    /// </summary>
    Task OnReconfigured() => Task.CompletedTask;
}
