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
                "ActivityLogEventEntry", "ActivityLogExportData", "AddressRequest", "AddressType", "ConnectionMode", "ConnectionPoint", "DeleteContext",
                "DeliveryStatusChangedEvent", "DestinationStatus", "DraftExportData", "DraftState`3", "Engine", "ExternalSystemBase`1", "FolderType", "FrameOrigin",
                "FramePacketCreateContext`1", "FrameSerializer`2", "HdlcRemote", "IAddressTypeBuilder`5", "IAlarmHandler", "IConnectionInfo", "IDeleteHandler", "IDisplayHandler",
                "IDraftHandler`3", "IEngineBuilder", "IEngineBuilder`5", "IEngineConfiguration", "IEngineContext", "IExportFormat", "IExternalSystem", "IFrameBuilder`5",
                "IFrameHandler`4", "IFrameHandshakeContext`1", "IFrameHandshakeHandler`1", "IFrameHeartbeatHandler`2", "IFrameSerializer", "IHdlcBuilder`5", "IImportFormat`2",
                "IImportFormatContext`2", "IIpConnectionInfo", "ILogHandler", "IMessageAspectBuilder`5", "IMessageLevelBuilder`5", "IMsmtBuilder`5", "INetworkConnectedContext`4",
                "INetworkContext`4", "INetworkDisconnectedContext`4", "INetworkReadContext`4", "INetworkReceivedContext`4", "INetworkRetrievalContext`4", "INetworkSentContext`4",
                "IPacketBuilder`5", "IPacketHandler`2", "IPacketHandshakeContext`1", "IPacketHandshakeHandler`1", "IPacketHeartbeatHandler`2", "IPacketSerializer",
                "IPrintHandler`3", "IPriorityLevelBuilder`5", "ISerialConnectionInfo", "IServiceConnection`3", "Message`3", "MessageAddress", "MessageDeliveryStatus",
                "MessageExportData", "NoMessageAspect", "NoMessageLevel", "NoPacket", "NoPriority", "NoteExportData", "PacketSerializer`2", "PooledBufferWriter", "PriorityMode",
                "ProtobufSerializer", "RetrievalCriteria", "SendMessageResult", "StagedSendData`2", "StagedSendMode", "TagCase", "UserIdentity", "UserInfo", "UserLink",
                "UserRole"
            ],
            exported);
    }
}
