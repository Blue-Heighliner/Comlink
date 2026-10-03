namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Reacts to peer activity: a newly connected user is welcomed with who else is currently online, everyone still online is told when someone
/// disconnects, and any received message tagged <c>PING</c> gets an automatic <c>PONG</c> reply. A relay composes nothing of its own, so it does none of this, and a relay is never welcomed, announced or counted among who is online, since nothing can be addressed to it.
/// </summary>
public sealed class SampleNetworkProcessor : INetworkProcessor<SampleFrame>
{
    private bool IsRelay(IEnumerable<UserInfo> users, string userName) => users.Any(user => user.Role == UserRole.Relay && string.Equals(user.Name, userName, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public void OnConnected(INetworkConnectedContext<SampleFrame> context)
    {
        if (context.CurrentUser.Role == UserRole.Relay) { return; }

        string userName = context.TargetUser;
        if (IsRelay(context.Users, userName)) { return; }

        List<string> others = [.. context.ConnectedUsers.Where(u => u.Role != UserRole.Relay).Select(u => u.Name).Where(name => !string.Equals(name, userName, StringComparison.OrdinalIgnoreCase))];
        string body = others.Count > 0 ? $"Welcome. Also online right now: {string.Join(", ", others)}." : "Welcome. You're the only one online right now.";
        context.Send(new SampleFrame { IsMessage = true, Text = body, Recipients = [new SampleRecipient { User = userName }] });
    }

    /// <inheritdoc />
    public void OnDisconnected(INetworkDisconnectedContext<SampleFrame> context)
    {
        if (context.CurrentUser.Role == UserRole.Relay) { return; }

        string userName = context.TargetUser;
        if (IsRelay(context.Users, userName)) { return; }

        foreach (UserInfo user in context.ConnectedUsers.Where(u => u.Role != UserRole.Relay))
        {
            context.Send(new SampleFrame { IsMessage = true, Text = $"{userName} just went offline.", Recipients = [new SampleRecipient { User = user.Name }] });
        }
    }

    /// <inheritdoc />
    public void OnReceived(INetworkReceivedContext<SampleFrame> context)
    {
        if (context.CurrentUser.Role == UserRole.Relay) { return; }

        SampleFrame frame = context.Frame;
        if (frame.IsMessage && string.Equals(frame.Category, "PING", StringComparison.OrdinalIgnoreCase))
        {
            context.Send(new SampleFrame { IsMessage = true, Text = "PONG", Recipients = [new SampleRecipient { User = frame.Sender }] });
        }
    }
}
