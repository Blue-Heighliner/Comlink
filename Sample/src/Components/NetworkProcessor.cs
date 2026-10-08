namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// The Sample's whole protocol, because the engine itself receives, routes, receipts and retrieves nothing and keeps no network indicator. What each role does with a <see cref="Frame"/>:
/// <list type="bullet">
/// <item>A client turns a message the user sends (<see cref="OnSent"/>) into a frame addressed to every recipient and hands it to its server, reporting each recipient's status as it learns it. It turns a received message
/// frame into a <see cref="Message"/> for the engine to store and show, passes it on to every connected interface, sends the sender a receive receipt (and a read receipt when the user opens the message),
/// forwards a message tagged <c>ALERT</c> or <c>URGENT</c> on, as the original frame, sent as the forwarding user, addressed to every user on the target list of the Escalation auto forwarder, and turns the receipts it receives into delivery statuses. It asks a storage server for stored messages when the
/// user submits a retrieval. It shows the network online while its parent is connected.</item>
/// <item>A server routes what its clients send: it keeps a copy of every message, expands the group addresses, leaves out recipients whose message level is too low, and sends the frame once to each local client that
/// is a recipient and once to each other server that owns a recipient (a frame that came from another server is only delivered locally). It answers a retrieval request addressed to it with the stored messages.</item>
/// </list>
/// </summary>
public sealed class NetworkProcessor : INetworkProcessor<Frame, MessagePriority, MessageLevel, MessageAspect>
{
    /// <summary>Gets the name of the auto forwarder whose target list receives a copy of every received alert or <c>URGENT</c>-tagged message.</summary>
    public static string EscalationForwarder { get; } = "Escalation";

