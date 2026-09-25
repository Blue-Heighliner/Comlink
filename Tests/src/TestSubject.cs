namespace BlueHeighliner.Comlink.Tests;

/// <summary>A minimal hot observable that, unlike <see cref="TestObservable{T}"/>, can also complete, as a real device stream does when it ends.</summary>
internal sealed class TestSubject<T> : IObservable<T>
{
    private readonly List<IObserver<T>> observers = [];

    /// <summary>Publishes <paramref name="value"/> to every current subscriber.</summary>
    public void Publish(T value)
    {
        foreach (IObserver<T> observer in Snapshot()) { observer.OnNext(value); }
    }

    /// <summary>Completes every current subscriber and drops them.</summary>
    public void Complete()
    {
        IObserver<T>[] current = Snapshot();
        lock (observers) { observers.Clear(); }
        foreach (IObserver<T> observer in current) { observer.OnCompleted(); }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        lock (observers) { observers.Add(observer); }
        return new Unsubscriber(this, observer);
    }

    private IObserver<T>[] Snapshot()
    {
        lock (observers) { return [.. observers]; }
    }

    private sealed class Unsubscriber(TestSubject<T> subject, IObserver<T> observer) : IDisposable
    {
        public void Dispose()
        {
            lock (subject.observers) { subject.observers.Remove(observer); }
        }
    }
}
