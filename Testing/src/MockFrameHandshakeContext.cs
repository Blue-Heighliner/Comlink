namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IFrameHandshakeContext{TFrame}"/> a handshake handler is handed for one connection.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public sealed class MockFrameHandshakeContext<TFrame> : Mock<IFrameHandshakeContext<TFrame>> where TFrame : class
{
    /// <summary>Initializes the mock. <c>Send</c> reports that the other node accepted what was sent, and <c>Connected</c> and <c>Disconnect</c> complete at once; verify them to see what a handler did.</summary>
    /// <param name="connection">What is known about the connection.</param>
    public MockFrameHandshakeContext(IConnectionInfo connection)
    {
        Connection = connection;
        Setup(context => context.Connection).Returns(() => Connection);
        Setup(context => context.Connected(It.IsAny<string>())).Returns(Task.CompletedTask);
        Setup(context => context.Disconnect()).Returns(Task.CompletedTask);
        Setup(context => context.Send(It.IsAny<TFrame>())).ReturnsAsync(true);
    }

    /// <summary>Gets or sets what <c>Connection</c> returns.</summary>
    public IConnectionInfo Connection { get; set; }
}
