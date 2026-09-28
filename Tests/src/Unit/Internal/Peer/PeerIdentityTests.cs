namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="PeerIdentity"/>.</summary>
public sealed class PeerIdentityTests
{
    /// <summary>The common name is the value of the CN component, wherever it is in the distinguished name.</summary>
    [Theory]
    [InlineData("CN=Alice", "Alice")]
    [InlineData("O=Org, CN=Alice, C=US", "Alice")]
    [InlineData("cn=Alice", "Alice")]
    public void ExtractCommonName_ReturnsCnComponent(string subject, string expected)
        => Assert.Equal(expected, PeerIdentity.ExtractCommonName(subject));

    /// <summary>A subject with no common name is returned whole.</summary>
    [Fact]
    public void ExtractCommonName_NoCn_ReturnsWholeSubject()
        => Assert.Equal("O=Org", PeerIdentity.ExtractCommonName("O=Org"));

    /// <summary>Every common name in the subject is returned, in order.</summary>
    [Fact]
    public void ExtractCommonNames_ReturnsEveryCn()
    {
        Assert.Equal(["Alice", "Alias"], PeerIdentity.ExtractCommonNames("CN=Alice, O=Org, CN=Alias"));
        Assert.Empty(PeerIdentity.ExtractCommonNames("O=Org"));
    }
}
