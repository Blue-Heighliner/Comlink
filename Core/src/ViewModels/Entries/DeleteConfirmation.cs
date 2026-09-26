namespace BlueHeighliner.Comlink.ViewModels.Entries;

/// <summary>
/// Two-press confirmation for deleting an entry from its editor: the first <see cref="Confirm"/> arms it, and a second
/// one within the window confirms. If the window passes without one, it disarms itself, so a stray click cannot leave
/// a delete waiting to be triggered later.
/// </summary>
internal sealed class DeleteConfirmation(Action<bool> changed, TimeSpan? window = null)
{
    private readonly TimeSpan window = window ?? TimeSpan.FromSeconds(4);
    private readonly Lock gate = new();
    private CancellationTokenSource? timer;

    /// <summary>Gets a value indicating whether a press is armed and waiting for its confirming one.</summary>
    public bool IsPending
    {
        get { lock (gate) { return timer is not null; } }
    }

    /// <summary>Arms the confirmation, or, when already armed, disarms it and reports that the delete is confirmed.</summary>
    /// <returns><see langword="true"/> when this press confirmed a delete armed by an earlier one.</returns>
    public bool Confirm()
    {
        CancellationTokenSource? previous;
        CancellationTokenSource? source = null;
        lock (gate)
        {
            previous = timer;
            if (previous is null)
            {
                source = new CancellationTokenSource();
                timer = source;
            }
            else
            {
                timer = null;
            }
        }

        if (previous is not null)
        {
            previous.Cancel();
            Notify(false);
            return true;
        }

        Notify(true);
        _ = Expire(source!);
        return false;
    }

    private async Task Expire(CancellationTokenSource source)
    {
        try { await Task.Delay(window, source.Token); }
        catch (OperationCanceledException) { return; }

        bool expired;
        lock (gate)
        {
            expired = ReferenceEquals(timer, source);
            if (expired) { timer = null; }
        }

        if (expired) { Notify(false); }
    }

    private void Notify(bool pending)
    {
        if (Dispatcher.UIThread.CheckAccess()) { changed(pending); }
        else { Dispatcher.UIThread.Post(() => changed(pending)); }
    }
}
