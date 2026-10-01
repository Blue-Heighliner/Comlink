namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="UserConnections"/>.</summary>
public sealed class UserConnectionsTests
{
    private static PeerConnection Connection(string? user, bool inbound = false)
        => new(inbound ? null : new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }, new IpConnectionInfo { IsInbound = inbound }, () => { }) { User = user is null ? null : new UserIdentity { Name = user } };

    /// <summary>A connection is found under the name it was identified as, ignoring case.</summary>
    [Fact]
    public void Get_FindsConnectionByIdentifiedNameIgnoringCase()
    {
        UserConnections connections = new();
        PeerConnection connection = Connection("Alice");
        connections.Add(connection);

        Assert.Same(connection, connections.Get("ALICE"));
        Assert.True(connections.Has("alice"));
        Assert.True(connections.Contains(connection));
    }

    /// <summary>A user nobody is identified as has no connection.</summary>
    [Fact]
    public void Get_UnknownUser_ReturnsNull()
    {
        UserConnections connections = new();
        connections.Add(Connection("Alice"));

        Assert.Null(connections.Get("Bob"));
        Assert.False(connections.Has("Bob"));
        Assert.Empty(connections.GetAll("Bob"));
    }

    /// <summary>When a user has several connections the newest is the one returned, and all of them are listed oldest first.</summary>
    [Fact]
    public void Get_SeveralConnections_ReturnsNewest()
    {
        UserConnections connections = new();
        PeerConnection older = Connection("Alice");
        PeerConnection newer = Connection("Alice", inbound: true);
        connections.Add(older);
        connections.Add(newer);

        Assert.Same(newer, connections.Get("Alice"));
        Assert.Equal([older, newer], connections.GetAll("Alice"));
    }

    /// <summary>Removing a connection reports the user it belonged to, and leaves the user reachable while another connection remains.</summary>
    [Fact]
    public void Remove_ReportsUserAndKeepsOtherConnections()
    {
        UserConnections connections = new();
        PeerConnection first = Connection("Alice");
        PeerConnection second = Connection("Alice", inbound: true);
        connections.Add(first);
        connections.Add(second);

        Assert.Equal("Alice", connections.Remove(second));
        Assert.Same(first, connections.Get("Alice"));

        Assert.Equal("Alice", connections.Remove(first));
        Assert.False(connections.Has("Alice"));
        Assert.False(connections.Contains(first));
    }

    /// <summary>Removing a connection that was never added, or already removed, reports nothing.</summary>
    [Fact]
    public void Remove_UnknownConnection_ReturnsNull()
    {
        UserConnections connections = new();
        PeerConnection connection = Connection("Alice");
        connections.Add(connection);
        connections.Remove(connection);

        Assert.Null(connections.Remove(connection));
        Assert.Null(connections.Remove(Connection("Bob")));
    }

    /// <summary>Adding the same connection twice records it once.</summary>
    [Fact]
    public void Add_SameConnectionTwice_RecordsOnce()
    {
        UserConnections connections = new();
        PeerConnection connection = Connection("Alice");

        connections.Add(connection);
        connections.Add(connection);

        Assert.Single(connections.GetAll("Alice"));
    }

    /// <summary>A connection that has not been identified cannot be recorded.</summary>
    [Fact]
    public void Add_UnidentifiedConnection_Throws()
        => Assert.Throws<ArgumentException>(() => new UserConnections().Add(Connection(null)));

    /// <summary>AddNewlyOnline reports true for a user's first connection, and false for a second one for the same already-online user.</summary>
    [Fact]
    public void AddNewlyOnline_SecondConnectionForSameUser_ReportsFalse()
    {
        UserConnections connections = new();

        Assert.True(connections.AddNewlyOnline(Connection("Alice")));
        Assert.False(connections.AddNewlyOnline(Connection("Alice", inbound: true)));
    }

    /// <summary>Adding the same connection twice reports false the second time, matching Add's own de-duplication.</summary>
    [Fact]
    public void AddNewlyOnline_SameConnectionTwice_ReportsFalseTheSecondTime()
    {
        UserConnections connections = new();
        PeerConnection connection = Connection("Alice");

        Assert.True(connections.AddNewlyOnline(connection));
        Assert.False(connections.AddNewlyOnline(connection));
    }

    /// <summary>RemoveNowOffline reports NowOffline only once a user's last connection is removed, not for one of several.</summary>
    [Fact]
    public void RemoveNowOffline_LastConnection_ReportsNowOffline()
    {
        UserConnections connections = new();
        PeerConnection first = Connection("Alice");
        PeerConnection second = Connection("Alice", inbound: true);
        connections.Add(first);
        connections.Add(second);

        (string? name, bool nowOffline) = connections.RemoveNowOffline(second);
        Assert.Equal("Alice", name);
        Assert.False(nowOffline);

        (name, nowOffline) = connections.RemoveNowOffline(first);
        Assert.Equal("Alice", name);
        Assert.True(nowOffline);
    }

    /// <summary>RemoveNowOffline for a connection never added reports no user and NowOffline false.</summary>
    [Fact]
    public void RemoveNowOffline_UnknownConnection_ReportsNoUser()
    {
        UserConnections connections = new();

        (string? name, bool nowOffline) = connections.RemoveNowOffline(Connection("Alice"));

        Assert.Null(name);
        Assert.False(nowOffline);
    }

    /// <summary>GetUsers lists every distinct user with a recorded connection, and drops one once its last connection is removed.</summary>
    [Fact]
    public void GetUsers_ReflectsCurrentlyRecordedUsers()
    {
        UserConnections connections = new();
        PeerConnection alice = Connection("Alice");
        PeerConnection bob = Connection("Bob");
        connections.Add(alice);
        connections.Add(bob);

        Assert.Equal(["Alice", "Bob"], connections.GetUsers().Order());

        connections.Remove(alice);

        Assert.Equal(["Bob"], connections.GetUsers());
    }
}
