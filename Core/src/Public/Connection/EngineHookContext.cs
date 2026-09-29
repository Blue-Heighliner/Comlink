namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Simplified, synchronous snapshot handed to every connection and message hook (see
/// <see cref="IEngineBuilder.OnUserConnected"/>/<see cref="IEngineBuilder.OnUserDisconnected"/>/
/// <see cref="IEngineBuilder.OnMessageReceived"/>), exposing only what a hook needs rather than the full
/// <see cref="IServiceConnection"/> surface: this instance's own identity, the full user directory, who is
/// currently reachable, and a way to originate a new message or packet. Each <see cref="Users"/>/
/// <see cref="ConnectedUsers"/> entry carries that user's directly-assigned group memberships, the same as
/// <see cref="UserInfo.Groups"/>, but never a real installation code (empty), since that only ever belongs to
/// this instance's own <see cref="CurrentUser"/>. <see cref="IUserConnectionHookContext"/> and
/// <see cref="IMessageReceivedHookContext"/> add the one further piece of data specific to their event.
/// </summary>
public interface IEngineHookContext
{
    /// <summary>This instance's own installed user. Never <see langword="null"/>: a hook only ever runs once a user is installed.</summary>
    UserInfo CurrentUser { get; }

    /// <summary>Every known user in the messaging system (see <see cref="IEngineBuilder.Users"/>), computed lazily as enumerated rather than materialized upfront.</summary>
    IEnumerable<UserInfo> Users { get; }

    /// <summary>The subset of <see cref="Users"/> currently reachable over at least one live peer connection, computed lazily as enumerated rather than materialized upfront.</summary>
    IEnumerable<UserInfo> ConnectedUsers { get; }

    /// <summary>Whether <paramref name="userName"/> is currently reachable over at least one live peer connection.</summary>
    bool IsConnected(string userName);

    /// <summary>
    /// Sends a new, already-built message - fire-and-forget: a hook does not track or await the send it makes, so
    /// this returns nothing and the send proceeds in the background exactly as it would from any other caller (a
    /// failure is logged, not thrown back into the hook). <paramref name="message"/> must be an instance of
    /// <see cref="IEngineBuilder.Message{TMessage}"/>'s configured message type; its message ID, sender, and sent
    /// time are overwritten before it is routed, so only its content fields (subject, body, addresses, ...) need
    /// to be set.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="message"/> is not an instance of the configured message type.</exception>
    void SendMessage(object message);

    /// <summary>
    /// Sends a raw, already-built packet directly to every user named in <paramref name="userNames"/> - fire-and-forget,
    /// the same as <see cref="SendMessage"/>. Bypasses the normal packetization/reassembly a full message goes
    /// through, and carries no delivery-status tracking of its own; unlike a message, a packet has no address list
    /// of its own, so <paramref name="userNames"/> states who receives it directly, with no group expansion.
    /// </summary>
    /// <param name="packet">Must be an instance of the configured packet type (see <see cref="IEngineBuilder.Packets{TPacket}"/>).</param>
    /// <param name="userNames">Who receives the packet.</param>
    /// <exception cref="ArgumentException"><paramref name="packet"/> is not an instance of the configured packet type, or no packet type is configured at all.</exception>
    void SendPacket(object packet, params IEnumerable<string> userNames);
}

/// <summary>An <see cref="IEngineHookContext"/> for <see cref="IEngineBuilder.OnUserConnected"/>/<see cref="IEngineBuilder.OnUserDisconnected"/>.</summary>
public interface IUserConnectionHookContext : IEngineHookContext
{
    /// <summary>The user that connected or disconnected.</summary>
    string TargetUser { get; }
}

/// <summary>An <see cref="IEngineHookContext"/> for <see cref="IEngineBuilder.OnMessageReceived"/>.</summary>
public interface IMessageReceivedHookContext : IEngineHookContext
{
    /// <summary>The message that was received - an instance of the configured message type, the same as <see cref="IEngineHookContext.SendMessage"/> expects, so a hook reads it by casting to that type directly.</summary>
    object Message { get; }
}
