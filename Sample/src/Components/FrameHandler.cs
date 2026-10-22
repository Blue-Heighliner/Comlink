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
public sealed class FrameHandler(IUserCatalog<NoProfile> users, INetwork<Frame, Priority, Level, Aspect> network) : IFrameHandler<Frame, Priority, Level, Aspect>
{
    /// <summary>Gets the name of the auto forwarder whose target list receives a copy of every received alert or <c>URGENT</c>-tagged message.</summary>
    public static string EscalationForwarder { get; } = "Escalation";

    /// <inheritdoc />
    public Task OnConnected(INetworkConnectedContext context)
    {
        if (users.CurrentUser.Role is UserRole.Client && context.TargetUser == users.CurrentUser.Parent?.User)
        {
            network.SetNetworkIndicator(true);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnDisconnected(INetworkDisconnectedContext context)
    {
        if (users.CurrentUser.Role is UserRole.Client && context.TargetUser == users.CurrentUser.Parent?.User)
        {
            network.SetNetworkIndicator(false);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnReceived(INetworkReceivedContext<Frame> context)
        => users.CurrentUser.Role is UserRole.Server ? Serve(context) : Receive(context);

    /// <inheritdoc />
    public async Task OnSent(INetworkSentContext<Priority, Level, Aspect> context)
    {
        bool sent = await network.Send(users.CurrentUser.Parent!.User, context.Message.Priority, Frame.FromMessage(context.Message));
        await network.SetSentStatus(context.Message.Id, context.Destinations, sent ? DestinationStatus.Sent : DestinationStatus.Failed);
    }

    /// <inheritdoc />
    public Task OnRead(INetworkReadContext<Priority, Level, Aspect> context)
        => network.Send
        (
            users.CurrentUser.Parent!.User,
            Priority.Receipt,
            new()
            {
                IsReadReceipt = true,
                Id = Guid.NewGuid().ToString("N"),
                Sender = users.CurrentUser.Name,
                ReadMessageId = context.Message.Id,
                Timestamp = DateTime.UtcNow,
                Recipients = [new() { User = context.Message.FromUser }]
            }
        );

    /// <inheritdoc />
    public Task OnRetrieval(INetworkRetrievalContext context)
        => network.Send
        (
            users.CurrentUser.Parent!.User,
            Priority.Retrieval,
            new()
            {
                IsRetrieval = true,
                Id = Guid.NewGuid().ToString("N"),
                Sender = users.CurrentUser.Name,
                Timestamp = DateTime.UtcNow,
                Recipients = [new() { User = context.Server }],
                RetrievalFrom = context.Criteria.From,
                RetrievalTo = context.Criteria.To,
                RetrievalAuthors = [.. context.Criteria.Authors],
                RetrievalDestinations = [.. context.Criteria.Destinations],
                RetrievalIds = [.. context.Criteria.Ids]
            }
        );

    private async Task Receive(INetworkReceivedContext<Frame> context)
    {
        Frame frame = context.Frame;
        if (context.Origin is FrameOrigin.Interface)
        {
            // An application on this machine sends as the user: the frame goes out as if this node had written it.
            frame.Sender = users.CurrentUser.Name;
            frame.Id = Guid.NewGuid().ToString("N");
            frame.Timestamp = DateTime.UtcNow;
            await network.Send(users.CurrentUser.Parent!.User, frame.Priority, frame);
            return;
        }

        if (frame.IsReadReceipt)
        {
            await network.SetSentStatus(frame.ReadMessageId, frame.Sender, DestinationStatus.Read);
        }
        else if (frame.IsReceiveReceipt)
        {
            await network.SetSentStatus(frame.ReceivedMessageId, frame.Sender, DestinationStatus.Received);
        }
        else if (frame.IsMessage)
        {
            await ReceiveMessage(context, frame);
        }
    }

    private async Task ReceiveMessage(INetworkReceivedContext<Frame> context, Frame frame)
    {
        await network.ReceiveMessage(frame.ToMessage());

        if (context.Origin is not FrameOrigin.Peer)
        {
            return;
        }

        await network.SendInterface(frame.Priority, frame);

        await network.Send
        (
            users.CurrentUser.Parent!.User,
            Priority.Receipt,
            new()
            {
                IsReceiveReceipt = true,
                Id = Guid.NewGuid().ToString("N"),
                Sender = users.CurrentUser.Name,
                ReceivedMessageId = frame.Id,
                Timestamp = DateTime.UtcNow,
                Recipients = [new() { User = frame.Sender }]
            }
        );

        if (frame.Category is "ALERT" or "URGENT")
        {
            IReadOnlyList<string> targets = await network.GetAutoForwardTargets(EscalationForwarder);
            if (targets.Count > 0)
            {
                frame.Sender = users.CurrentUser.Name;
                frame.Recipients = [.. targets.Select(target => new Recipient { User = target })];
                await network.Send(users.CurrentUser.Parent!.User, frame.Priority, frame);
            }
        }
    }

    private async Task Serve(INetworkReceivedContext<Frame> context)
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

        if (context.Frame.IsMessage && !IsServer(context.SourceUser))
        {
            await network.StoreMessage(context.Frame.ToMessage());
        }

        await Route
        (
            context,
            context.Frame,
            network.GetDestinations(context.Frame.IsMessage ? (Level?)context.Frame.Confidentiality : null, out _, context.Frame.Recipients.Where(recipient => recipient.Kind is not "OUTSIDE").Select(recipient => recipient.User)),
            !IsServer(context.SourceUser)
        );
    }

    private bool IsServer(string userName) => users.Users.TryGetValue(userName, out UserInfo<NoProfile>? info) && info.Role is UserRole.Server;

    // A retrieval request names the server it is for: this one answers it, and a request that came from a client is handed on to the other server it names.
    private async Task Answer(INetworkReceivedContext<Frame> context, Frame request)
    {
        string server = request.Recipients.FirstOrDefault()?.User ?? string.Empty;
        if (server != users.CurrentUser.Name)
        {
            if (!IsServer(context.SourceUser) && server.Length > 0)
            {
                await network.Send(server, request.Priority, request);
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
        foreach (Message stored in await network.FindStoredMessages(criteria))
        {
            Frame copy = Frame.FromMessage(stored with { Addresses = [new() { UserName = request.Sender, Type = AddressType.To }], IsAlert = false });
            await Route(context, copy, [request.Sender], true);
        }
    }

    // Sends a frame once to each node that reaches a recipient: the recipient itself when it is a child of this server, and, when allowed, the other server that is the recipient or their parent.
    private async Task Route(INetworkReceivedContext<Frame> context, Frame frame, IEnumerable<string> recipients, bool mayLeaveServer)
    {
        HashSet<string> hops = [];
        foreach (string user in recipients)
        {
            if (!users.Users.TryGetValue(user, out UserInfo<NoProfile>? info))
            {
                continue;
            }

            if (info.Role is UserRole.Server)
            {
                if (mayLeaveServer && user != users.CurrentUser.Name)
                {
                    hops.Add(user);
                }
            }
            else if (info.Parent?.User == users.CurrentUser.Name)
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
            await network.Send(hop, frame.Priority, frame);
        }
    }
}
