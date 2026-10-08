namespace BlueHeighliner.Comlink;

/// <summary>
/// Hands the packets queued for one connection to the wrapped transport, at most a window's worth at a time, always
/// the highest-priority queued packet next and first come, first served among equals. Holding the rest back here
/// rather than queueing them all on the wrapped transport is what lets a higher-priority payload sent while a
/// lower-priority one is still being transmitted go out right after the packets already in flight, on any
/// transport: those cannot be recalled, but the remaining lower-priority packets wait until nothing more urgent is
/// queued. A wider window keeps more packets in flight at once, which suits a link with a long round trip, at the
/// cost of that many packets a more urgent payload can end up waiting behind.
/// </summary>
internal sealed class PacketScheduler(IPeerTransport transport, PeerConnection connection, int window) : IDisposable
{
    private readonly Lock gate = new();
    private readonly PriorityQueue<Item, (long NegatedPriority, long Sequence)> queue = new();
    private long sequence;
    private int workers;
    private bool disposed;

    /// <summary>Queues <paramref name="packet"/> to be sent over the connection.</summary>
    /// <param name="packet">The packet, which must stay valid until the returned task completes.</param>
    /// <param name="transmitted">Invoked once the packet has been handed to the remote end.</param>
    /// <param name="payload">The payload the packet belongs to; once one of its packets fails the rest are not sent.</param>
    /// <param name="cancellation">Cancels the packet while it is still queued, and is passed on to the wrapped transport once it is being sent.</param>
    /// <returns>A task that completes once the packet has been sent and acknowledged, rejected, failed, cancelled while queued, or dropped.</returns>
    public Task<bool> Enqueue(Packet packet, Action? transmitted, Payload payload, CancellationToken cancellation)
    {
        Item item = new(packet, transmitted, payload, cancellation);

        // Registered before the packet is queued, so the pump can never finish with it before its registration exists to
        // be disposed. A token that is already cancelled cancels it on the spot, and the pump skips it.
        if (cancellation.CanBeCanceled)
        {
            item.Registration = cancellation.Register(() => Cancel(item));
        }

        lock (gate)
        {
            if (disposed)
            {
                item.Result.TrySetException(new IOException("The transport was disposed"));
                item.Registration.Dispose();
                return item.Result.Task;
            }

            queue.Enqueue(item, (-(long)packet.Priority, sequence++));
            if (workers < window)
            {
                workers++;
                _ = Task.Run(Pump);
            }
        }

        return item.Result.Task;
    }

    /// <summary>Fails every packet still queued. The one being sent, if any, is left to finish.</summary>
    public void Dispose()
    {
        List<Item> dropped = [];
        lock (gate)
        {
            disposed = true;
            while (queue.TryDequeue(out Item? item, out _)) { dropped.Add(item); }
        }

        foreach (Item item in dropped)
        {
            item.Result.TrySetException(new IOException("The transport was disposed"));
            item.Registration.Dispose();
        }
    }

    private async Task Pump()
    {
        while (true)
        {
            Item? item;
            lock (gate)
            {
                item = Next();
                if (item is null)
                {
                    workers--;
                    return;
                }
            }

            await Send(item);
        }
    }

    private Item? Next()
    {
        while (queue.TryDequeue(out Item? item, out _))
        {
            if (item.Result.Task.IsCompleted)
            {
                continue;
            }

            if (item.Payload.IsFailed)
            {
                item.Result.TrySetResult(false);
                item.Registration.Dispose();
                continue;
            }

            item.Started = true;
            return item;
        }

        return null;
    }

    private async Task Send(Item item)
    {
        try
        {
            bool accepted = await transport.Request(connection, item.Packet.Data.Memory, new PeerSendOptions { Priority = item.Packet.Priority, Transmitted = item.Transmitted }, item.Cancellation);
            if (!accepted)
            {
                item.Payload.Fail();
            }
            item.Result.TrySetResult(accepted);
        }
        catch (Exception ex)
        {
            item.Payload.Fail();
            item.Result.TrySetException(ex);
        }
        finally
        {
            item.Registration.Dispose();
        }
    }

    // A packet being sent is left alone: its memory is still in use, and the wrapped transport sees the token itself.
    private void Cancel(Item item)
    {
        lock (gate)
        {
            if (item.Started)
            {
                return;
            }
        }

        item.Result.TrySetCanceled(item.Cancellation);
    }

    /// <summary>Tracks whether any packet of one payload has failed, so the rest of it is not sent for nothing.</summary>
    internal sealed class Payload
    {
        private bool failed;

        /// <summary>Gets a value indicating whether a packet of this payload has failed.</summary>
        public bool IsFailed => Volatile.Read(ref failed);

        /// <summary>Marks the payload as failed.</summary>
        public void Fail() => Volatile.Write(ref failed, true);
    }

    private sealed class Item(Packet packet, Action? transmitted, Payload payload, CancellationToken cancellation)
    {
        public Packet Packet { get; } = packet;
        public Action? Transmitted { get; } = transmitted;
        public Payload Payload { get; } = payload;
        public CancellationToken Cancellation { get; } = cancellation;
        public TaskCompletionSource<bool> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenRegistration Registration { get; set; }
        public bool Started { get; set; }
    }
}
