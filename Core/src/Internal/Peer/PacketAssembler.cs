namespace BlueHeighliner.Comlink;

/// <summary>Reassembles the packets one remote sender sent into whole payloads. Packets may arrive in any order and interleaved with the packets of other payloads, and <see cref="Add"/> may be called concurrently.</summary>
internal interface IPacketAssembler : IDisposable
{
    /// <summary>Adds a received packet to the payload it belongs to.</summary>
    /// <param name="packet">The packet's serialized bytes, exactly as an <see cref="IPacketizer"/> produced them.</param>
    /// <returns>The complete payload and its first packet once its last packet has arrived, the payload being something the caller owns and must dispose; otherwise <see langword="null"/>.</returns>
    /// <exception cref="InvalidDataException">The bytes are not a packet, or contradict the earlier packets of the same payload.</exception>
    /// <exception cref="ObjectDisposedException">This assembler has been disposed.</exception>
    AssembledPayload? Add(ReadOnlyMemory<byte> packet);
}

/// <summary>
/// The standard <see cref="IPacketAssembler"/>. Every chunk of a payload except the last is the same size, so the
/// chunks tile the payload in index order and need no offsets. Received chunks are held as they arrive, and the
/// payload is put together into one pooled buffer only once the last one is in, so what a partly received payload
/// costs is what its sender has actually sent, however much length it announced. Even so, partly received payloads
/// never wait for their missing packets forever: once more than a set number are pending, or they hold more than twice
/// the payload limit between them, the oldest are dropped, which bounds the memory a lost packet, or a sender that
/// never finishes, can tie up.
/// </summary>
internal sealed class PacketAssembler(IEngineController engineController, int maxPayloadSize, int maxPendingPayloads) : IPacketAssembler
{
    private readonly Lock gate = new();
    private readonly OrderedDictionary<int, Pending> pending = [];
    private readonly IPacketSerializer serializer = engineController.PacketSerializer ?? throw new InvalidOperationException("The engine controller has no packet serializer");
    private readonly long maxPendingBytes = 2L * maxPayloadSize;
    private long pendingBytes;
    private bool disposed;

    /// <inheritdoc />
    public AssembledPayload? Add(ReadOnlyMemory<byte> packet)
    {
        object decoded = serializer.Deserialize(packet);
        if (decoded.GetType() == engineController.PacketType && engineController.IsPacketHeartbeat(decoded))
        {
            return null;
        }
        if (decoded.GetType() != engineController.PacketType || !engineController.IsFramePacket(decoded))
        {
            throw new InvalidDataException("The bytes are not a frame packet");
        }

        int id = engineController.GetPayloadId(decoded);
        int index = engineController.GetPacketIndex(decoded);
        int count = engineController.GetPacketCount(decoded);
        int total = engineController.GetPayloadLength(decoded);
        ReadOnlySpan<byte> chunk = engineController.GetPacketData(decoded).Span;

        bool isLast = index == count - 1;
        if (count < 1 || count > Packetizer.MaxPacketCount || index < 0 || index >= count || total < 0 || total > maxPayloadSize || chunk.Length > total || (!isLast && chunk.Length == 0))
        {
            throw new InvalidDataException("The packet's fields are inconsistent");
        }

        if (!isLast && ((long)index + 1) * chunk.Length > total)
        {
            throw new InvalidDataException("The packet lies beyond the end of its payload");
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (count == 1)
            {
                if (chunk.Length != total)
                {
                    throw new InvalidDataException("A single packet must hold its whole payload");
                }

                PooledMemoryOwner whole = PooledMemoryOwner.Rent(total);
                chunk.CopyTo(whole.Memory.Span);
                return new AssembledPayload(whole, decoded);
            }

            if (!pending.TryGetValue(id, out Pending? payload))
            {
                payload = new Pending(count, total);
                pending.Add(id, payload);
                while (pending.Count > maxPendingPayloads) { EvictOldest(except: id); }
            }
            else if (payload.Count != count || payload.Total != total)
            {
                throw new InvalidDataException("The packet contradicts the earlier packets of its payload");
            }

            if (payload.Chunks[index] is not null)
            {
                return null;
            }

            if (!isLast)
            {
                if (payload.ChunkSize != 0 && payload.ChunkSize != chunk.Length)
                {
                    throw new InvalidDataException("The packets of a payload differ in size");
                }

                payload.ChunkSize = chunk.Length;
            }

            PooledMemoryOwner held = PooledMemoryOwner.Rent(chunk.Length);
            chunk.CopyTo(held.Memory.Span);
            payload.Chunks[index] = held;
            if (index == 0)
            {
                payload.FirstPacket = decoded;
            }
            payload.ReceivedCount++;
            payload.ReceivedBytes += chunk.Length;
            pendingBytes += chunk.Length;
            while (pendingBytes > maxPendingBytes && pending.Count > 1) { EvictOldest(except: id); }

            if (payload.ReceivedCount < count)
            {
                return null;
            }

            pending.Remove(id);
            pendingBytes -= payload.ReceivedBytes;
            try
            {
                if (payload.ReceivedBytes != total)
                {
                    throw new InvalidDataException("The packets of a payload do not add up to its length");
                }

                PooledMemoryOwner whole = PooledMemoryOwner.Rent(total);
                int offset = 0;
                foreach (PooledMemoryOwner? part in payload.Chunks)
                {
                    part!.Memory.Span.CopyTo(whole.Memory.Span[offset..]);
                    offset += part.Memory.Length;
                }

                return new AssembledPayload(whole, payload.FirstPacket ?? decoded);
            }
            finally
            {
                payload.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (KeyValuePair<int, Pending> entry in pending)
            {
                entry.Value.Dispose();
            }
            pending.Clear();
            pendingBytes = 0;
        }
    }

    // Never the payload a packet is being added to, which is still in use.
    private void EvictOldest(int except)
    {
        int position = pending.GetAt(0).Key == except ? 1 : 0;
        Pending evicted = pending.GetAt(position).Value;
        pending.RemoveAt(position);
        pendingBytes -= evicted.ReceivedBytes;
        evicted.Dispose();
    }

    private sealed class Pending(int count, int total) : IDisposable
    {
        public int Count { get; } = count;
        public int Total { get; } = total;
        public PooledMemoryOwner?[] Chunks { get; } = new PooledMemoryOwner?[count];
        public object? FirstPacket { get; set; }
        public int ChunkSize { get; set; }
        public int ReceivedCount { get; set; }
        public int ReceivedBytes { get; set; }

        public void Dispose()
        {
            foreach (PooledMemoryOwner? chunk in Chunks)
            {
                chunk?.Dispose();
            }
        }
    }
}
