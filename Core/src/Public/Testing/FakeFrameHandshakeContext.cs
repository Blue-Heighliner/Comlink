namespace BlueHeighliner.Comlink.Testing;

/// <summary>
/// A stand-in for the <see cref="IFrameHandshakeContext{TFrame}"/> a frame handshake handler is handed, to test a handshake without a connection. Whatever the handler sends is recorded in <see cref="Sent"/> and
/// accepted unless <see cref="SendResult"/> says otherwise; <see cref="Accept"/> and <see cref="Reject"/> are recorded too and finish at once.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="connection">The connection the handshake is on.</param>
public sealed class FakeFrameHandshakeContext<TFrame>(IConnectionInfo connection) : IFrameHandshakeContext<TFrame> where TFrame : class
{
    /// <summary>Gets every frame the handler sent, in order.</summary>
    public List<TFrame> Sent { get; } = [];

    /// <summary>Gets the user name of each call to <see cref="Accept"/>, in order.</summary>
    public List<string> Accepted { get; } = [];

    /// <summary>Gets the reason of each call to <see cref="Reject"/>, in order.</summary>
    public List<string> Rejected { get; } = [];

    /// <summary>Gets or sets whether the other node accepts what is sent, which <c>Send</c> reports. Defaults to <see langword="true"/>.</summary>
    public bool SendResult { get; set; } = true;

    /// <inheritdoc />
    public IConnectionInfo Connection { get; set; } = connection;

    /// <inheritdoc />
    public Task Accept(string userName)
    {
        Accepted.Add(userName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Reject(string reason)
    {
        Rejected.Add(reason);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> Send(TFrame frame)
    {
        Sent.Add(frame);
        return Task.FromResult(SendResult);
    }
}
