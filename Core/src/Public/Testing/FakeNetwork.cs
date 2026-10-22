namespace BlueHeighliner.Comlink.Testing;

/// <summary>
/// A stand-in for the <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}"/> a frame handler acts with, to test a handler by what it did. Every send is accepted (unless <see cref="SendResult"/> says otherwise) and every other
/// operation finishes at once, each recorded in a list of its own; the answers to the questions the handler asks can be set. <c>GetDestinations</c> answers the distinct targets it is given (a message's, for the overload that takes a
/// message), leaving out no one.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its aspects.</typeparam>
public sealed class FakeNetwork<TFrame, TPriority, TLevel, TAspect> : INetwork<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Gets every frame sent to a user, in order.</summary>
    public List<(string UserName, TPriority Priority, TFrame Frame)> Sent { get; } = [];

    /// <summary>Gets every frame sent to the interfaces, in order.</summary>
    public List<(TPriority Priority, TFrame Frame)> SentToInterfaces { get; } = [];

    /// <summary>Gets every frame sent to the external systems, in order.</summary>
    public List<TFrame> SentToExternalSystems { get; } = [];

    /// <summary>Gets every message the handler handed to the engine as received, in order.</summary>
    public List<Message<TPriority, TLevel, TAspect>> ReceivedMessages { get; } = [];

    /// <summary>Gets every message the handler stored for retrieval, in order.</summary>
    public List<Message<TPriority, TLevel, TAspect>> Stored { get; } = [];

    /// <summary>Gets every delivery status the handler set on a sent message, one entry per user, in order.</summary>
    public List<(string MessageId, string UserName, DestinationStatus Status)> SentStatuses { get; } = [];

    /// <summary>Gets every status the handler set on a received message, in order.</summary>
    public List<(string MessageId, DestinationStatus Status)> ReceivedStatuses { get; } = [];

    /// <summary>Gets every state the handler set the network indicator to (<see langword="true"/> for online), in order.</summary>
    public List<bool> NetworkIndicators { get; } = [];

    /// <summary>Gets or sets what <c>Send</c> reports: whether the user accepted the frame. Defaults to <see langword="true"/>.</summary>
    public bool SendResult { get; set; } = true;

    /// <summary>Gets or sets what <c>FindStoredMessages</c> returns, whatever the criteria. Defaults to none.</summary>
    public IReadOnlyList<Message<TPriority, TLevel, TAspect>> FoundMessages { get; set; } = [];

    /// <summary>Gets or sets what <c>GetAutoForwardTargets</c> returns, whatever the auto forwarder. Defaults to none.</summary>
    public IReadOnlyList<string> AutoForwardTargets { get; set; } = [];

    /// <inheritdoc />
    public Task<bool> Send(string userName, TPriority priority, TFrame frame)
    {
        Sent.Add((userName, priority, frame));
        return Task.FromResult(SendResult);
    }

    /// <inheritdoc />
    public Task ReceiveMessage(Message<TPriority, TLevel, TAspect> message)
    {
        ReceivedMessages.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetSentStatus(string messageId, string userName, DestinationStatus status)
    {
        SentStatuses.Add((messageId, userName, status));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetSentStatus(string messageId, IEnumerable<string> userNames, DestinationStatus status)
    {
        SentStatuses.AddRange(userNames.Select(userName => (messageId, userName, status)));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetReceivedStatus(string messageId, DestinationStatus status)
    {
        ReceivedStatuses.Add((messageId, status));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void SetNetworkIndicator(bool isOnline) => NetworkIndicators.Add(isOnline);

    /// <inheritdoc />
    public Task SendInterface(TPriority priority, TFrame frame)
    {
        SentToInterfaces.Add((priority, frame));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendToExternalSystems(TFrame frame)
    {
        SentToExternalSystems.Add(frame);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StoreMessage(Message<TPriority, TLevel, TAspect> message)
    {
        Stored.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Message<TPriority, TLevel, TAspect>>> FindStoredMessages(RetrievalCriteria criteria) => Task.FromResult(FoundMessages);

    /// <inheritdoc />
    public IReadOnlySet<string> GetDestinations(TLevel? minimumLevel, out IReadOnlySet<string> excluded, params IEnumerable<string> targets)
    {
        excluded = new HashSet<string>();
        return new HashSet<string>(targets);
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetDestinations(Message<TPriority, TLevel, TAspect> message, out IReadOnlySet<string> excluded)
    {
        excluded = new HashSet<string>();
        return new HashSet<string>(message.Addresses.Where(address => address.Type is not AddressType.External).Select(address => address.UserName));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAutoForwardTargets(string controllerName) => Task.FromResult(AutoForwardTargets);
}
