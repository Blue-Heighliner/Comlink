namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="PointMaintenance"/>: only the points that changed are started or stopped.</summary>
public sealed class PointMaintenanceTests
{
    private static readonly ConnectionPoint first = new() { IpAddress = "10.0.0.1", Port = 1 };
    private static readonly ConnectionPoint second = new() { IpAddress = "10.0.0.2", Port = 2 };

    private static Mock<IPeerTransport> Transport()
    {
        Mock<IPeerTransport> transport = new();
        transport.Setup(t => t.Connect(It.IsAny<ConnectionPoint>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("unreachable"));
        return transport;
    }

    /// <summary>The first sync starts every point, and a second sync with the same points starts and stops nothing.</summary>
    [Fact]
    public void Sync_SamePointsAgain_ChangesNothing()
    {
        PointMaintenance maintenance = new(new PeerConnectionMonitor());
        Mock<IPeerTransport> transport = Transport();
        using CancellationTokenSource lifetime = new();

        (IReadOnlyList<ConnectionPoint> removed, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = maintenance.Sync(transport.Object, [first, second], lifetime.Token);
        (IReadOnlyList<ConnectionPoint> removedAgain, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> startedAgain) = maintenance.Sync(transport.Object, [second, first], lifetime.Token);

        Assert.Empty(removed);
        Assert.Equal([first, second], started.Select(entry => entry.Point).Order(Comparer<ConnectionPoint>.Create((a, b) => string.CompareOrdinal(a.Key, b.Key))));
        Assert.Empty(removedAgain);
        Assert.Empty(startedAgain);
        transport.Verify(t => t.SetClosed(It.IsAny<ConnectionPoint>(), It.IsAny<bool>()), Times.Never);
        lifetime.Cancel();
    }

    /// <summary>A point that is no longer defined is closed on the transport and reported removed; one that is newly defined is started; the rest are untouched.</summary>
    [Fact]
    public void Sync_PointsChanged_StopsTheRemovedAndStartsTheAdded()
    {
        PointMaintenance maintenance = new(new PeerConnectionMonitor());
        Mock<IPeerTransport> transport = Transport();
        using CancellationTokenSource lifetime = new();
        maintenance.Sync(transport.Object, [first], lifetime.Token);

        (IReadOnlyList<ConnectionPoint> removed, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = maintenance.Sync(transport.Object, [second], lifetime.Token);

        Assert.Equal([first], removed);
        Assert.Equal([second], started.Select(entry => entry.Point));
        transport.Verify(t => t.SetClosed(first, true), Times.Once);
        lifetime.Cancel();
    }

    /// <summary>A point that comes back after being removed is reopened on the transport before its loop starts again.</summary>
    [Fact]
    public void Sync_PointReturns_IsReopened()
    {
        PointMaintenance maintenance = new(new PeerConnectionMonitor());
        Mock<IPeerTransport> transport = Transport();
        using CancellationTokenSource lifetime = new();
        maintenance.Sync(transport.Object, [first], lifetime.Token);
        maintenance.Sync(transport.Object, [], lifetime.Token);

        maintenance.Sync(transport.Object, [first], lifetime.Token);

        transport.Verify(t => t.SetClosed(first, false), Times.Once);
        lifetime.Cancel();
    }

    /// <summary>A serial point whose named user changed counts as a different point: the link is closed and opened again so it is identified as the new user.</summary>
    [Fact]
    public void Sync_SerialPointUserChanged_ReopensThePoint()
    {
        PointMaintenance maintenance = new(new PeerConnectionMonitor());
        Mock<IPeerTransport> transport = Transport();
        using CancellationTokenSource lifetime = new();
        ConnectionPoint before = new() { SerialPort = "SL0", User = "A" };
        ConnectionPoint after = new() { SerialPort = "SL0", User = "B" };
        maintenance.Sync(transport.Object, [before], lifetime.Token);

        (IReadOnlyList<ConnectionPoint> removed, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = maintenance.Sync(transport.Object, [after], lifetime.Token);

        Assert.Single(removed);
        Assert.Equal("B", Assert.Single(started).Point.User);
        lifetime.Cancel();
    }
}
