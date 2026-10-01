namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Reacts to peer activity: a newly connected user is welcomed with who else is currently online, everyone still online is told when someone
/// disconnects, and any received message tagged <c>PING</c> gets an automatic <c>PONG</c> reply.
/// </summary>
public sealed class SampleNetworkProcessor : INetworkProcessor<SampleFrame>
{
    /// <inheritdoc />
    public Task OnConnected(INetworkConnectedContext<SampleFrame> context)
    {
        string userName = context.TargetUser;
        List<string> others = [.. context.ConnectedUsers.Select(u => u.Name).Where(name => !string.Equals(name, userName, StringComparison.OrdinalIgnoreCase))];
        string body = others.Count > 0 ? $"Welcome. Also online right now: {string.Join(", ", others)}." : "Welcome. You're the only one online right now.";
        context.Send(new SampleFrame { IsMessage = true, Text = body, Recipients = [new SampleRecipient { User = userName }] });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnDisconnected(INetworkDisconnectedContext<SampleFrame> context)
    {
        string userName = context.TargetUser;
        foreach (UserInfo user in context.ConnectedUsers)
        {
            context.Send(new SampleFrame { IsMessage = true, Text = $"{userName} just went offline.", Recipients = [new SampleRecipient { User = user.Name }] });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnReceived(INetworkReceivedContext<SampleFrame> context)
    {
        SampleFrame frame = context.Frame;
        if (frame.IsMessage && string.Equals(frame.Category, "PING", StringComparison.OrdinalIgnoreCase))
        {
            context.Send(new SampleFrame { IsMessage = true, Text = "PONG", Recipients = [new SampleRecipient { User = frame.Sender }] });
        }

        return Task.CompletedTask;
    }
}
