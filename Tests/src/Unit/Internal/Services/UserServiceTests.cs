namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="UserService"/>: who the node is, and what a refresh does when its certificate changes.</summary>
public sealed class UserServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"comlink-user-{Guid.NewGuid():N}");
    private readonly Mock<IEngineController> controller = new();
    private readonly CurrentUserProvider current = new();
    private readonly EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration());
    private readonly NetworkConfig network;

    /// <summary>Creates the folder the certificate files live in, and a network of ALICE and BOB.</summary>
    public UserServiceTests()
    {
        Directory.CreateDirectory(directory);
        builder.AuthorityCertificateValue = Authority;
        builder.UserCertificateValue = Certificate;
        network = NetworkConfig.Load([], directory);
        network.Users = new Dictionary<string, NetworkUserConfig>(StringComparer.OrdinalIgnoreCase) { ["ALICE"] = new(), ["BOB"] = new() };
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    private string Authority => Path.Combine(directory, "authority.cer");
    private string Certificate => Path.Combine(directory, "certificate.pfx");

    private void Write(X509Certificate2Collection authorities, X509Certificate2 identity)
    {
        File.WriteAllBytes(Authority, authorities[0].Export(X509ContentType.Cert));
        File.WriteAllBytes(Certificate, identity.Export(X509ContentType.Pfx));
    }

    private UserService Start(string user)
    {
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed(user);
        Write(authorities, identities[user]);
        NodeIdentity identity = MsmtCertificateLookup.ResolveIdentity(builder, network);
        current.UserName = identity.UserName;
        return new UserService(controller.Object, current, builder, network, new IdentityCheck { Identity = identity }, LoggerFactory.Create(_ => { }));
    }

    /// <summary>The current user's info is whatever the controller says about the user the certificate named, and there is none without a current user.</summary>
    [Fact]
    public void GetCurrentUserInfo_IsTheCertificatesUser()
    {
        UserInfo<object> info = new() { Name = "ALICE" };
        controller.Setup(c => c.GetUserInfo("ALICE")).Returns(info);
        UserService service = Start("Alice");

        Assert.Same(info, service.GetCurrentUserInfo());
        Assert.Equal("ALICE", service.Identity.UserName);

        current.UserName = null;
        Assert.Null(service.GetCurrentUserInfo());
    }

    /// <summary>A refresh that finds the same certificate changes nothing and announces nothing.</summary>
    [Fact]
    public async Task Refresh_SameCertificate_ChangesNothing()
    {
        UserService service = Start("ALICE");
        int announced = 0;
        service.Changed += () => announced++;

        Assert.False(await service.Refresh());

        Assert.Equal((0, "ALICE"), (announced, current.UserName));
    }

    /// <summary>A certificate that now names another user makes that user current and announces it.</summary>
    [Fact]
    public async Task Refresh_CertificateNamesAnotherUser_SwitchesUsers()
    {
        UserService service = Start("ALICE");
        int announced = 0;
        service.Changed += () => announced++;
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("BOB");
        Write(authorities, identities["BOB"]);

        Assert.True(await service.Refresh());

        Assert.Equal((1, "BOB", "BOB"), (announced, current.UserName, service.Identity.UserName));
        Assert.Equal(identities["BOB"].Thumbprint, service.Identity.CertificateThumbprint);
    }

    /// <summary>A replaced certificate for the same user is not a change of user, but the identity records the new thumbprints so the connections can be restarted.</summary>
    [Fact]
    public async Task Refresh_CertificateReplacedForTheSameUser_KeepsTheUserAndRecordsTheNewCertificate()
    {
        UserService service = Start("ALICE");
        string before = service.Identity.CertificateThumbprint;
        int announced = 0;
        service.Changed += () => announced++;
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
        Write(authorities, identities["ALICE"]);

        Assert.False(await service.Refresh());

        Assert.Equal((0, "ALICE"), (announced, current.UserName));
        Assert.NotEqual(before, service.Identity.CertificateThumbprint);
        Assert.Equal(identities["ALICE"].Thumbprint, service.Identity.CertificateThumbprint);
    }

    /// <summary>A certificate that can no longer be used leaves nobody as the current user, says why, and announces it; a later refresh that finds a good one makes the user current again.</summary>
    [Fact]
    public async Task Refresh_CertificateGone_LeavesNobodyWithAProblem_AndRecovers()
    {
        UserService service = Start("ALICE");
        int announced = 0;
        service.Changed += () => announced++;
        string good = File.ReadAllText(Certificate, System.Text.Encoding.Latin1);
        File.Delete(Certificate);

        Assert.True(await service.Refresh());

        Assert.Equal((1, null), (announced, current.UserName));
        Assert.Contains("was not found", service.Problem);
        Assert.Null(service.GetCurrentUserInfo());

        File.WriteAllText(Certificate, good, System.Text.Encoding.Latin1);
        Assert.True(await service.Refresh());

        Assert.Equal((2, "ALICE"), (announced, current.UserName));
        Assert.Null(service.Problem);
    }

    /// <summary>A node whose certificates were already bad at startup has no user and the reason, until a refresh finds them in order.</summary>
    [Fact]
    public async Task StartedWithAProblem_HasNoUser_UntilARefreshFindsAGoodCertificate()
    {
        IdentityCheck check = MsmtCertificateLookup.CheckIdentity(builder, network);
        UserService service = new(controller.Object, current, builder, network, check, LoggerFactory.Create(_ => { }));
        Assert.Contains("was not found", service.Problem);
        Assert.Null(service.GetCurrentUserInfo());
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("BOB");
        Write(authorities, identities["BOB"]);

        Assert.True(await service.Refresh());

        Assert.Equal(("BOB", null), (current.UserName, service.Problem));
    }
}
