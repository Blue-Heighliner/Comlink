namespace BlueHeighliner.Comlink.Tests;

/// <summary>A minimal hot <see cref="IObservable{T}"/> for stubbing an <see cref="IMsmtPeer"/> mock's observable properties in tests, letting a test raise a value with <see cref="Publish"/> exactly like the old event-based API's <c>Mock.Raise</c>.</summary>
internal sealed class TestObservable<T> : IObservable<T>
{
    private readonly List<IObserver<T>> observers = [];

    /// <summary>Publishes <paramref name="value"/> to every observer currently subscribed.</summary>
    public void Publish(T value)
    {
        foreach (IObserver<T> observer in observers.ToArray())
        {
            observer.OnNext(value);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        observers.Add(observer);
        return new Unsubscriber(observers, observer);
    }

    private sealed class Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer) : IDisposable
    {
        public void Dispose() => observers.Remove(observer);
    }
}