    /// <inheritdoc />
    public Task OnConnected(INetworkConnectedContext<Frame, MessagePriority, MessageLevel, MessageAspect> context)
    {
        if (context.CurrentUser.Role is UserRole.Client && context.TargetUser.Name == context.CurrentUser.Parent?.User)
        {
            context.SetNetworkIndicator(true);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnDisconnected(INetworkDisconnectedContext<Frame, MessagePriority, MessageLevel, MessageAspect> context)
    {
        if (context.CurrentUser.Role is UserRole.Client && context.TargetUser.Name == context.CurrentUser.Parent?.User)
        {
            context.SetNetworkIndicator(false);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnReceived(INetworkReceivedContext<Frame, MessagePriority, MessageLevel, MessageAspect> context)
        => context.CurrentUser.Role is UserRole.Server ? Serve(context) : Receive(context);

    /// <inheritdoc />
    public async Task OnSent(INetworkSentContext<Frame, MessagePriority, MessageLevel, MessageAspect> context)
    {
        bool sent = await context.Send(context.CurrentUser.Parent!.User, context.Message.Priority, Frame.FromMessage(context.Message));
        await context.SetSentStatus(context.Message.Id, context.Destinations, sent ? DestinationStatus.Sent : DestinationStatus.Failed);
    }

    /// <inheritdoc />
    public Task OnRead(INetworkReadContext<Frame, MessagePriority, MessageLevel, MessageAspect> context)
        => context.Send
        (
            context.CurrentUser.Parent!.User,
            MessagePriority.Receipt,
            new()
            {
                IsReadReceipt = true,
                Id = Guid.NewGuid().ToString("N"),
                Sender = context.CurrentUser.Name,
                ReadMessageId = context.Message.Id,
                Timestamp = DateTime.UtcNow,
                Recipients = [new() { User = context.Message.FromUser }]
            }
        );

    /// <inheritdoc />
    public Task OnRetrieval(INetworkRetrievalContext<Frame, MessagePriority, MessageLevel, MessageAspect> context)
        => context.Send
        (
            context.CurrentUser.Parent!.User,
            MessagePriority.Retrieval,
            new()
            {
                IsRetrieval = true,
                Id = Guid.NewGuid().ToString("N"),
                Sender = context.CurrentUser.Name,
                Timestamp = DateTime.UtcNow,
                Recipients = [new() { User = context.Server.Name }],
                RetrievalFrom = context.Criteria.From,
                RetrievalTo = context.Criteria.To,
                RetrievalAuthors = [.. context.Criteria.Authors],
                RetrievalDestinations = [.. context.Criteria.Destinations],
                RetrievalIds = [.. context.Criteria.Ids]
            }
        );

    private static async Task Receive(INetworkReceivedContext<Frame, MessagePriority, MessageLevel, MessageAspect> context)
    {
        Frame frame = context.Frame;
        if (context.Origin is FrameOrigin.Interface)
        {
            // An application on this machine sends as the user: the frame goes out as if this node had written it.
            frame.Sender = context.CurrentUser.Name;
            frame.Id = Guid.NewGuid().ToString("N");
            frame.Timestamp = DateTime.UtcNow;
            await context.Send(context.CurrentUser.Parent!.User, frame.Priority, frame);
            return;
        }

        if (frame.IsReadReceipt)
        {
            await context.SetSentStatus(frame.ReadMessageId, frame.Sender, DestinationStatus.Read);
        }
        else if (frame.IsReceiveReceipt)
        {
            await context.SetSentStatus(frame.ReceivedMessageId, frame.Sender, DestinationStatus.Received);
        }
        else if (frame.IsMessage)
        {
            await ReceiveMessage(context, frame);
        }
    }

    private static async Task ReceiveMessage(INetworkReceivedContext<Frame, MessagePriority, MessageLevel, MessageAspect> context, Frame frame)
    {
        await context.ReceiveMessage(frame.ToMessage());

        if (context.Origin is not FrameOrigin.Peer)
        {
            return;
        }

        await context.SendInterface(frame.Priority, frame);

        await context.Send
        (
            context.CurrentUser.Parent!.User,
            MessagePriority.Receipt,
            new()
            {
                IsReceiveReceipt = true,
                Id = Guid.NewGuid().ToString("N"),
                Sender = context.CurrentUser.Name,
                ReceivedMessageId = frame.Id,
                Timestamp = DateTime.UtcNow,
                Recipients = [new() { User = frame.Sender }]
            }
        );

        if (frame.Category is "ALERT" or "URGENT")
        {
            IReadOnlyList<string> targets = await context.GetAutoForwardTargets(EscalationForwarder);
            if (targets.Count > 0)
            {
                frame.Sender = context.CurrentUser.Name;
                frame.Recipients = [.. targets.Select(target => new Recipient { User = target })];
                await context.Send(context.CurrentUser.Parent!.User, frame.Priority, frame);
            }
        }
    }

    private static async Task Serve(INetworkReceivedContext<Frame, MessagePriority, MessageLevel, MessageAspect> context)
    {
        if (context.Origin is not FrameOrigin.Peer)
        {
            return;
        }

        if (context.Frame.IsRetrieval)
        {
            await Answer(context, context.Frame);
            return;
        }

        if (context.Frame.IsMessage && context.SourceUser.Role is not UserRole.Server)
        {
            await context.StoreMessage(context.Frame.ToMessage());
        }

        await Route
        (
            context,
            context.Frame,
            context.GetDestinations(context.Frame.IsMessage ? (MessageLevel?)context.Frame.Confidentiality : null, out _, context.Frame.Recipients.Where(recipient => recipient.Kind is not "OUTSIDE").Select(recipient => recipient.User)),
            context.SourceUser.Role is not UserRole.Server
        );
    }

    // A retrieval request names the server it is for: this one answers it, and a request that came from a client is handed on to the other server it names.
    private static async Task Answer(INetworkReceivedContext<Frame, MessagePriority, MessageLevel, MessageAspect> context, Frame request)
    {
        string server = request.Recipients.FirstOrDefault()?.User ?? string.Empty;
        if (server != context.CurrentUser.Name)
        {
            if (context.SourceUser.Role is not UserRole.Server && server.Length > 0)
            {
                await context.Send(server, request.Priority, request);
            }

            return;
        }

        RetrievalCriteria criteria = new()
        {
            From = request.RetrievalFrom,
            To = request.RetrievalTo,
            Authors = request.RetrievalAuthors,
            Destinations = request.RetrievalDestinations,
            Ids = request.RetrievalIds
        };
        foreach (Message stored in await context.FindStoredMessages(criteria))
        {
            Frame copy = Frame.FromMessage(stored with { Addresses = [new() { UserName = request.Sender, Type = AddressType.To }], IsAlert = false });
            await Route(context, copy, [request.Sender], true);
        }
    }

    // Sends a frame once to each node that reaches a recipient: the recipient itself when it is a child of this server, and, when allowed, the other server that is the recipient or their parent.
    private static async Task Route(INetworkReceivedContext<Frame, MessagePriority, MessageLevel, MessageAspect> context, Frame frame, IEnumerable<string> recipients, bool mayLeaveServer)
    {
        HashSet<string> hops = [];
        foreach (string user in recipients)
        {
            if (!context.Users.TryGetValue(user, out UserInfo? info))
            {
                continue;
            }

            if (info.Role is UserRole.Server)
            {
                if (mayLeaveServer && user != context.CurrentUser.Name)
                {
                    hops.Add(user);
                }
            }
            else if (info.Parent?.User == context.CurrentUser.Name)
            {
                hops.Add(user);
            }
            else if (mayLeaveServer && info.Parent is { } parent)
            {
                hops.Add(parent.User);
            }
        }

        foreach (string hop in hops)
        {
            await context.Send(hop, frame.Priority, frame);
        }
    }
}
