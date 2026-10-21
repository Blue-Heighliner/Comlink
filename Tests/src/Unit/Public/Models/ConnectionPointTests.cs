namespace BlueHeighliner.Comlink.Tests.Unit.Public.Models;

/// <summary>Unit tests for <see cref="ConnectionPoint"/> and its config-file shape.</summary>
public sealed class ConnectionPointTests
{

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
}
