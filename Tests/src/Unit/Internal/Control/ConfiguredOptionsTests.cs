namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for the MSMT and HDLC options a user's entry in the network configuration file states, and for the HDLC ports it opens.</summary>
public sealed class ConfiguredOptionsTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static NetworkConfig Network(NetworkUserConfig me) => new() { Users = new Dictionary<string, NetworkUserConfig> { ["ME"] = me } };

    private static ConfiguredEngineController Configured(IEngineController fallback, NetworkUserConfig me) => new(fallback, Network(me), new CurrentUserProvider { UserName = "ME" });

    /// <summary>The HDLC options the file states replace the defaults one by one, including nested link options and enum names, and everything it does not state stays as the host stated it.</summary>
    [Fact]
    public void HdlcOptions_OverlayStatedOptionsOnWhatTheHostStated()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.HdlcOptions).Returns(new HdlcPeerOptions { MaxInfoField = 512, TransmitWindow = 3 });
        ConfiguredEngineController controller = Configured(fallback.Object, new NetworkUserConfig
        {
            Hdlc = Json("""{ "address": 4, "Ports": "*", "maxinfofield": 1024, "AcknowledgeDelay": "00:00:01", "Link": { "Crc": "Crc32Ccitt", "ClockSpeed": 9600 } }""")
        });

        HdlcPeerOptions options = controller.HdlcOptions;

        Assert.Equal((1024, 3), (options.MaxInfoField, options.TransmitWindow));
        Assert.Equal(TimeSpan.FromSeconds(1), options.AcknowledgeDelay);
        Assert.Equal((HdlcCrc.Crc32Ccitt, 9600), (options.Link.Crc, options.Link.ClockSpeed));
    }

    /// <summary>Without an HDLC section the host's options are returned as they are.</summary>
    [Fact]
    public void HdlcOptions_WithoutASection_AreTheHostsOwn()
    {
        HdlcPeerOptions stated = new() { MaxInfoField = 512 };
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.HdlcOptions).Returns(stated);

        Assert.Same(stated, Configured(fallback.Object, new NetworkUserConfig()).HdlcOptions);
    }

    /// <summary>The MSMT options the file states replace the defaults one by one, a null disables a timeout, the engine's credentials are kept, and the listen port is not an option.</summary>
    [Fact]
    public void MsmtOptions_OverlayStatedOptionsKeepingCredentials()
    {
        MsmtCredentials credentials = new() { Identity = TestMsmtCertificates.Create().Server, TrustedAuthorities = [] };
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.ConfigureConnectionOptions(It.IsAny<MsmtSessionPeerOptions>())).Returns((MsmtSessionPeerOptions options) => options with { ResponseTimeout = TimeSpan.FromSeconds(9) });
        ConfiguredEngineController controller = Configured(fallback.Object, new NetworkUserConfig
        {
            Msmt = Json("""{ "Port": 1234, "HandshakeTimeout": "00:00:07", "stallTimeout": null, "SessionLifetime": "00:20:00" }""")
        });

        MsmtSessionPeerOptions options = controller.ConfigureConnectionOptions(new MsmtSessionPeerOptions { Credentials = credentials });

        Assert.Equal(TimeSpan.FromSeconds(7), options.HandshakeTimeout);
        Assert.Null(options.StallTimeout);
        Assert.Equal(TimeSpan.FromMinutes(20), options.SessionLifetime);
        Assert.Equal(TimeSpan.FromSeconds(9), options.ResponseTimeout);
        Assert.Same(credentials, options.Credentials);
    }

    /// <summary>A single <c>*</c> for the HDLC ports opens every port the machine has, one point per port, each trying the HDLC-linked users.</summary>
    [Fact]
    public void HdlcPorts_Star_OpensEveryAvailablePort()
    {
        NetworkConfig network = new()
        {
            Users = new Dictionary<string, NetworkUserConfig>
            {
                ["ME"] = new NetworkUserConfig { Role = "Client", Parent = new NetworkLinkConfig { User = "SERVER", Mode = "Hdlc" }, Hdlc = Json("""{ "Address": 2, "Ports": "*" }""") },
                ["SERVER"] = new NetworkUserConfig { Role = "Server", Hdlc = Json("""{ "Address": 5 }""") }
            }
        };
        Mock<IMicroGatePortSource> ports = new();
        ports.Setup(p => p.GetPorts(It.IsAny<CancellationToken>())).Returns(new ValueTask<IReadOnlyList<string>>(["ttyUSB0", "ttyUSB1"]));
        ServiceCollection services = new();
        services.AddSingleton(ports.Object);
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration());
        EngineController controller = new(builder, new CurrentUserProvider { UserName = "ME" }, network, services.BuildServiceProvider());

        Assert.Equal(
            [new ConnectionPoint { SerialPort = "ttyUSB0", SerialAddress = 2, RemoteSerialAddress = 5, User = "SERVER" }, new ConnectionPoint { SerialPort = "ttyUSB1", SerialAddress = 2, RemoteSerialAddress = 5, User = "SERVER" }],
            controller.ParentPoints);
        Assert.Equal(["ttyUSB0", "ttyUSB1"], controller.OutgoingPoints.Select(point => point.SerialPort));
    }
}
