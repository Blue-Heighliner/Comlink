namespace BlueHeighliner.Comlink.Testing;

/// <summary>
/// A mock of the <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}"/> a frame handler acts with. Every send is accepted and every other operation finishes at once, so a test verifies what the handler did;
/// the answers to the questions it asks can be set. <c>GetDestinations</c> answers the distinct targets it is given (a message's, for the overload that takes a message), leaving out no one.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public sealed class MockNetwork<TFrame, TPriority, TLevel, TAspect> : Mock<INetwork<TFrame, TPriority, TLevel, TAspect>> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    private delegate IReadOnlySet<string> DestinationsOfLevel(TLevel? minimumLevel, out IReadOnlySet<string> excluded, IEnumerable<string> targets);

    private delegate IReadOnlySet<string> DestinationsOfMessage(Message<TPriority, TLevel, TAspect> message, out IReadOnlySet<string> excluded);

    /// <summary>Initializes the mock.</summary>
    public MockNetwork()
    {
        Setup(network => network.Send(It.IsAny<string>(), It.IsAny<TPriority>(), It.IsAny<TFrame>())).Returns(() => Task.FromResult(SendResult));
        Setup(network => network.ReceiveMessage(It.IsAny<Message<TPriority, TLevel, TAspect>>())).Returns(Task.CompletedTask);
        Setup(network => network.SetSentStatus(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DestinationStatus>())).Returns(Task.CompletedTask);
        Setup(network => network.SetSentStatus(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DestinationStatus>())).Returns(Task.CompletedTask);
        Setup(network => network.SetReceivedStatus(It.IsAny<string>(), It.IsAny<DestinationStatus>())).Returns(Task.CompletedTask);
        Setup(network => network.SendInterface(It.IsAny<TPriority>(), It.IsAny<TFrame>())).Returns(Task.CompletedTask);
        Setup(network => network.SendToExternalSystems(It.IsAny<TFrame>())).Returns(Task.CompletedTask);
        Setup(network => network.StoreMessage(It.IsAny<Message<TPriority, TLevel, TAspect>>())).Returns(Task.CompletedTask);
        Setup(network => network.FindStoredMessages(It.IsAny<RetrievalCriteria>())).Returns(() => Task.FromResult(StoredMessages));
        Setup(network => network.GetAutoForwardTargets(It.IsAny<string>())).Returns(() => Task.FromResult(AutoForwardTargets));

        IReadOnlySet<string> excludedOfLevel = new HashSet<string>();
        Setup(network => network.GetDestinations(It.IsAny<TLevel?>(), out excludedOfLevel, It.IsAny<IEnumerable<string>>()))
            .Returns(new DestinationsOfLevel((TLevel? _, out IReadOnlySet<string> excluded, IEnumerable<string> targets) =>
            {
                excluded = new HashSet<string>();
                return new HashSet<string>(targets);
            }));
        IReadOnlySet<string> excludedOfMessage = new HashSet<string>();
        Setup(network => network.GetDestinations(It.IsAny<Message<TPriority, TLevel, TAspect>>(), out excludedOfMessage))
            .Returns(new DestinationsOfMessage((Message<TPriority, TLevel, TAspect> message, out IReadOnlySet<string> excluded) =>
            {
                excluded = new HashSet<string>();
                return new HashSet<string>(message.Addresses.Where(address => address.Type is not AddressType.External).Select(address => address.UserName));
            }));
    }

    /// <summary>Gets or sets what <c>Send</c> reports: whether the user accepted the frame. Defaults to <see langword="true"/>.</summary>
    public bool SendResult { get; set; } = true;

    /// <summary>Gets or sets what <c>FindStoredMessages</c> returns, whatever the criteria. Defaults to none.</summary>
    public IReadOnlyList<Message<TPriority, TLevel, TAspect>> StoredMessages { get; set; } = [];

    /// <summary>Gets or sets what <c>GetAutoForwardTargets</c> returns, whatever the auto forwarder. Defaults to none.</summary>
    public IReadOnlyList<string> AutoForwardTargets { get; set; } = [];
}
