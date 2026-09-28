namespace BlueHeighliner.Comlink.Tests.Unit;

/// <summary>Guards the size of the package's public surface: only what a host needs to configure, start and talk to the engine is public.</summary>
public sealed class PublicApiTests
{
    /// <summary>The exported types are exactly the host-facing API, so nothing else becomes public by accident.</summary>
    [Fact]
    public void ExportedTypes_AreExactlyTheHostFacingApi()
    {
        string[] exported = [.. typeof(Engine).Assembly.GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith("CompiledAvaloniaXaml") != true)
            .Select(type => type.Name)
            .Order()];

        Assert.Equal(
            [
                "AddressRequest", "AddressType", "ConnectionInfo", "ConnectionPoint", "DeliveryStatusChangedEvent", "DestinationStatus",
                "Engine", "ExternalSystemBase`1", "FolderType", "IEngineBuilder", "IEngineConfiguration", "IExternalSystem",
                "IMessageBuilder`1", "INetworkSerializer", "IPacketBuilder`1", "IServiceConnection",
                "MessageReceivedEvent", "NodeRole", "ProtobufNetworkSerializer", "SendMessageResult",
                "UserDeliveryResult", "UserIdentity", "UserInfo"
            ],
            exported);
    }
}
