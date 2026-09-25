namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// The message framing carried inside each HDLC information frame of a serial link. HDLC gives an ordered,
/// gap-free byte stream per frame but caps a frame at a few kilobytes and knows nothing about messages, so a
/// message is split into numbered fragments and answered with a reply frame carrying the remote node's accept or reject.
/// </summary>
internal readonly record struct SerialFrame(SerialFrameKind Kind, uint Id, ushort Index, ushort Count, bool Success, ReadOnlyMemory<byte> Chunk)
{
    /// <summary>Bytes of header in a data frame.</summary>
    public static int DataHeaderSize { get; } = 9;

    /// <summary>Bytes in a reply frame.</summary>
    public static int ReplySize { get; } = 6;

    /// <summary>Builds a data frame carrying fragment <paramref name="index"/> of <paramref name="count"/> of message <paramref name="id"/>.</summary>
    public static byte[] EncodeData(uint id, ushort index, ushort count, ReadOnlySpan<byte> chunk)
    {
        byte[] frame = new byte[DataHeaderSize + chunk.Length];
        frame[0] = (byte)SerialFrameKind.Data;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(1), id);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(5), index);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(7), count);
        chunk.CopyTo(frame.AsSpan(DataHeaderSize));
        return frame;
    }

    /// <summary>Builds a reply frame accepting or rejecting message <paramref name="id"/>.</summary>
    public static byte[] EncodeReply(uint id, bool success)
    {
        byte[] frame = new byte[ReplySize];
        frame[0] = (byte)SerialFrameKind.Reply;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(1), id);
        frame[5] = success ? (byte)1 : (byte)0;
        return frame;
    }

    /// <summary>Parses <paramref name="frame"/>, returning <see langword="false"/> for anything that is not a well-formed frame.</summary>
    public static bool TryParse(ReadOnlyMemory<byte> frame, out SerialFrame parsed)
    {
        parsed = default;
        ReadOnlySpan<byte> span = frame.Span;
        if (span.Length >= DataHeaderSize && span[0] == (byte)SerialFrameKind.Data)
        {
            ushort count = BinaryPrimitives.ReadUInt16LittleEndian(span[7..]);
            ushort index = BinaryPrimitives.ReadUInt16LittleEndian(span[5..]);
            if (count == 0 || index >= count) { return false; }

            parsed = new SerialFrame(SerialFrameKind.Data, BinaryPrimitives.ReadUInt32LittleEndian(span[1..]), index, count, false, frame[DataHeaderSize..]);
            return true;
        }

        if (span.Length == ReplySize && span[0] == (byte)SerialFrameKind.Reply)
        {
            parsed = new SerialFrame(SerialFrameKind.Reply, BinaryPrimitives.ReadUInt32LittleEndian(span[1..]), 0, 0, span[5] != 0, ReadOnlyMemory<byte>.Empty);
            return true;
        }

        return false;
    }
}

/// <summary>The kind of a <see cref="SerialFrame"/>.</summary>
internal enum SerialFrameKind : byte
{
    /// <summary>One fragment of a message.</summary>
    Data = 1,
    /// <summary>The remote node's accept or reject of a whole message.</summary>
    Reply = 2
}
