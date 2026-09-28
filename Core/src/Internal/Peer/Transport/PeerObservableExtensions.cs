namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// Subscription helper that avoids the ambiguity between the MSMT package's own <c>Subscribe(Action)</c> extension and
/// System.Reactive's, both of which are visible once the MicroGate package is referenced.
/// </summary>
internal static class PeerObservableExtensions
{
    extension<T>(IObservable<T> source)
    {
        /// <summary>Subscribes <paramref name="onNext"/>, and optionally <paramref name="onCompleted"/>, ignoring errors.</summary>
        public IDisposable Listen(Action<T> onNext, Action? onCompleted = null) => source.Subscribe(new ActionObserver<T>(onNext, onCompleted));
    }

    private sealed class ActionObserver<T>(Action<T> onNext, Action? onCompleted) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);

        public void OnError(Exception error) => onCompleted?.Invoke();

        public void OnCompleted() => onCompleted?.Invoke();
    }
}
