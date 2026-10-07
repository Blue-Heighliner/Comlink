namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="NetworkReloadService"/>: which changes to the network file restart something and which are applied in place.</summary>
public sealed class NetworkReloadServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"comlink-reload-{Guid.NewGuid():N}");
    private readonly Mock<IRolePeerService> peer = new();
    private readonly Mock<IInterfaceService> listener = new();
    private readonly Mock<ILogSettings> logSettings = new();
    private readonly Mock<IUserService> users = new();
    private readonly NetworkConfig network;
    private readonly NetworkReloadService service;

    /// <summary>Writes the initial file and builds the service over it, with ME as the current user.</summary>
    public NetworkReloadServiceTests()
    {
        Directory.CreateDirectory(directory);
        Write("""{ "Users": { "ME": { "PeerPort": 1000, "InterfacePort": 2000, "OutgoingPoints": [ { "IpAddress": "10.0.0.1", "Port": 1 } ], "SecurityLevel": "LOW" } } }""");
        network = NetworkConfig.Load([], directory);
        CurrentUserProvider user = new() { UserName = "ME" };
        EngineController controller = new(EngineBuilder.Build(new TestEngineConfiguration()), user, network);
        service = new NetworkReloadService(network, controller, peer.Object, listener.Object, logSettings.Object, users.Object, LoggerFactory.Create(_ => { }));
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    private void Write(string json) => File.WriteAllText(Path.Combine(directory, "Config.json"), json);

    /// <summary>A change that does not affect connections, such as a security level, restarts and reconfigures nothing that matters but is still read and announced.</summary>
    [Fact]
    public async Task Reload_UnrelatedChange_RestartsNothing_ButIsAppliedAndAnnounced()
    {
        int announced = 0;
        service.Reloaded += () => announced++;
        Write("""{ "Users": { "ME": { "PeerPort": 1000, "InterfacePort": 2000, "OutgoingPoints": [ { "IpAddress": "10.0.0.1", "Port": 1 } ], "SecurityLevel": "HIGH" } } }""");

        await service.Reload();

        peer.Verify(p => p.Restart(), Times.Never);
        listener.Verify(l => l.Restart(), Times.Never);
        Assert.Equal("HIGH", network.Find("ME")!.SecurityLevel);
        Assert.Equal(1, announced);
    }

    /// <summary>A changed outgoing point, peer port or topology is applied in place by reconfiguring, never by restarting, so connections that did not change are kept.</summary>
    [Fact]
    public async Task Reload_ConnectionSettingsChanged_ReconfiguresInPlace()
    {
        Write("""{ "Users": { "ME": { "PeerPort": 1001, "InterfacePort": 2000, "OutgoingPoints": [ { "IpAddress": "10.0.0.2", "Port": 1 } ] } } }""");

        await service.Reload();

        peer.Verify(p => p.Reconfigure(), Times.Once);
        peer.Verify(p => p.Restart(), Times.Never);
        listener.Verify(l => l.Restart(), Times.Never);
    }

    /// <summary>A changed role needs a different implementation, so the peer layer restarts instead of reconfiguring.</summary>
    [Fact]
    public async Task Reload_RoleChanged_RestartsThePeerLayer()
    {
        Write("""{ "Users": { "ME": { "Role": "Server", "PeerPort": 1000, "InterfacePort": 2000, "OutgoingPoints": [ { "IpAddress": "10.0.0.1", "Port": 1 } ] } } }""");

        await service.Reload();

        peer.Verify(p => p.Restart(), Times.Once);
        peer.Verify(p => p.Reconfigure(), Times.Never);
        listener.Verify(l => l.Restart(), Times.Never);
    }

    /// <summary>A changed interface port restarts only the interface listener.</summary>
    [Fact]
    public async Task Reload_InterfacePortChanged_RestartsTheInterfaceListener()
    {
        Write("""{ "Users": { "ME": { "PeerPort": 1000, "InterfacePort": 2001, "OutgoingPoints": [ { "IpAddress": "10.0.0.1", "Port": 1 } ] } } }""");

        await service.Reload();

        listener.Verify(l => l.Restart(), Times.Once);
        peer.Verify(p => p.Restart(), Times.Never);
    }

    /// <summary>Changing the certificate store or authority restarts both, since certificates are fixed when a listener or peer service is created.</summary>
    [Fact]
    public async Task Reload_CertificatesChanged_RestartsBoth()
    {
        Write("""{ "CertificateStore": "certs", "AuthorityCertificate": "root.cer", "Users": { "ME": { "PeerPort": 1000, "InterfacePort": 2000, "OutgoingPoints": [ { "IpAddress": "10.0.0.1", "Port": 1 } ] } } }""");

        await service.Reload();

        peer.Verify(p => p.Restart(), Times.Once);
        listener.Verify(l => l.Restart(), Times.Once);
    }

    /// <summary>A file that can no longer be read throws, restarts nothing, announces nothing, and leaves the configuration as it was.</summary>
    [Fact]
    public async Task Reload_UnreadableFile_ThrowsAndChangesNothing()
    {
        int announced = 0;
        service.Reloaded += () => announced++;
        Write("{ not json");

        await Assert.ThrowsAnyAsync<Exception>(() => service.Reload());

        Assert.Equal("LOW", network.Find("ME")!.SecurityLevel);
        peer.Verify(p => p.Restart(), Times.Never);
        peer.Verify(p => p.Reconfigure(), Times.Never);
        listener.Verify(l => l.Restart(), Times.Never);
        Assert.Equal(0, announced);
    }

    /// <summary>Logging.json is read again by every reload, so the categories it turns on take effect at once.</summary>
    [Fact]
    public async Task Reload_ReadsTheLoggingSettingsAgain()
    {
        await service.Reload();

        logSettings.Verify(l => l.Reload(), Times.Once);
    }

    /// <summary>When User.json changed who the user is, the connections are started again for that user, so they are neither reconfigured nor restarted here, and the reload is still announced.</summary>
    [Fact]
    public async Task Reload_UserChanged_AdjustsNothingAndAnnounces()
    {
        users.Setup(u => u.Refresh(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        int announced = 0;
        service.Reloaded += () => announced++;
        Write("""{ "Users": { "ME": { "Role": "Server", "PeerPort": 1001, "InterfacePort": 2001, "OutgoingPoints": [ { "IpAddress": "10.0.0.2", "Port": 1 } ] } } }""");

        await service.Reload();

        peer.Verify(p => p.Restart(), Times.Never);
        peer.Verify(p => p.Reconfigure(), Times.Never);
        listener.Verify(l => l.Restart(), Times.Never);
        Assert.Equal(1, announced);
    }
}
