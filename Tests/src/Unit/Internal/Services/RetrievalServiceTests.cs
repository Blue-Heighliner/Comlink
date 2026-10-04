namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="RetrievalService"/>.</summary>
public sealed class RetrievalServiceTests
{
    private static (RetrievalService Service, Mock<IMessageRoutingService> Routing, List<object> Sent) Build(string? user = "ALICE", bool delivered = true)
    {
        Mock<ICurrentUserProvider> currentUser = new();
        currentUser.SetupGet(p => p.UserName).Returns(user);
        Mock<IMessageRoutingService> routing = new();
        List<object> sent = [];
        routing.Setup(r => r.RouteFrame(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>((_, message, _) => sent.Add(message))
            .ReturnsAsync(("ID", (IReadOnlyList<UserDeliveryResult>)[new UserDeliveryResult { UserName = "SERVER", Success = delivered }]));
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.StorageServers).Returns(["SERVER"]);
        return (new RetrievalService(controller.Object, currentUser.Object, routing.Object), routing, sent);
    }

    /// <summary>The request is an ordinary message carrying the criteria in its retrieval fields, addressed only to the server, routed from the current user.</summary>
    [Fact]
    public async Task Request_RoutesAMessageCarryingTheCriteriaToTheServer()
    {
        (RetrievalService service, Mock<IMessageRoutingService> routing, List<object> sent) = Build();
        RetrievalCriteria criteria = new() { Authors = ["BOB"], Ids = ["M1"] };

        bool ok = await service.Request("SERVER", criteria);

        Assert.True(ok);
        TestFrame request = Assert.IsType<TestFrame>(Assert.Single(sent));
        Assert.True(request.IsRetrieval);
        Assert.Equal(["BOB"], request.RetrievalAuthors);
        Assert.Equal(["M1"], request.RetrievalIds);
        TestAddressEntry address = Assert.Single(request.Addresses);
        Assert.Equal(("SERVER", "To"), (address.UserName, address.Type));
        routing.Verify(r => r.RouteFrame("ALICE", request, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A retrieval can only be asked of a server, so any other user is refused before anything is sent.</summary>
    [Fact]
    public async Task Request_ToAUserThatIsNotAServer_IsRefused()
    {
        (RetrievalService service, Mock<IMessageRoutingService> routing, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => service.Request("BOB", new RetrievalCriteria()));
        routing.Verify(r => r.RouteFrame(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A request the server side never acknowledged reports failure.</summary>
    [Fact]
    public async Task Request_ServerNotReached_ReturnsFalse()
    {
        (RetrievalService service, _, _) = Build(delivered: false);

        Assert.False(await service.Request("SERVER", new RetrievalCriteria()));
    }

    /// <summary>Without an installed user there is nobody to request for.</summary>
    [Fact]
    public async Task Request_NoUser_Throws()
    {
        (RetrievalService service, _, _) = Build(user: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Request("SERVER", new RetrievalCriteria()));
    }
}
