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
                "ActivityLogEventEntry", "ActivityLogExportData", "AddressRequest", "AddressType", "BiDictionary`2", "Bit", "BitEndianness",
                "ConnectionMode", "ConnectionPoint", "DeliveryStatusChangedEvent", "DestinationStatus", "DraftExportData", "Engine",
                "ExternalSystemBase`1", "FolderType", "FrameOrigin", "FrameSerializer`2", "GroupInfo", "HdlcRemote", "IAddressTypeBuilder`5",
                "IAlarmHandler", "IConnectionInfo", "IDataPool", "IDebugMode", "IDeleteContext", "IDeleteHandler", "IDisplayHandler",
                "IDraftBlockContext`3", "IDraftContext`3", "IDraftHandler`3", "IEngineBuilder", "IEngineBuilder`5", "IEngineConfiguration",
                "IExportFormat", "IExternalSystem", "IFrameBuilder`5", "IFrameHandler`4", "IFrameHandshakeContext`1", "IFrameHandshakeHandler`1",
                "IFrameHeartbeatHandler`2", "IFramePacketCreateContext`1", "IFrameSerializer", "IHdlcBuilder`5", "IImportFormat`2",
                "IImportFormatContext`2", "IIpConnectionInfo", "ILogger", "ILogHandler", "IMessageAspectBuilder`5", "IMessageLevelBuilder`5",
                "IMsmtBuilder`5", "INetwork`4", "INetworkConnectedContext", "INetworkDisconnectedContext", "INetworkReadContext`3",
                "INetworkReceivedContext`1", "INetworkRetrievalContext", "INetworkSentContext`3", "IPacketBuilder`5", "IPacketHandler`2",
                "IPacketHandshakeContext`1", "IPacketHandshakeHandler`1", "IPacketHeartbeatHandler`2", "IPacketSerializer", "IPrintHandler`3",
                "IPriorityLevelBuilder`5", "IReconfigurer", "ISerialConnectionInfo", "IServiceConnection`3", "IUserCatalog", "LogCategories",
                "MemoryOwner`1", "Message`3", "MessageAddress", "MessageDeliveryStatus", "MessageExportData", "NoMessageAspect", "NoMessageLevel",
                "NoPacket", "NoPriority", "NoteExportData", "PacketSerializer`2", "PooledBufferWriter", "PriorityMode", "ProtobufSerializer",
                "RetrievalCriteria", "SendMessageResult", "StagedSendData`2", "StagedSendMode", "TagCase", "UserIdentity", "UserInfo", "UserLink",
                "UserRole"
            ],
            exported);
    }
}
