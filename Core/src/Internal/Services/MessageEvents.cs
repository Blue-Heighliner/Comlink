namespace BlueHeighliner.Comlink;

/// <summary>Where what happens to the user's messages is announced: a message recorded as received, and a destination's delivery status changing. The host's handler causes both through its context; the GUI and <see cref="IEngineConnection"/> listen.</summary>
internal interface IMessageEvents
{
    /// <summary>Raised when the host's handler records a message as received.</summary>
    event Func<Message, Task>? MessageReceived;

    /// <summary>Raised when a destination's delivery status on a sent message changes.</summary>
    event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

    /// <summary>Raises <see cref="MessageReceived"/>.</summary>
    /// <param name="message">The message that was received.</param>
    Task RaiseMessageReceived(Message message);

    /// <summary>Raises <see cref="DeliveryStatusChanged"/>.</summary>
    /// <param name="change">What changed.</param>
    Task RaiseDeliveryStatusChanged(DeliveryStatusChangedEvent change);
}

/// <inheritdoc cref="IMessageEvents" />
internal sealed class MessageEvents : IMessageEvents
{
    /// <inheritdoc />
    public event Func<Message, Task>? MessageReceived;

    /// <inheritdoc />
    public event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

    /// <inheritdoc />
    public Task RaiseMessageReceived(Message message) => MessageReceived.InvokeAll(message);

    /// <inheritdoc />
    public Task RaiseDeliveryStatusChanged(DeliveryStatusChangedEvent change) => DeliveryStatusChanged.InvokeAll(change);
}
