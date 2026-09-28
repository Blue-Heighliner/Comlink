namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>A minimal thread-safe hot observable: values published while nobody is subscribed are dropped.</summary>
internal sealed class PeerEvent<T> : IObservable<T>
{
    private readonly Lock gate = new();
    private IObserver<T>[] observers = [];

    /// <summary>Publishes <paramref name="value"/> to every current subscriber; a subscriber that throws is left subscribed and does not affect the others.</summary>
    public void Publish(T value)
    {
        IObserver<T>[] snapshot = observers;
        foreach (IObserver<T> observer in snapshot)
        {
            try { observer.OnNext(value); }
            catch { }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        lock (gate) { observers = [.. observers, observer]; }
        return new Subscription(this, observer);
    }

    private void Remove(IObserver<T> observer)
    {
        lock (gate) { observers = [.. observers.Where(o => !ReferenceEquals(o, observer))]; }
    }

    private sealed class Subscription(PeerEvent<T> owner, IObserver<T> observer) : IDisposable
    {
        public void Dispose() => owner.Remove(observer);
    }
}
