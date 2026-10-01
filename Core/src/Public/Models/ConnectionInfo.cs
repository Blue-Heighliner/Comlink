namespace BlueHeighliner.Comlink.Models;

/// <summary>
/// What is known about a connection that has just formed, handed to <see cref="IEngineBuilder.Identify"/>
/// and the initial packet and frame contexts so a host can decide who is on the other end. The medium specific details
/// are on <see cref="IIpConnectionInfo"/> and <see cref="ISerialConnectionInfo"/>; test which one a connection is with a type pattern.
/// </summary>
public interface IConnectionInfo
{
    /// <summary>Gets a value indicating whether the remote node opened the connection to this node's listener. A serial connection is never inbound, since both ends open the port.</summary>
    bool IsInbound { get; }

    /// <summary>Gets the name of the user this node runs as, for an initial packet or frame to say who is speaking. <see langword="null"/> before a user is installed or named.</summary>
    string? LocalUser { get; }
}
