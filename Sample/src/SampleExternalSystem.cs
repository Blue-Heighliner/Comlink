namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="ExternalSystemBase{TMessage}"/> demonstrating the external-system conduit pattern —
/// see <see cref="SampleEngineController.GetExternalSystems"/> and <c>Docs/ExternalSystems.md</c>. Not a
/// real integration: "connecting" is simulated with a short delay, and once connected it stays connected
/// and periodically synthesizes an inbound demo message, so the receive path — including mirroring to
/// every other external system, and normal processing as a received message — is visible without needing
/// an actual external system to connect to. Never loses its (simulated) connection once established, so
/// it has nothing to poll for and leaves <see cref="ExternalSystemBase{TMessage}.PollIsConnected"/> at its
/// default rather than overriding it — a real implementation that needs active polling, or that instead
/// learns about disconnection through an event or callback and calls
/// <see cref="ExternalSystemBase{TMessage}.ReportDisconnected"/>, would do so here. A real host
/// implementation would also replace <see cref="TryConnect"/>, <see cref="Disconnect"/>, and
/// <see cref="Send"/> with genuine connection logic for its own external system (a socket, a message
/// queue, an HTTP long-poll, etc.).
/// </summary>
/// <param name="name">A short, human-readable name identifying this external system.</param>
public sealed class SampleExternalSystem() : ExternalSystemBase<SampleMessage>("EXTERNAL")
{
    private static readonly TimeSpan ConnectDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DemoMessageInterval = TimeSpan.FromSeconds(30);

    private CancellationTokenSource? demoMessageLoopCts;

    /// <inheritdoc />
    protected override async Task<bool> TryConnect(CancellationToken cancellation)
    {
        // Simulates the latency a real connection attempt (a socket handshake, an auth exchange, ...)
        // would have.
        await Task.Delay(ConnectDelay, cancellation);

        demoMessageLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _ = Task.Run(() => RunDemoMessageLoop(demoMessageLoopCts.Token), CancellationToken.None);
        return true;
    }

    /// <inheritdoc />
    protected override Task Disconnect()
    {
        demoMessageLoopCts?.Cancel();
        demoMessageLoopCts = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task<bool> Send(SampleMessage message) => Task.FromResult(true);

    private async Task RunDemoMessageLoop(CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await Task.Delay(DemoMessageInterval, cancellation);
                await Receive(new SampleMessage
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Sender = Name,
                    Title = "Message from external system",
                    Text = $"This is a demo message synthesized by {Name} to show the receive path.",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
