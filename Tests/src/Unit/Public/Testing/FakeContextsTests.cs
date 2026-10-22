namespace BlueHeighliner.Comlink.Tests.Unit.Public.Testing;

/// <summary>Unit tests for the context fakes of <c>BlueHeighliner.Comlink.Testing</c>: each hands out what it was given, follows later changes, and records what a handler does with it.</summary>
public sealed class FakeContextsTests
{
    /// <summary>The delete context hands out its folder, and changing it changes what the context says.</summary>
    [Fact]
    public void FakeDeleteContext_HandsOutItsFolder()
    {
        FakeDeleteContext context = new(FolderType.Notes);
        Assert.Equal(FolderType.Notes, context.Folder);

        context.Folder = FolderType.Drafts;

        Assert.Equal(FolderType.Drafts, ((IDeleteContext)context).Folder);
    }

    /// <summary>The packet create context hands out the piece of the frame it was given, so a packet handler can be tested without the engine.</summary>
    [Fact]
    public void FakeFramePacketCreateContext_HandsOutThePiece()
    {
        TestFrame frame = new() { Body = "x" };
        FakeFramePacketCreateContext<TestFrame> context = new(frame, index: 1, count: 3, frameLength: 30, payload: new byte[] { 1, 2 });

        TestPacket packet = new TestPacketHandler().CreateFramePacket(context);

        Assert.Equal((1, 3, 30), (packet.Index, packet.Count, packet.PayloadLength));
        Assert.Equal([1, 2], packet.Data);
        Assert.Same(frame, context.Frame);
    }

    /// <summary>The draft contexts hand out the draft's aspects, so a draft handler's decisions can be tested without the engine.</summary>
    [Fact]
    public void FakeDraftContexts_HandOutTheDraft()
    {
        FakeDraftContext<TestMessagePriority, TestLevel, TestAspect> draft = new(TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "URGENT", "body", [new AddressRequest { UserName = "BOB" }], 40);
        FakeDraftBlockContext<TestMessagePriority, TestLevel, TestAspect> block = new(TestMessagePriority.Low, tag: "SPAM");

        Assert.Equal("TAG: URGENT", new TestDraftHandler().GetHeader(draft));
        Assert.Equal((TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "body", 40), (draft.Priority, draft.MessageLevel, draft.MessageAspect, draft.Body, draft.LineWidth));
        Assert.Equal("BOB", Assert.Single(draft.Addresses).UserName);
        Assert.Equal((TestMessagePriority.Low, null, null, "SPAM"), (block.Priority, block.MessageLevel, block.MessageAspect, block.Tag));

        block.Tag = "OK";
        Assert.Equal("OK", block.Tag);
    }

    /// <summary>The network event contexts hand out what happened.</summary>
    [Fact]
    public void FakeNetworkContexts_HandOutWhatHappened()
    {
        TestFrame frame = new();
        Message<TestMessagePriority, TestLevel, TestAspect> message = new() { Id = "M1", FromUser = "ALICE", Body = "Hi", Addresses = [], SentAt = DateTime.UtcNow, Priority = TestMessagePriority.Normal };
        RetrievalCriteria criteria = new();

        Assert.Equal("ALICE", new FakeNetworkConnectedContext("ALICE").TargetUser);
        Assert.Equal("ALICE", new FakeNetworkDisconnectedContext("ALICE").TargetUser);
        FakeNetworkReceivedContext<TestFrame> received = new(frame, "ALICE", FrameOrigin.Interface);
        Assert.Equal((frame, "ALICE", FrameOrigin.Interface), (received.Frame, received.SourceUser, received.Origin));
        FakeNetworkSentContext<TestMessagePriority, TestLevel, TestAspect> sent = new(message, new HashSet<string> { "BOB" });
        Assert.Equal(("M1", "BOB"), (sent.Message.Id, Assert.Single(sent.Destinations)));
        Assert.Same(message, new FakeNetworkReadContext<TestMessagePriority, TestLevel, TestAspect>(message).Message);
        FakeNetworkRetrievalContext retrieval = new("SERVER", criteria);
        Assert.Equal(("SERVER", criteria), (retrieval.Server, retrieval.Criteria));
    }

    /// <summary>The handshake contexts record what a handler sends and how it connects and disconnects, accept what is sent unless told otherwise, and finish at once.</summary>
    [Fact]
    public async Task FakeHandshakeContexts_AcceptWhatIsSent_AndRecordTheCalls()
    {
        IpConnectionInfo connection = new() { Host = "10.0.0.1", LocalUser = "ME" };
        FakePacketHandshakeContext<TestPacket> packets = new(connection);
        FakeFrameHandshakeContext<TestFrame> frames = new(connection);
        IPacketHandshakeContext<TestPacket> packetContext = packets;
        IFrameHandshakeContext<TestFrame> frameContext = frames;
        TestPacket packet = new();
        TestFrame frame = new();

        Assert.Same(connection, packetContext.Connection);
        Assert.True(await packetContext.Send(packet));
        await packetContext.Connected("ALICE");
        await packetContext.Disconnect();
        Assert.True(await frameContext.Send(frame));
        await frameContext.Connected("BOB");

        Assert.Same(packet, Assert.Single(packets.Sent));
        Assert.Equal(["ALICE"], packets.ConnectedAs);
        Assert.Equal(1, packets.DisconnectCount);
        Assert.Same(frame, Assert.Single(frames.Sent));
        Assert.Equal(["BOB"], frames.ConnectedAs);

        frames.SendResult = false;
        Assert.False(await frameContext.Send(frame));
    }

    /// <summary>The import context records every addition, so a reader can be tested by what it added.</summary>
    [Fact]
    public async Task FakeImportFormatContext_RecordsEveryAddition()
    {
        FakeImportFormatContext<TestMessagePriority, TestLevel> context = new();
        IImportFormatContext<TestMessagePriority, TestLevel> import = context;
        NoteExportData note = new() { Id = "N1", Body = "n", CreatedAt = DateTime.UtcNow, ModifiedAt = DateTime.UtcNow };

        await import.AddNote(note);

        Assert.Same(note, Assert.Single(context.Notes));
        Assert.Empty(context.Messages);
        Assert.Empty(context.Drafts);
        Assert.Empty(context.StagedSends);
    }
}
