namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="PeerIdentity"/>.</summary>
public sealed class PeerIdentityTests
{
    /// <summary>Every common name in the subject is returned, in order.</summary>
    [Fact]
    public void ExtractCommonNames_ReturnsEveryCn()
    {
        Assert.Equal(["Alice", "Alias"], PeerIdentity.ExtractCommonNames("CN=Alice, O=Org, CN=Alias"));
        Assert.Empty(PeerIdentity.ExtractCommonNames("O=Org"));
    }
}
