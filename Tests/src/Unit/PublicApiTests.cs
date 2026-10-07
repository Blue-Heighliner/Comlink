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
                "ActivityLogEventEntry", "ActivityLogExportData", "AddressRequest", "AddressType", "ConnectionMode", "ConnectionPoint", "DeleteContext", "DeliveryStatusChangedEvent", "DestinationStatus", "DraftExportData",
                "DraftState`3", "Engine", "ExternalSystemBase`1", "FolderType", "FramePacketCreateContext", "FrameSerializer`2", "HdlcRemote", "IAddressTypeBuilder`5", "IAddressTypesBuilder`5", "IAlarmHandler",
                "IAutoForwardController`1", "IConnectionInfo", "IConnectionsBuilder`5", "IDeleteHandler", "IDisplayHandler", "IDraftHandler`3", "IEngineBuilder", "IEngineBuilder`5", "IEngineConfiguration", "IEngineContext",
                "IExportFormat", "IExportsBuilder`5", "IExternalSystem", "IFrameBuilder`5", "IFramePacketHandler`1", "IFrameSerializer", "IHeartbeatHandler`2", "IImportFormat", "IImportFormatContext", "IImportsBuilder`5",
                "IInitialFrameContext`1", "IInitialFrameProcessor`1", "IInitialPacketContext`1", "IInitialPacketProcessor`1", "IIpConnectionInfo", "ILogHandler", "IMessageAspectBuilder`5", "IMessageAspectsBuilder`5",
                "IMessageHandler`4", "IMessageLevelBuilder`5", "IMessageLevelsBuilder`5", "INetworkConnectedContext`1", "INetworkContext`1", "INetworkDisconnectedContext`1", "INetworkProcessor`1", "INetworkReceivedContext`1",
                "IPacketBuilder`5", "IPacketSerializer", "IPrintHandler`1", "IPriorityBuilder`5", "IPriorityLevelBuilder`5", "IReadReceiptHandler`2", "IReceiveReceiptHandler`2", "IRetrievalHandler`2", "ISerialConnectionInfo",
                "IServiceConnection", "MessageCreateContext`3", "MessageDeliveryStatus", "MessageExportData", "MessageReceivedEvent", "MsmtConnectionOptions", "NoMessageAspect", "NoMessageLevel", "NoPacket", "NoPriority",
                "NoteExportData", "PacketSerializer`2", "PooledBufferWriter", "PriorityMode", "ProtobufSerializer", "ReceiptCreateContext", "RetrievalCreateContext", "SendMessageResult", "StagedSendData", "StagedSendMode",
                "TagCase", "UserDeliveryResult", "UserIdentity", "UserInfo", "UserLink", "UserRole"
            ],
            exported);
    }
}
