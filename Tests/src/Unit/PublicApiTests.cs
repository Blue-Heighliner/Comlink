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
                "DeleteContext", "DeliveryStatusChangedEvent", "DestinationStatus", "DraftExportData", "DraftState`2",
                "Engine", "ExternalSystemBase`1", "FolderType", "FramePacketCreateContext", "FrameSerializer`2", "HdlcRemote", "IAddressTypeBuilder`4", "IAddressTypesBuilder`4", "IAlarmHandler", "IAutoForwardController`1", "IConnectionInfo", "IConnectionsBuilder`4", "IDeleteHandler", "IDisplayHandler", "IDraftHandler`2", "IEngineBuilder", "IEngineBuilder`4", "IEngineConfiguration", "IEngineContext", "IExportFormat", "IExportsBuilder`4", "IExternalSystem", "IFrameBuilder`4", "IFramePacketHandler`1", "IFrameSerializer", "IHeartbeatHandler`2",
                "IImportFormat", "IImportFormatContext", "IImportsBuilder`4", "IInitialFrameContext`1", "IInitialFrameProcessor`1", "IInitialPacketContext`1", "IInitialPacketProcessor`1", "IIpConnectionInfo", "ILogHandler", "IMessageHandler`3", "INetworkConnectedContext`1", "INetworkContext`1", "INetworkDisconnectedContext`1", "INetworkProcessor`1", "INetworkReceivedContext`1", "IPacketBuilder`4", "IPacketSerializer", "IPrintHandler`1", "IPriorityBuilder`4", "IPriorityLevelBuilder`4", "IReadReceiptHandler`2", "IReceiveReceiptHandler`2", "IRetrievalHandler`2", "ISecurityLevelBuilder`4", "ISecurityLevelsBuilder`4", "ISerialConnectionInfo", "IServiceConnection",
                "MessageCreateContext`2", "MessageDeliveryStatus", "MessageExportData", "MessageReceivedEvent", "MsmtConnectionOptions", "NoPacket", "NoPriority", "NoSecurityLevel", "NoteExportData", "PacketSerializer`2", "PooledBufferWriter", "PriorityMode", "ProtobufSerializer", "ReceiptCreateContext", "RetrievalCreateContext", "SendMessageResult",
                "StagedSendData", "StagedSendMode", "TagCase", "UserDeliveryResult", "UserIdentity", "UserInfo", "UserLink", "UserRole"
            ],
            exported);
    }
}
