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
                "ExternalSystemBase`1", "FakeDataPool", "FakeDataStore", "FakeDebugMode", "FakeDeleteContext", "FakeDraftBlockContext`3",
                "FakeDraftContext`3", "FakeFrameHandshakeContext`1", "FakeFramePacketCreateContext`1", "FakeImportFormatContext`2", "FakeLogEntry",
                "FakeLogger", "FakeNetwork`4", "FakeNetworkConnectedContext", "FakeNetworkDisconnectedContext", "FakeNetworkReadContext`3",
                "FakeNetworkReceivedContext`1", "FakeNetworkRetrievalContext", "FakeNetworkSentContext`3", "FakePacketHandshakeContext`1",
                "FakeServiceConnection`4", "FakeUserCatalog`2", "FolderType", "FrameOrigin", "FrameSerializer`2", "GroupInfo", "HdlcRemote",
                "IAddressTypeBuilder`6", "IAlarmHandler", "IAspectBuilder`6", "IConfigurationHandler", "IConnectionInfo", "IDataPool", "IDataStore", "IDebugMode", "IDeleteContext", "IDeleteHandler",
                "IDisplayHandler", "IDraftBlockContext`3", "IDraftContext`3", "IDraftHandler`3", "IEngineBuilder", "IEngineBuilder`6",
                "IEngineConfiguration", "IExportFormat", "IExternalSystem", "IFrameBuilder`6", "IFrameHandler`4", "IFrameHandshakeContext`1",
                "IFrameHandshakeHandler`1", "IFrameHeartbeatHandler`2", "IFramePacketCreateContext`1", "IFrameSerializer", "IHdlcBuilder`6",
                "IImportFormat`2", "IImportFormatContext`2", "IIpConnectionInfo", "ILevelBuilder`6", "ILogger", "ILogHandler", "IMsmtBuilder`6", "INetwork`4", "INetworkConnectedContext", "INetworkDisconnectedContext",
                "INetworkReadContext`3", "INetworkReceivedContext`1", "INetworkRetrievalContext", "INetworkSentContext`3", "IPacketBuilder`6",
                "IPacketHandler`2", "IPacketHandshakeContext`1", "IPacketHandshakeHandler`1", "IPacketHeartbeatHandler`2", "IPacketSerializer",
                "IPrintHandler`3", "IPriorityLevelBuilder`6", "ISerialConnectionInfo", "IServiceConnection`4", "IUserCatalog`2",
                "IUserInfo", "LogCategories", "MemoryOwner`1", "Message`3", "MessageAddress", "MessageDeliveryStatus", "MessageExportData",
                "NoAspect", "NoLevel", "NoPacket", "NoPriority", "NoProfile", "NoteExportData", "PacketSerializer`2", "PooledBufferWriter",
                "PriorityMode", "ProtobufSerializer", "RetrievalCriteria", "SendMessageResult", "SentMessage", "StagedSendData`2", "StagedSendMode",
                "TagCase", "UserIdentity", "UserInfo`2", "UserLink", "UserRole"
            ],
            exported);
    }
}
