namespace BlueHeighliner.Comlink;

/// <summary>
/// Runs work that touches bound ViewModel state on the UI thread. Network and storage events are raised on background
/// threads, and changing a bound collection there races with the UI thread reading it (for example while the user
/// pages the entry list).
/// </summary>
internal static class UiThread
{
    /// <summary>Runs <paramref name="action"/> on the UI thread, inline when already on it.</summary>
    /// <param name="action">The work to run.</param>
    /// <returns>A task that completes when <paramref name="action"/> has completed.</returns>
    /// <remarks>
    /// <see cref="Dispatcher.CheckAccess"/> is also <see langword="true"/> when no Avalonia dispatcher loop is running
    /// at all (headless mode, unit tests), so the work runs inline there instead of being posted to a queue nothing
    /// would ever pump.
    /// </remarks>
    public static Task Run(Func<Task> action)
        => Dispatcher.UIThread.CheckAccess() ? action() : Dispatcher.UIThread.InvokeAsync(action);
}
