namespace BlueHeighliner.Comlink;

/// <summary>The implementation behind <see cref="ISerialConnectionInfo"/>.</summary>
internal sealed record SerialConnectionInfo : ConnectionInfo, ISerialConnectionInfo
{
    /// <inheritdoc />
    public string Port { get; init; } = string.Empty;

    /// <inheritdoc />
    public byte Address { get; init; }

    /// <inheritdoc />
    public byte RemoteAddress { get; set; }

    /// <summary>The remote HDLC address the far end was found to be at once its user was identified, which the link dials from then on in place of cycling through the candidates; <see langword="null"/> until one is learned.</summary>
    public byte? PreferredRemoteAddress { get; set; }
}
