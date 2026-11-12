namespace BlueHeighliner.Comlink;

/// <summary>
/// An <see cref="IBufferWriter{T}"/> whose write position can be moved, so something already written can be written over, such as a length prefix that is only known once the body after it has been written.
/// What it holds is everything up to the furthest position written to, wherever the position is now.
/// </summary>
/// <typeparam name="T">The type of the items written.</typeparam>
public interface ISeekableBufferWriter<T> : IBufferWriter<T>
{
    /// <summary>Gets how many items have been written: the furthest position written to.</summary>
    int Length { get; }

    /// <summary>Gets or sets where the next item is written. Writing at a position before <see cref="Length"/> overwrites what is there; writing at <see cref="Length"/> appends.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The position is negative or beyond <see cref="Length"/>, which would leave a gap.</exception>
    int Position { get; set; }
}
