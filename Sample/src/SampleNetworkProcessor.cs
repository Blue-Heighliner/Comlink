namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Reacts to peer activity: a newly connected user is welcomed with who else is currently online, everyone still online is told when someone
/// disconnects, and any received message tagged <c>PING</c> gets an automatic <c>PONG</c> reply.
/// </summary>
public sealed class SampleNetworkProcessor : INetworkProcessor<SampleMessage>
{
    /// <inheritdoc />
    public Task OnConnected(INetworkConnectedContext<SampleMessage> context)
    {
        string userName = context.TargetUser;
        List<string> others = [.. context.ConnectedUsers.Select(u => u.Name).Where(name => !string.Equals(name, userName, StringComparison.OrdinalIgnoreCase))];
        string body = others.Count > 0 ? $"Also online right now: {string.Join(", ", others)}." : "You're the only one online right now.";
        context.Send(new SampleMessage { Title = "Welcome", Text = body, Recipients = [new SampleRecipient { User = userName }] });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnDisconnected(INetworkDisconnectedContext<SampleMessage> context)
    {
        string userName = context.TargetUser;
        foreach (UserInfo user in context.ConnectedUsers)
        {
            context.Send(new SampleMessage { Title = "Offline", Text = $"{userName} just went offline.", Recipients = [new SampleRecipient { User = user.Name }] });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnReceived(INetworkReceivedContext<SampleMessage> context)
    {
        SampleMessage message = context.Message;
        if (string.Equals(message.Category, "PING", StringComparison.OrdinalIgnoreCase))
        {
            context.Send(new SampleMessage { Title = "Re: " + message.Title, Text = "PONG", Recipients = [new SampleRecipient { User = message.Sender }] });
        }

        return Task.CompletedTask;
    }
}
