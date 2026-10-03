namespace BlueHeighliner.Comlink;

/// <summary>
/// One connection between a user and its parent or one of its children. By default a user opens an outgoing connection to its parent and listens for incoming
/// connections from its children, so naming the other user is enough; stating a <see cref="Mode"/> forces the link into a specific mode, and the other end of the link
/// states the matching one (a child forced to <see cref="ConnectionMode.MsmtConnect"/> is dialed by its parent, so the child states <see cref="ConnectionMode.MsmtListen"/> for its parent).
/// Where each user can be reached is stated on the users themselves: their IP host, MSMT port, and HDLC address and ports.
/// </summary>
public sealed record UserLink
{
    /// <summary>Reads a plain user name as a link with the default mode.</summary>
    /// <param name="user">The name of the user at the other end.</param>
    public static implicit operator UserLink(string user) => new() { User = user };

    /// <summary>Gets the name of the user at the other end of the link.</summary>
    public required string User { get; init; }

    /// <summary>Gets the forced connection mode, or <see langword="null"/> for the default: <see cref="ConnectionMode.MsmtConnect"/> to a parent and <see cref="ConnectionMode.MsmtListen"/> for a child.</summary>
    public ConnectionMode? Mode { get; init; }
}
