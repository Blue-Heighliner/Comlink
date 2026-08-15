namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="NullConnectionStatusService"/>.</summary>
public sealed class ConnectionStatusServiceTests
{
    /// <summary>GetStatuses always returns an empty list.</summary>
    [Fact]
    public void GetStatuses_AlwaysEmpty()
    {
        NullConnectionStatusService service = new();

        Assert.Empty(service.GetStatuses());
    }

    /// <summary>Subscribing to StatusesChanged is a no-op that never fires.</summary>
    [Fact]
    public void StatusesChanged_Subscribed_NeverFires()
    {
        NullConnectionStatusService service = new();
        bool raised = false;
        service.StatusesChanged += () => raised = true;

        Assert.False(raised);
    }
}
