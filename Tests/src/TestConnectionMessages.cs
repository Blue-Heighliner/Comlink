namespace BlueHeighliner.Comlink.Tests;

/// <summary>A test DTO that is deliberately not the configured message type, used where a foreign type must be rejected.</summary>
[ProtoContract]
public sealed class TestHello
{
    /// <summary>The user name the sender claims.</summary>
    [ProtoMember(1)] public string Name { get; set; } = string.Empty;
}
