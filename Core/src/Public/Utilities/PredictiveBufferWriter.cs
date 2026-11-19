namespace BlueHeighliner.Comlink;

/// <summary>
/// An <see cref="IBufferWriter{T}"/> that can be told how much will be written, so it can allocate once instead of growing in steps as the writes arrive. A prediction is only a hint: writing more or less than predicted is always fine.
/// </summary>
/// <typeparam name="T">The type of the items written.</typeparam>
public interface IPredictiveBufferWriter<T> : IBufferWriter<T>
{
    /// <summary>
    /// Predicts how many more items will be written from the current position, and makes room for them now. A later call replaces the earlier prediction instead of adding to it, so predicting the same size twice reserves it once. A prediction only
    /// ever makes the buffer larger: a smaller one than before, or one that is already covered, leaves the buffer as it is.
    /// </summary>
    /// <param name="size">How many more items are expected to be written, which may be zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    void Predict(int size);
}
