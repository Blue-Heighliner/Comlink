namespace BlueHeighliner.Comlink.Tests;

/// <summary>
/// An in-memory stand-in for a serial cable between two MicroGate devices. Each end has its own <see cref="IHdlcPeerFactory"/>;
/// once a peer has been started and connected on both ends they become connected and frames sent on one arrive on the other, as with a real link.
/// A peer is single use, so <see cref="Cut"/> disconnects whichever peers are attached and the next pair started reconnects.
/// </summary>
internal sealed class FakeHdlcCable
{
    private readonly Lock gate = new();
    private readonly FakeHdlcPeer?[] attached = new FakeHdlcPeer?[2];

    /// <summary>Creates a cable whose peers report <paramref name="maxPayloadSize"/> as their largest frame.</summary>
    public FakeHdlcCable(int maxPayloadSize = 4090)
    {
        MaxPayloadSize = maxPayloadSize;
        EndA = new EndFactory(this, 0);
        EndB = new EndFactory(this, 1);
    }

    /// <summary>Largest payload a frame may carry.</summary>
    public int MaxPayloadSize { get; }

    /// <summary>Factory for the first end.</summary>
    public IHdlcPeerFactory EndA { get; }

    /// <summary>Factory for the second end.</summary>
    public IHdlcPeerFactory EndB { get; }

    /// <summary>When set, every <c>Start</c> on any end fails with this exception until it is cleared.</summary>
    public Exception? StartFailure { get; set; }

    /// <summary>When set, two ends only connect when each one's remote address is the other's own address, as on a real link; otherwise any two ends connect.</summary>
    public bool EnforcesAddresses { get; set; }

    /// <summary>Number of peers created on either end so far.</summary>
    public int PeersCreated { get; private set; }

    /// <summary>The (local address, remote address, options) every <c>Connect</c> on any end was given, in order.</summary>
    public List<(byte Address, byte RemoteAddress, HdlcPeerOptions Options)> Starts { get; } = [];

    /// <summary>Disconnects both attached peers, as if the cable were pulled.</summary>
    public void Cut()
    {
        FakeHdlcPeer?[] peers;
        lock (gate)
        {
            peers = [.. attached];
            Array.Clear(attached);
        }

        foreach (FakeHdlcPeer? peer in peers)
        {
            peer?.Terminate();
        }
    }

    private void Attach(int end, FakeHdlcPeer peer)
    {
        FakeHdlcPeer? other;
        lock (gate)
        {
            attached[end] = peer;
            other = attached[1 - end];
        }

        if (other is not null && (!EnforcesAddresses || (peer.Remote == other.Local && other.Remote == peer.Local)))
        {
            peer.MarkConnected();
            other.MarkConnected();
        }
    }

    private void Detach(int end, FakeHdlcPeer peer)
    {
        lock (gate)
        {
            if (ReferenceEquals(attached[end], peer))
            {
                attached[end] = null;
            }
        }
    }

    private FakeHdlcPeer? Other(int end)
    {
        lock (gate) { return attached[1 - end]; }
    }

    private sealed class EndFactory(FakeHdlcCable cable, int end) : IHdlcPeerFactory
    {
        public IHdlcPeer Create()
        {
            cable.PeersCreated++;
            return new FakeHdlcPeer(cable, end);
        }
    }

    private sealed class FakeHdlcPeer(FakeHdlcCable cable, int end) : IHdlcPeer
    {
        private readonly TestSubject<Exception> exceptions = new();
        private readonly TestSubject<HdlcFrame> monitored = new();
        private readonly TestSubject<HdlcFrame> transmitted = new();
        private readonly TestSubject<HdlcPeerState> stateChanged = new();
        private readonly TestSubject<MicroGateSignals> signalsChanged = new();
        private readonly TaskCompletionSource connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private HdlcPeerOptions options = new();

        public byte Local { get; private set; }

        public byte Remote { get; private set; }

        public Action<IMemoryOwner<byte>>? Receiver { get; set; }

        public IObservable<HdlcFrame> Monitored => monitored;

        public IObservable<HdlcFrame> Transmitted => transmitted;

        public IObservable<Exception> Exceptions => exceptions;

        public IObservable<HdlcPeerState> StateChanged => stateChanged;

        public IObservable<MicroGateSignals> SignalsChanged => signalsChanged;

        public MicroGateSignals Signals => MicroGateSignals.None;

        public HdlcPeerState State { get; private set; } = HdlcPeerState.Idle;

        public bool IsConnected => State is HdlcPeerState.Connected;

        public int MaxPayloadSize => cable.MaxPayloadSize;

        public ValueTask Start(string portName, HdlcPeerOptions? options = null, CancellationToken cancellation = default)
        {
            this.options = options ?? new();
            if (cable.StartFailure is { } failure)
            {
                Terminate();
                throw failure;
            }

            SetState(HdlcPeerState.Ready);
            return ValueTask.CompletedTask;
        }

        public async ValueTask Connect(byte address, byte remoteAddress, CancellationToken cancellation = default)
        {
            lock (cable.gate) { cable.Starts.Add((address, remoteAddress, options)); }
            Local = address;
            Remote = remoteAddress;

            SetState(HdlcPeerState.Connecting);
            cable.Attach(end, this);
            try { await connected.Task.WaitAsync(cancellation); }
            catch (OperationCanceledException)
            {
                Terminate();
                throw;
            }
        }

        public ValueTask Forward(ReadOnlyMemory<byte> frame, CancellationToken cancellation = default) => throw new NotSupportedException();

        public ValueTask Send(ReadOnlyMemory<byte> data, CancellationToken cancellation = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("The peer is not connected.");
            }
            if (data.Length > MaxPayloadSize)
            {
                throw new ArgumentOutOfRangeException(nameof(data));
            }

            cable.Other(end)?.Receiver?.Invoke(new TestOwner(data.ToArray()));
            return ValueTask.CompletedTask;
        }

        public ValueTask Send(IMemoryOwner<byte> data, CancellationToken cancellation = default)
        {
            using (data) { return Send(data.Memory, cancellation); }
        }

        public void MarkConnected()
        {
            SetState(HdlcPeerState.Connected);
            connected.TrySetResult();
        }

        public void Terminate()
        {
            if (State is HdlcPeerState.Disconnected)
            {
                return;
            }

            FakeHdlcPeer? remote = cable.Other(end);
            SetState(HdlcPeerState.Disconnected);
            cable.Detach(end, this);
            connected.TrySetException(new IOException("closed before connected"));
            exceptions.Complete();
            monitored.Complete();
            transmitted.Complete();
            stateChanged.Complete();
            signalsChanged.Complete();
            remote?.Terminate();
        }

        public void Dispose() => Terminate();

        public ValueTask Drop()
        {
            Terminate();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Terminate();
            return ValueTask.CompletedTask;
        }

        private void SetState(HdlcPeerState state)
        {
            State = state;
            stateChanged.Publish(state);
        }
    }
}
