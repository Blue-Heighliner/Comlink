namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="RetrievalService"/>.</summary>
public sealed class RetrievalServiceTests
{
    private static (RetrievalService Service, Mock<INetworkProcessing> Processing) Build(string? user = "ALICE", bool processorStated = true)
    {
        Mock<ICurrentUserProvider> currentUser = new();
        currentUser.SetupGet(p => p.UserName).Returns(user);
        Mock<INetworkProcessing> processing = new();
        processing.Setup(p => p.Retrieval(It.IsAny<string>(), It.IsAny<RetrievalCriteria>())).Returns(processorStated);
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.StorageServers).Returns(["SERVER"]);
        return (new RetrievalService(controller.Object, currentUser.Object, processing.Object), processing);
    }

    /// <summary>The request is handed to the network processor with the server and the criteria.</summary>
    [Fact]
    public async Task Request_HandsTheCriteriaToTheProcessor()
    {
        (RetrievalService service, Mock<INetworkProcessing> processing) = Build();
        RetrievalCriteria criteria = new() { Authors = ["BOB"], Ids = ["M1"] };

        Assert.True(await service.Request("SERVER", criteria));

        processing.Verify(p => p.Retrieval("SERVER", criteria), Times.Once);
    }

    /// <summary>A retrieval can only be asked of a server, so any other user is refused before the processor is told.</summary>
    [Fact]
    public async Task Request_ToAUserThatIsNotAServer_IsRefused()
    {
        (RetrievalService service, Mock<INetworkProcessing> processing) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => service.Request("BOB", new RetrievalCriteria()));
        processing.Verify(p => p.Retrieval(It.IsAny<string>(), It.IsAny<RetrievalCriteria>()), Times.Never);
    }

    /// <summary>Without a processor nobody takes the request, which reports failure.</summary>
    [Fact]
    public async Task Request_WithoutAProcessor_ReturnsFalse()
    {
        (RetrievalService service, _) = Build(processorStated: false);

        Assert.False(await service.Request("SERVER", new RetrievalCriteria()));
    }

    /// <summary>Without an installed user there is nobody to request for.</summary>
    [Fact]
    public async Task Request_NoUser_Throws()
    {
        (RetrievalService service, _) = Build(user: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Request("SERVER", new RetrievalCriteria()));
    }
}
