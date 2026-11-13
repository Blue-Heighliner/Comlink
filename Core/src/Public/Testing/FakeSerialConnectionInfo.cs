namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="ISerialConnectionInfo"/> of a serial connection, to give a fake handshake context or a handler under test a connection of that kind. It is never inbound, as a real one is not, since both ends open the port.</summary>
public sealed class FakeSerialConnectionInfo : ISerialConnectionInfo
{
    /// <inheritdoc />
    public bool IsInbound => false;

    /// <inheritdoc />
    public string? LocalUser { get; set; }

    /// <inheritdoc />
    public string SerialPort { get; set; } = "SL0";

    /// <inheritdoc />
    public byte SerialAddress { get; set; } = 1;

    /// <inheritdoc />
    public byte RemoteSerialAddress { get; set; } = 2;
}
