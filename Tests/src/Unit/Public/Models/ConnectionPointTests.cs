namespace BlueHeighliner.Comlink.Tests.Unit.Public.Models;

/// <summary>Unit tests for <see cref="ConnectionPoint"/> and its config-file shape.</summary>
public sealed class ConnectionPointTests
{
    /// <summary>An point with a serial port is serial; without one it is IP.</summary>
    [Fact]
    public void IsSerial_FollowsSerialPort()
    {
        Assert.True(new ConnectionPoint { SerialPort = "SL0" }.IsSerial);
        Assert.False(new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }.IsSerial);
        Assert.False(new ConnectionPoint { SerialPort = "" }.IsSerial);
    }

    /// <summary>Two IP points are equal when host (ignoring case) and port match.</summary>
    [Fact]
    public void Equals_Ip_ComparesHostIgnoringCaseAndPort()
    {
        ConnectionPoint a = new() { IpAddress = "Host.Example", Port = 5 };

        Assert.Equal(a, new ConnectionPoint { IpAddress = "host.example", Port = 5 });
        Assert.NotEqual(a, new ConnectionPoint { IpAddress = "host.example", Port = 6 });
        Assert.Equal(a.GetHashCode(), new ConnectionPoint { IpAddress = "HOST.EXAMPLE", Port = 5 }.GetHashCode());
    }

    /// <summary>Two serial points are equal when port name (ignoring case) and address match; the IP members do not matter.</summary>
    [Fact]
    public void Equals_Serial_ComparesPortIgnoringCaseAndAddress()
    {
        ConnectionPoint a = new() { SerialPort = "SL0", SerialAddress = 3, Port = 99 };

        Assert.Equal(a, new ConnectionPoint { SerialPort = "sl0", SerialAddress = 3 });
        Assert.NotEqual(a, new ConnectionPoint { SerialPort = "SL0", SerialAddress = 4 });
        Assert.NotEqual(a, new ConnectionPoint { SerialPort = "SL1", SerialAddress = 3 });
    }

    /// <summary>A serial point is never equal to an IP one, nor to null.</summary>
    [Fact]
    public void Equals_SerialVersusIpOrNull_IsFalse()
    {
        ConnectionPoint serial = new() { SerialPort = "SL0" };

        Assert.NotEqual(serial, new ConnectionPoint { IpAddress = "SL0", Port = 255 });
        Assert.False(serial.Equals(null));
        Assert.False(serial.Equals("SL0"));
    }

    /// <summary>The serial address defaults to 255 (0xFF), the MicroGate default.</summary>
    [Fact]
    public void SerialAddress_DefaultsTo255() => Assert.Equal(0xFF, new ConnectionPoint().SerialAddress);

    /// <summary>ToString names the medium so log lines say where a link goes.</summary>
    [Fact]
    public void ToString_DescribesPoint()
    {
        Assert.Equal("10.0.0.1:50021", new ConnectionPoint { IpAddress = "10.0.0.1", Port = 50021 }.ToString());
        Assert.Equal("SL0 (address 7)", new ConnectionPoint { SerialPort = "SL0", SerialAddress = 7 }.ToString());
    }
}
