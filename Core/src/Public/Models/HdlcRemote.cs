namespace BlueHeighliner.Comlink;

/// <summary>A user that may be at the other end of an HDLC serial point, and the station address it answers to.</summary>
/// <param name="User">The user's name.</param>
/// <param name="Address">The HDLC station address of that user's node.</param>
public sealed record HdlcRemote(string User, byte Address);
