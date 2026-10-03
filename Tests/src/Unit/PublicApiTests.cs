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
                "ActivityLogEventEntry", "ActivityLogExportData", "AddressRequest", "AddressType", "ConnectionMode", "ConnectionPoint",
                "DeliveryStatusChangedEvent", "DestinationStatus", "DraftExportData",
                "Engine", "ExternalSystemBase`1", "FolderType", "FramePacketCreateContext", "FrameSerializer`2", "IAutoForwardController`1", "IConnectionInfo", "IEngineBuilder", "IEngineConfiguration", "IEngineContext", "IExportFormat", "IExternalSystem", "IFrameBuilder`1", "IFramePacketHandler`1", "IFrameSerializer",
                "IImportFormat", "IImportFormatContext", "IInitialFrameContext`1", "IInitialFrameProcessor`1", "IInitialPacketContext`1", "IInitialPacketProcessor`1", "IIpConnectionInfo", "IMessageHandler`1", "INetworkConnectedContext`1", "INetworkContext`1", "INetworkDisconnectedContext`1", "INetworkProcessor`1", "INetworkReceivedContext`1", "IPacketBuilder`1", "IPacketSerializer", "IReadReceiptHandler`1", "IReceiveReceiptHandler`1", "IRetrievalHandler`1", "ISerialConnectionInfo", "IServiceConnection",
                "MessageCreateContext", "MessageDeliveryStatus", "MessageExportData", "MessageReceivedEvent", "MsmtConnectionOptions", "NoteExportData", "PacketSerializer`2", "PeerPoint", "PooledBufferWriter", "ProtobufSerializer", "ReceiptCreateContext", "RetrievalCreateContext", "SendMessageResult",
                "StagedSendData", "StagedSendMode", "UserDeliveryResult", "UserIdentity", "UserInfo", "UserLink", "UserRole"
            ],
            exported);
    }
}
