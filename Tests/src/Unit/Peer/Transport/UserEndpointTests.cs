namespace BlueHeighliner.Comlink.Tests.Unit.Models;

/// <summary>Unit tests for <see cref="UserEndpoint"/> and its config-file shape.</summary>
public sealed class UserEndpointTests
{
    /// <summary>An endpoint with a serial port is serial; without one it is IP.</summary>
    [Fact]
    public void IsSerial_FollowsSerialPort()
    {
        Assert.True(new UserEndpoint { SerialPort = "SL0" }.IsSerial);
        Assert.False(new UserEndpoint { IpAddress = "10.0.0.1", Port = 1 }.IsSerial);
        Assert.False(new UserEndpoint { SerialPort = "" }.IsSerial);
    }

    /// <summary>Two IP endpoints are equal when host (ignoring case) and port match.</summary>
    [Fact]
    public void Equals_Ip_ComparesHostIgnoringCaseAndPort()
    {
        UserEndpoint a = new() { IpAddress = "Host.Example", Port = 5 };

        Assert.Equal(a, new UserEndpoint { IpAddress = "host.example", Port = 5 });
        Assert.NotEqual(a, new UserEndpoint { IpAddress = "host.example", Port = 6 });
        Assert.Equal(a.GetHashCode(), new UserEndpoint { IpAddress = "HOST.EXAMPLE", Port = 5 }.GetHashCode());
    }

    /// <summary>Two serial endpoints are equal when port name (ignoring case) and address match; the IP members do not matter.</summary>
    [Fact]
    public void Equals_Serial_ComparesPortIgnoringCaseAndAddress()
    {
        UserEndpoint a = new() { SerialPort = "SL0", SerialAddress = 3, Port = 99 };

        Assert.Equal(a, new UserEndpoint { SerialPort = "sl0", SerialAddress = 3 });
        Assert.NotEqual(a, new UserEndpoint { SerialPort = "SL0", SerialAddress = 4 });
        Assert.NotEqual(a, new UserEndpoint { SerialPort = "SL1", SerialAddress = 3 });
    }

    /// <summary>A serial endpoint is never equal to an IP one, nor to null.</summary>
    [Fact]
    public void Equals_SerialVersusIpOrNull_IsFalse()
    {
        UserEndpoint serial = new() { SerialPort = "SL0" };

        Assert.NotEqual(serial, new UserEndpoint { IpAddress = "SL0", Port = 255 });
        Assert.False(serial.Equals(null));
        Assert.False(serial.Equals("SL0"));
    }

    /// <summary>The serial address defaults to 255 (0xFF), the MicroGate default.</summary>
    [Fact]
    public void SerialAddress_DefaultsTo255() => Assert.Equal(0xFF, new UserEndpoint().SerialAddress);

    /// <summary>ToString names the medium so log lines say where a link goes.</summary>
    [Fact]
    public void ToString_DescribesEndpoint()
    {
        Assert.Equal("10.0.0.1:50021", new UserEndpoint { IpAddress = "10.0.0.1", Port = 50021 }.ToString());
        Assert.Equal("SL0 (address 7)", new UserEndpoint { SerialPort = "SL0", SerialAddress = 7 }.ToString());
    }

    /// <summary>A config entry with a serial port converts to a serial endpoint carrying its address.</summary>
    [Fact]
    public void UserEndpointConfig_Serial_ConvertsToSerialEndpoint()
    {
        UserEndpoint endpoint = new UserEndpointConfig { SerialPort = "SL2", SerialAddress = 9 }.ToEndpoint();

        Assert.True(endpoint.IsSerial);
        Assert.Equal("SL2", endpoint.SerialPort);
        Assert.Equal(9, endpoint.SerialAddress);
    }

    /// <summary>A config entry with only an IP address and port converts to an IP endpoint, as before.</summary>
    [Fact]
    public void UserEndpointConfig_Ip_ConvertsToIpEndpoint()
    {
        UserEndpoint endpoint = new UserEndpointConfig { IpAddress = "10.1.1.1", Port = 8 }.ToEndpoint();

        Assert.False(endpoint.IsSerial);
        Assert.Equal("10.1.1.1", endpoint.IpAddress);
        Assert.Equal(8, endpoint.Port);
    }

    /// <summary>Users, ServerEndpoint, and ServerUsers entries in a config file can each name a serial port instead of an IP address.</summary>
    [Fact]
    public void EngineConfig_SerialEntries_ParseIntoSerialEndpoints()
    {
        string path = Path.Combine(Path.GetTempPath(), $"comlink-serial-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "Users": { "Bob": { "SerialPort": "SL0", "SerialAddress": 5 }, "Carol": { "IpAddress": "10.0.0.9", "Port": 7 } },
              "ServerEndpoint": { "SerialPort": "SL1" },
              "ServerUsers": { "S1": { "SerialPort": "SL2", "ChildClients": [ "C1" ] }, "S2": { "IpAddress": "10.0.0.3", "Port": 4, "ChildClients": [] } }
            }
            """);
        try
        {
            EngineConfig config = EngineConfig.Load(["--config", path]);

            IReadOnlyDictionary<string, UserEndpoint> users = config.GetUserEndpoints();
            Assert.Equal(new UserEndpoint { SerialPort = "SL0", SerialAddress = 5 }, users["Bob"]);
            Assert.Equal(new UserEndpoint { IpAddress = "10.0.0.9", Port = 7 }, users["Carol"]);
            Assert.True(config.ServerEndpoint!.ToEndpoint().IsSerial);
            IReadOnlyDictionary<string, ServerUserConfig> servers = config.GetServerUsers();
            Assert.Equal(new UserEndpoint { SerialPort = "SL2" }, servers["S1"].Endpoint);
            Assert.Equal(new UserEndpoint { IpAddress = "10.0.0.3", Port = 4 }, servers["S2"].Endpoint);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
