namespace BlueHeighliner.Comlink;

/// <summary>Where a frame handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnReceived"/> came from.</summary>
public enum FrameOrigin
{
    /// <summary>A peer node, over an MSMT or HDLC connection.</summary>
    Peer,

    /// <summary>An application connected to the local interface.</summary>
    Interface,

    /// <summary>An external system.</summary>
    ExternalSystem
}
