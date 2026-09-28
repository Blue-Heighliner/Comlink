namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test connection message DTO: what a node says about itself when it opens a connection.</summary>
[ProtoContract]
public sealed class TestHello
{
    /// <summary>The user name the sender claims.</summary>
    [ProtoMember(1)] public string Name { get; set; } = string.Empty;
}

/// <summary>Test connection response DTO: what a node answers a <see cref="TestHello"/> with.</summary>
[ProtoContract]
public sealed class TestWelcome
{
    /// <summary>The user name the responder claims.</summary>
    [ProtoMember(1)] public string Name { get; set; } = string.Empty;

    /// <summary>An app-specific number the responder attaches to itself.</summary>
    [ProtoMember(2)] public int Station { get; set; }
}
