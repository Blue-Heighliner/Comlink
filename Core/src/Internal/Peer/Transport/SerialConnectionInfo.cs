namespace BlueHeighliner.Comlink;

/// <summary>The implementation behind <see cref="ISerialConnectionInfo"/>.</summary>
internal sealed record SerialConnectionInfo : ConnectionInfo, ISerialConnectionInfo
{
    /// <inheritdoc />
    public string SerialPort { get; init; } = string.Empty;

    /// <inheritdoc />
    public byte SerialAddress { get; init; }

    /// <inheritdoc />
    public byte RemoteSerialAddress { get; set; }
}
