namespace BlueHeighliner.Comlink.Tests;

/// <summary>
/// An in-memory stand-in for a serial cable between two MicroGate devices. Each end has its own <see cref="IMicroGatePeerFactory"/>;
/// once a peer has been started on both ends they become connected and frames sent on one arrive on the other, as with a real link.
/// A peer is single use, so <see cref="Cut"/> disconnects whichever peers are attached and the next pair started reconnects.
/// </summary>
internal sealed class FakeMicroGateCable
{
    private readonly Lock gate = new();
    private readonly FakeMicroGatePeer?[] attached = new FakeMicroGatePeer?[2];

    /// <summary>Creates a cable whose peers report <paramref name="maxPayloadSize"/> as their largest frame.</summary>
    public FakeMicroGateCable(int maxPayloadSize = 4090)
    {
        MaxPayloadSize = maxPayloadSize;
        EndA = new EndFactory(this, 0);
        EndB = new EndFactory(this, 1);
    }

    /// <summary>Largest payload a frame may carry.</summary>
    public int MaxPayloadSize { get; }

    /// <summary>Factory for the first end.</summary>
    public IMicroGatePeerFactory EndA { get; }

    /// <summary>Factory for the second end.</summary>
    public IMicroGatePeerFactory EndB { get; }

    /// <summary>When set, every <c>Start</c> on any end fails with this exception until it is cleared.</summary>
    public Exception? StartFailure { get; set; }

    /// <summary>Number of peers created on either end so far.</summary>
    public int PeersCreated { get; private set; }

    /// <summary>Disconnects both attached peers, as if the cable were pulled.</summary>
    public void Cut()
    {
        FakeMicroGatePeer?[] peers;
        lock (gate)
        {
            peers = [.. attached];
            Array.Clear(attached);
        }

        foreach (FakeMicroGatePeer? peer in peers) { peer?.Terminate(); }
    }

    private void Attach(int end, FakeMicroGatePeer peer)
    {
        FakeMicroGatePeer? other;
        lock (gate)
        {
            attached[end] = peer;
            other = attached[1 - end];
        }

        if (other is not null)
        {
            peer.MarkConnected();
            other.MarkConnected();
        }
    }

    private void Detach(int end, FakeMicroGatePeer peer)
    {
        lock (gate)
        {
            if (ReferenceEquals(attached[end], peer)) { attached[end] = null; }
        }
    }

    private FakeMicroGatePeer? Other(int end)
    {
        lock (gate) { return attached[1 - end]; }
    }

    private sealed class EndFactory(FakeMicroGateCable cable, int end) : IMicroGatePeerFactory
    {
        public IMicroGatePeer Create()
        {
            cable.PeersCreated++;
            return new FakeMicroGatePeer(cable, end);
        }
    }

    private sealed class FakeMicroGatePeer(FakeMicroGateCable cable, int end) : IMicroGatePeer
    {
        private readonly TestSubject<ReadOnlyMemory<byte>> received = new();
        private readonly TestSubject<MicroGatePeerState> stateChanged = new();
        private readonly TaskCompletionSource connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IObservable<ReadOnlyMemory<byte>> Received => received;

        public IObservable<MicroGatePeerState> StateChanged => stateChanged;

        public MicroGatePeerState State { get; private set; } = MicroGatePeerState.Idle;

        public bool IsConnected => State == MicroGatePeerState.Connected;

        public int MaxPayloadSize => cable.MaxPayloadSize;

        public async ValueTask Start(string portName, MicroGatePeerOptions? options = null, CancellationToken cancellation = default)
        {
            if (cable.StartFailure is { } failure)
            {
                Terminate();
                throw failure;
            }

            SetState(MicroGatePeerState.Connecting);
            cable.Attach(end, this);
            try { await connected.Task.WaitAsync(cancellation); }
            catch (OperationCanceledException)
            {
                Terminate();
                throw;
            }
        }

        public ValueTask Send(ReadOnlyMemory<byte> data, CancellationToken cancellation = default)
        {
            if (!IsConnected) { throw new InvalidOperationException("The peer is not connected."); }
            if (data.Length > MaxPayloadSize) { throw new ArgumentOutOfRangeException(nameof(data)); }

            cable.Other(end)?.received.Publish(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask Send(IMemoryOwner<byte> data, CancellationToken cancellation = default)
        {
            using (data) { return Send(data.Memory, cancellation); }
        }

        public void MarkConnected()
        {
            SetState(MicroGatePeerState.Connected);
            connected.TrySetResult();
        }

        public void Terminate()
        {
            if (State == MicroGatePeerState.Disconnected) { return; }

            FakeMicroGatePeer? remote = cable.Other(end);
            SetState(MicroGatePeerState.Disconnected);
            cable.Detach(end, this);
            connected.TrySetException(new IOException("closed before connected"));
            received.Complete();
            stateChanged.Complete();
            remote?.Terminate();
        }

        public void Dispose() => Terminate();

        public ValueTask DisposeAsync()
        {
            Terminate();
            return ValueTask.CompletedTask;
        }

        private void SetState(MicroGatePeerState state)
        {
            State = state;
            stateChanged.Publish(state);
        }
    }
}
