namespace BlueHeighliner.Comlink.Tests.Unit.Testing;

/// <summary>Unit tests for the context mocks of <c>BlueHeighliner.Comlink.Testing</c>: each hands out what it was given, follows later changes, and defaults what a handler would act on.</summary>
public sealed class MockContextsTests
{
    private static readonly UserInfo alice = new() { Name = "ALICE" };

    /// <summary>The delete context hands out its folder, and changing it changes what the context says.</summary>
    [Fact]
    public void MockDeleteContext_HandsOutItsFolder()
    {
        MockDeleteContext mock = new(FolderType.Notes);
        Assert.Equal(FolderType.Notes, mock.Object.Folder);

        mock.Folder = FolderType.Drafts;

        Assert.Equal(FolderType.Drafts, mock.Object.Folder);
    }

    /// <summary>The packet create context hands out the piece of the frame it was given, so a packet handler can be tested without the engine.</summary>
    [Fact]
    public void MockFramePacketCreateContext_HandsOutThePiece()
    {
        TestFrame frame = new() { Body = "x" };
        MockFramePacketCreateContext<TestFrame> mock = new(frame, index: 1, count: 3, frameLength: 30, payload: new byte[] { 1, 2 });

        TestPacket packet = new TestPacketHandler().CreateFramePacket(mock.Object);

        Assert.Equal((1, 3, 30), (packet.Index, packet.Count, packet.PayloadLength));
        Assert.Equal([1, 2], packet.Data);
        Assert.Same(frame, mock.Object.Frame);
    }

    /// <summary>The draft contexts hand out the draft's aspects, so a draft handler's decisions can be tested without the engine.</summary>
    [Fact]
    public void MockDraftContexts_HandOutTheDraft()
    {
        MockDraftContext<TestMessagePriority, TestLevel, TestAspect> draft = new(TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "URGENT", "body", [new AddressRequest { UserName = "BOB" }], 40);
        MockDraftBlockContext<TestMessagePriority, TestLevel, TestAspect> block = new(TestMessagePriority.Low, tag: "SPAM");

        Assert.Equal("TAG: URGENT", new TestDraftHandler().GetHeader(draft.Object));
        Assert.Equal((TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "body", 40), (draft.Object.Priority, draft.Object.MessageLevel, draft.Object.MessageAspect, draft.Object.Body, draft.Object.LineWidth));
        Assert.Equal("BOB", Assert.Single(draft.Object.Addresses).UserName);
        Assert.Equal((TestMessagePriority.Low, null, null, "SPAM"), (block.Object.Priority, block.Object.MessageLevel, block.Object.MessageAspect, block.Object.Tag));

        block.Tag = "OK";
        Assert.Equal("OK", block.Object.Tag);
    }

    /// <summary>The network event contexts hand out what happened.</summary>
    [Fact]
    public void MockNetworkContexts_HandOutWhatHappened()
    {
        TestFrame frame = new();
        Message<TestMessagePriority, TestLevel, TestAspect> message = new() { Id = "M1", FromUser = "ALICE", Body = "Hi", Addresses = [], SentAt = DateTime.UtcNow, Priority = TestMessagePriority.Normal };
        RetrievalCriteria criteria = new();

        Assert.Same(alice, new MockNetworkConnectedContext(alice).Object.TargetUser);
        Assert.Same(alice, new MockNetworkDisconnectedContext(alice).Object.TargetUser);
        MockNetworkReceivedContext<TestFrame> received = new(frame, alice, FrameOrigin.Interface);
        Assert.Equal((frame, alice, FrameOrigin.Interface), (received.Object.Frame, received.Object.SourceUser, received.Object.Origin));
        MockNetworkSentContext<TestMessagePriority, TestLevel, TestAspect> sent = new(message, new HashSet<string> { "BOB" });
        Assert.Equal(("M1", "BOB"), (sent.Object.Message.Id, Assert.Single(sent.Object.Destinations)));
        Assert.Same(message, new MockNetworkReadContext<TestMessagePriority, TestLevel, TestAspect>(message).Object.Message);
        MockNetworkRetrievalContext retrieval = new(alice, criteria);
        Assert.Equal((alice, criteria), (retrieval.Object.Server, retrieval.Object.Criteria));
    }

    /// <summary>The handshake contexts report that what a handler sends is accepted and that connecting and disconnecting finish, and record the calls for verifying.</summary>
    [Fact]
    public async Task MockHandshakeContexts_AcceptWhatIsSent_AndRecordTheCalls()
    {
        IpConnectionInfo connection = new() { Host = "10.0.0.1", LocalUser = "ME" };
        MockPacketHandshakeContext<TestPacket> packets = new(connection);
        MockFrameHandshakeContext<TestFrame> frames = new(connection);
        TestPacket packet = new();

        Assert.Same(connection, packets.Object.Connection);
        Assert.True(await packets.Object.Send(packet));
        await packets.Object.Connected("ALICE");
        await packets.Object.Disconnect();
        Assert.True(await frames.Object.Send(new TestFrame()));
        await frames.Object.Connected("BOB");

        packets.Verify(context => context.Send(packet), Times.Once);
        packets.Verify(context => context.Connected("ALICE"), Times.Once);
        packets.Verify(context => context.Disconnect(), Times.Once);
        frames.Verify(context => context.Connected("BOB"), Times.Once);
    }

    /// <summary>The import context accepts every addition and records it, so a reader can be tested by verifying what it added.</summary>
    [Fact]
    public async Task MockImportFormatContext_AcceptsEveryAddition()
    {
        MockImportFormatContext<TestMessagePriority, TestLevel> mock = new();
        NoteExportData note = new() { Id = "N1", Body = "n", CreatedAt = DateTime.UtcNow, ModifiedAt = DateTime.UtcNow };

        await mock.Object.AddNote(note);

        mock.Verify(context => context.AddNote(note), Times.Once);
    }
}
