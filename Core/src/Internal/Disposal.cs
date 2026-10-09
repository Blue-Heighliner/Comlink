namespace BlueHeighliner.Comlink;

/// <summary>Disposes the frames and packets the engine is finished with. Both are the host's own types, so they are disposed only when they happen to be <see cref="IDisposable"/>.</summary>
internal static class Disposal
{
    extension(object? instance)
    {
        /// <summary>Disposes the instance if it is <see cref="IDisposable"/>. Only for an instance the engine created or deserialized and never handed to the host's handlers, which own what they are given.</summary>
        public void TryDispose() => (instance as IDisposable)?.Dispose();
    }
}
