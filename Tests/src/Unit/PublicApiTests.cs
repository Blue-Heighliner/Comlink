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
                "ActivityLogEventEntry", "ActivityLogExportData", "AddressRequest", "AddressType", "ConnectionPoint",
                "DeliveryStatusChangedEvent", "DestinationStatus", "DraftExportData",
                "Engine", "ExternalSystemBase`1", "FolderType", "IConnectionInfo", "IEngineBuilder", "IEngineConfiguration", "IEngineContext", "IExportFormat", "IExternalSystem", "IFrameBuilder`1",
                "IImportFormat", "IImportFormatContext", "IInitialFrameContext`1", "IInitialFrameProcessor`1", "IInitialPacketContext`1", "IInitialPacketProcessor`1", "IIpConnectionInfo", "INetworkConnectedContext`1", "INetworkContext`1", "INetworkDisconnectedContext`1", "INetworkProcessor`1", "INetworkReceivedContext`1", "INetworkSerializer", "IPacketBuilder`1", "IRetrievalBuilder`1", "ISerialConnectionInfo", "IServiceConnection",
                "MessageDeliveryStatus", "MessageExportData", "MessageReceivedEvent", "NoteExportData", "ProtobufNetworkSerializer", "SendMessageResult",
                "StagedSendData", "StagedSendMode", "UserDeliveryResult", "UserIdentity", "UserInfo", "UserRole"
            ],
            exported);
    }
}
