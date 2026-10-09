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
                "ActivityLogEventEntry", "ActivityLogExportData", "AddressRequest", "AddressType", "ConnectionMode", "ConnectionPoint", "DeleteContext", "DeliveryStatusChangedEvent",
                "DestinationStatus", "DraftExportData", "DraftState`3", "Engine", "ExternalSystemBase`1", "FolderType", "FrameOrigin", "FramePacketCreateContext", "FrameSerializer`2", "HdlcRemote",
                "IAddressTypeBuilder`5", "IAddressTypesBuilder`5", "IAlarmHandler", "IConnectionInfo", "IConnectionsBuilder`5", "IDeleteHandler", "IDisplayHandler", "IDraftHandler`3",
                "IEngineBuilder", "IEngineBuilder`5", "IEngineConfiguration", "IEngineContext", "IExportFormat", "IExportsBuilder`5", "IExternalSystem", "IFrameBuilder`5", "IFramePacketHandler`1",
                "IFrameSerializer", "IHandshakeContext`1", "IHandshakeProcessor`1", "IHeartbeatHandler`2", "IImportFormat`2", "IImportFormatContext`2", "IImportsBuilder`5", "IIpConnectionInfo",
                "ILogHandler", "IMessageAspectBuilder`5", "IMessageAspectsBuilder`5", "IMessageLevelBuilder`5", "IMessageLevelsBuilder`5", "INetworkConnectedContext`4", "INetworkContext`4",
                "INetworkDisconnectedContext`4", "INetworkProcessor`4", "INetworkReadContext`4", "INetworkReceivedContext`4", "INetworkRetrievalContext`4", "INetworkSentContext`4", "IPacketBuilder`5",
                "IPacketSerializer", "IPrintHandler`3", "IPriorityBuilder`5", "IPriorityLevelBuilder`5", "ISerialConnectionInfo", "IServiceConnection`3", "Message`3", "MessageAddress",
                "MessageDeliveryStatus", "MessageExportData", "MsmtConnectionOptions", "NoMessageAspect", "NoMessageLevel", "NoPacket", "NoPriority", "NoteExportData", "PacketSerializer`2",
                "PooledBufferWriter", "PriorityMode", "ProtobufSerializer", "RetrievalCriteria", "SendMessageResult", "StagedSendData`2", "StagedSendMode", "TagCase", "UserIdentity", "UserInfo",
                "UserLink", "UserRole"
            ],
            exported);
    }
}
