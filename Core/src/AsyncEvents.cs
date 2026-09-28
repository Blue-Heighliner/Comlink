namespace BlueHeighliner.Comlink;

/// <summary>
/// Raises events whose handlers are asynchronous. Invoking such an event the ordinary way returns only the last
/// handler's task, so earlier handlers run unawaited: their work can still be in progress when the raiser carries on,
/// and their failures are never seen. <c>InvokeAll</c> awaits every handler, one after another in subscription
/// order, and raises whatever failed once all have run, so one handler failing does not stop the others.
/// </summary>
internal static class AsyncEventExtensions
{
    extension<T>(Func<T, Task>? handlers)
    {
        /// <summary>Invokes every handler with <paramref name="argument"/> and awaits them all. Does nothing without handlers.</summary>
        public Task InvokeAll(T argument) => Run(handlers, handler => ((Func<T, Task>)handler)(argument));
    }

    extension<T1, T2>(Func<T1, T2, Task>? handlers)
    {
        /// <summary>Invokes every handler with the arguments and awaits them all. Does nothing without handlers.</summary>
        public Task InvokeAll(T1 first, T2 second) => Run(handlers, handler => ((Func<T1, T2, Task>)handler)(first, second));
    }

    extension<T1, T2, T3>(Func<T1, T2, T3, Task>? handlers)
    {
        /// <summary>Invokes every handler with the arguments and awaits them all. Does nothing without handlers.</summary>
        public Task InvokeAll(T1 first, T2 second, T3 third) => Run(handlers, handler => ((Func<T1, T2, T3, Task>)handler)(first, second, third));
    }

    private static async Task Run(Delegate? handlers, Func<Delegate, Task> invoke)
    {
        if (handlers is null) { return; }

        List<Exception>? failures = null;
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try { await invoke(handler); }
            catch (Exception ex) { (failures ??= []).Add(ex); }
        }

        if (failures is null) { return; }
        if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        throw new AggregateException(failures);
    }
}
