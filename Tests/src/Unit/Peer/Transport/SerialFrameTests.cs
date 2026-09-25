namespace BlueHeighliner.Comlink.Tests.Unit.Peer.Transport;

/// <summary>Unit tests for <see cref="SerialFrame"/> encoding and parsing.</summary>
public sealed class SerialFrameTests
{
    /// <summary>A data frame survives an encode then parse round trip with every field intact.</summary>
    [Fact]
    public void EncodeData_ThenParse_RoundTrips()
    {
        byte[] frame = SerialFrame.EncodeData(0xAABBCCDD, 3, 9, [1, 2, 3, 4]);

        Assert.True(SerialFrame.TryParse(frame, out SerialFrame parsed));
        Assert.Equal(SerialFrameKind.Data, parsed.Kind);
        Assert.Equal(0xAABBCCDDu, parsed.Id);
        Assert.Equal(3, parsed.Index);
        Assert.Equal(9, parsed.Count);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, parsed.Chunk.ToArray());
    }

    /// <summary>A data frame with an empty chunk (a heartbeat) is valid and parses to an empty chunk.</summary>
    [Fact]
    public void EncodeData_EmptyChunk_ParsesToEmptyChunk()
    {
        byte[] frame = SerialFrame.EncodeData(1, 0, 1, []);

        Assert.Equal(SerialFrame.DataHeaderSize, frame.Length);
        Assert.True(SerialFrame.TryParse(frame, out SerialFrame parsed));
        Assert.True(parsed.Chunk.IsEmpty);
    }

    /// <summary>A reply frame survives an encode then parse round trip for both accept and reject.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EncodeReply_ThenParse_RoundTrips(bool success)
    {
        byte[] frame = SerialFrame.EncodeReply(77, success);

        Assert.True(SerialFrame.TryParse(frame, out SerialFrame parsed));
        Assert.Equal(SerialFrameKind.Reply, parsed.Kind);
        Assert.Equal(77u, parsed.Id);
        Assert.Equal(success, parsed.Success);
    }

    /// <summary>Anything that is not a well-formed frame is rejected instead of throwing.</summary>
    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 1, 0, 0, 0 })]
    [InlineData(new byte[] { 9, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 2, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 2, 0, 0, 0, 0, 1, 1 })]
    public void TryParse_Malformed_ReturnsFalse(byte[] frame)
        => Assert.False(SerialFrame.TryParse(frame, out _));

    /// <summary>A fragment whose count is zero, or whose index is not below its count, is rejected.</summary>
    [Fact]
    public void TryParse_InconsistentFragmentNumbers_ReturnsFalse()
    {
        Assert.False(SerialFrame.TryParse(SerialFrame.EncodeData(1, 0, 0, [1]), out _));
        Assert.False(SerialFrame.TryParse(SerialFrame.EncodeData(1, 4, 4, [1]), out _));
    }
}
