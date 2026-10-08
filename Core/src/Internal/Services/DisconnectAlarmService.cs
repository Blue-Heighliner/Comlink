namespace BlueHeighliner.Comlink;

/// <summary>
/// Sounds an alarm, in every role, when a connection drops: whenever <see cref="IPeerService"/> reports a user disconnected the alarm sound plays for
/// <see cref="IEngineController.DisconnectAlarmDuration"/>, starting the time again when another connection drops while it is still sounding. It stops early once every connection
/// that dropped during the current alarm is back up. It is separate from the alarm for alerts (see <see cref="IAlertViewModel"/>) and has its own sound player.
/// </summary>
internal interface IDisconnectAlarmService
{
    /// <summary>Subscribes to <see cref="IPeerService"/>'s connection events and blocks until <paramref name="cancellation"/> is cancelled, then silences the alarm.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="IDisconnectAlarmService" />
internal sealed class DisconnectAlarmService(IPeerService peerService, IEngineController engineController, IDisconnectAlarmPlayer player) : IDisconnectAlarmService
{
    private readonly Lock gate = new();
    private readonly HashSet<string> dropped = new(StringComparer.OrdinalIgnoreCase);
    private Timer? timer;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        peerService.UserConnected += OnUserConnected;
        peerService.UserDisconnected += OnUserDisconnected;

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
        finally
        {
            peerService.UserConnected -= OnUserConnected;
            peerService.UserDisconnected -= OnUserDisconnected;
            Silence();
        }
    }

    private Task OnUserDisconnected(string userName)
    {
        TimeSpan duration = engineController.DisconnectAlarmDuration;
        lock (gate)
        {
            dropped.Add(userName);
            if (timer is null)
            {
                timer = new Timer(_ => Silence(), null, duration, Timeout.InfiniteTimeSpan);
            }
            else
            {
                timer.Change(duration, Timeout.InfiniteTimeSpan);
            }
        }

        player.Play();
        return Task.CompletedTask;
    }

    private Task OnUserConnected(string userName)
    {
        bool allBack;
        lock (gate) { allBack = dropped.Remove(userName) && dropped.Count == 0; }

        if (allBack)
        {
            Silence();
        }
        return Task.CompletedTask;
    }

    private void Silence()
    {
        lock (gate)
        {
            timer?.Dispose();
            timer = null;
            dropped.Clear();
        }

        player.Stop();
    }
}
