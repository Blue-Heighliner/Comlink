# Peer Networking

The peer layer handles direct node-to-node message delivery. Every running instance — in both `Client` and `Headless` modes — runs a `PeerService` that wraps a single [OFT](Oft.md) `IOftPeer`, accepting inbound connections from other nodes and sending outbound ones. Every instance also runs `InterfaceService`, which hosts a local interface listener that mirrors and injects into this same message stream, regardless of mode — see [Interface.md](Interface.md).

## Message Format

Peer traffic carries exactly one payload shape and nothing else: an instance of `IMessageFormat.MessageType`, serialized with **protobuf-net** (binary), via `PeerSerializer` working from that runtime `Type` rather than a compile-time generic parameter. There is no envelope and no message-type discriminator on the wire. OFT-level delivery (did the bytes arrive) is tracked entirely through OFT's own delivery status (see [Delivery status](#delivery-status) below); the only application-level reply that exists is the user-read confirmation described in [Read Confirmation](#read-confirmation), and it is itself just an ordinary instance of `IMessageFormat.MessageType` with one field set — there is still no separate envelope or command discriminator.

The concrete message type is **injectable**, not hardwired. `IMessageFormat` (see [Control.md](Control.md)) is the control interface a host registers to supply its own DTO — it provides the type itself (`MessageType`, `CreateMessage()`) and maps the engine's logical fields onto that type's real fields:

```csharp
public interface IMessageFormat
{
    Type MessageType { get; }
    object CreateMessage();
    string GetMessageId(object message);
    void SetMessageId(object message, string value);
    string GetFromUser(object message);
    void SetFromUser(object message, string value);
    string GetSubject(object message);
    void SetSubject(object message, string value);
    string GetBody(object message);
    void SetBody(object message, string value);
    List<MessageAddress> GetAddresses(object message);
    void SetAddresses(object message, List<MessageAddress> value);
    DateTime GetSentAt(object message);
    void SetSentAt(object message, DateTime value);
    string GetConfirmationMessageId(object message);
    void SetConfirmationMessageId(object message, string value);
    bool GetIsAlert(object message);
    void SetIsAlert(object message, bool value);
}
```

Every layer that carries or stores a message — `PeerService`, `InterfaceService`, `MessageRoutingService`, `EntryService`, and `MessageEntity.Message` in the database (see [Data.md](Data.md)) — works purely in terms of `object`, calling into `IMessageFormat` for every logical field it needs. The engine has no message type of its own and never assumes a particular field name or wire layout beyond what a host's own `[ProtoContract]`/`[ProtoMember]` attributes declare on its DTO.

`IMessageFormat` is a **required** control interface (see [Control.md](Control.md#required-no-default)) — the engine ships no default implementation and no built-in message type at all. `EngineApplication.Start<TMessageFormat>` (the entry point used by `Sample`) takes the implementation as a required generic type parameter and registers it itself, so a host cannot omit it without a compile error; a host that bypasses `EngineApplication` and calls `UseEngine` directly without registering `IMessageFormat` fails at startup with a DI resolution error instead.

The `Sample` project registers `SampleMessageFormat` over a `SampleMessage` DTO (`Id`/`Sender`/`Title`/`Text`/`Recipients` fields) to demonstrate a working implementation — the mapping, not any particular field name, is what the engine actually depends on. See `Sample/src/SampleMessageFormat.cs`.

`SampleMessageFormat` derives from `MessageFormat<TMessage>` (see [Control.md](Control.md#imessageformat)) rather than implementing `IMessageFormat` directly — this abstract base class does the `object`-to-`TMessage` cast once and exposes type-safe `protected abstract` members instead, so implementations never write the cast themselves.

## Connection Lifecycle

`PeerService` wraps one `IOftPeer`, created via `IOftPeerFactory` and configured with `IOftCertificateProvider.GetPeerOptions()` (see [Oft.md](Oft.md)). The underlying `IOftPeer` transparently owns both directions — there is no separate inbound/outbound class:

### Inbound
1. `PeerService.Start` calls `IOftPeer.Listen` on `0.0.0.0:PeerPort` and blocks until cancelled.
2. `IOftPeer.ReceivedHandler` fires for every message received on any connection the peer holds (inbound or outbound), with the pooled payload copied out.
3. The copied bytes are deserialized as an instance of `IMessageFormat.MessageType` off the OFT callback thread and `MessageDelivered` fires with that `object`.
4. Deserialization failures are caught and simply dropped; OFT itself handles connection-level errors and retries.

### Outbound
- `PeerService.Send(userName, message)` takes `message` as `object` (an instance of `IMessageFormat.MessageType`), resolves `userName` to a `UserEndpoint` via `IUserLocator`, reads `IMessageFormat.GetMessageId(message)` for the delivery-status tag, then calls `IOftPeer.Send(host, port, data, tag: (messageId, userName))`.
- `IOftPeer` maintains its own connection cache internally, keyed by `host:port`, reusing an existing connection or creating one as needed.
- The `Send` call does not return until OFT has fully delivered the message (see [Delivery status](#delivery-status)) — a resolution failure (unknown user) or an OFT-level send failure (e.g. `OftDisconnectedException`) both cause `PeerService.Send` to return `false`.

## Delivery status

There is no application-level ack/confirm reply riding on the OFT delivery-status stream itself — that part of delivery status comes entirely from OFT's own `OftDeliveryStatus` stream. (A separate, later application-level reply — the user-read confirmation message — does exist; see [Read Confirmation](#read-confirmation) below.)

1. `PeerService.Send` tags every send with `(messageId, userName)` and sets a single `IOftPeer.DeliveryStatusHandler` (in the constructor, alongside `ReceivedHandler`) that decodes the tag and raises `IPeerService.DeliveryStatusChanged(messageId, userName, OftDeliveryStatus)`.
2. `MessageRoutingService` subscribes to that event and maps each `OftDeliveryStatus` to the app-level `DestinationStatus` used for persistence and UI:

   | `OftDeliveryStatus` | `DestinationStatus` |
   |---|---|
   | `Queued`, `Sending`, `Interrupted`, `Resumed` | `Sending` |
   | `Sent` | `Sent` |
   | `Acknowledged` | `Confirmed` |
   | `Cancelled` | `Failed` |
3. Because `IOftConnection.Send` only completes once OFT has fully acknowledged the message, `PeerService.Send` returning `true` already means the message reached `Acknowledged` — so `MessageRoutingService.Route`'s own per-user result (`UserDeliveryResult.Success`) is the message's final status, not an intermediate one. The `DeliveryStatusChanged` event exists for any consumer that wants to observe the in-flight transitions live, independent of when `Route` itself returns.

**Self-addressing**: If a recipient user name matches the sending user, no network connection is made — the message is delivered in-process (`IPeerService.DeliverLocal`) and `MessageRoutingService` raises `DeliveryStatusChanged` as `Confirmed` immediately.

## Read Confirmation

Beyond OFT's own delivery status, the engine tracks one more step per recipient: whether the user actually opened the message. This has two sides — the recipient's own **Received → Read** status on their Inbox record, and the sender's **Confirmed → Read** status on their Outbox record's per-user `DeliveryStatus`, driven by a confirmation message sent back over the wire. See [Data.md](Data.md#messageentity) for where these statuses live and `Docs/ViewModels.md` for the alert-message UI that also depends on this flow.

1. **Storage**: `EntryService.StoreIncomingMessage` sets a new Inbox record's `MessageEntity.ReadStatus` to `DestinationStatus.Received`. This status has no equivalent in an Outbox record's `DeliveryStatuses` — from the sender's perspective there is no separate "recipient received it" signal beyond the existing OFT `Confirmed` status.
2. **Reading**: When a Client-mode user opens an unread Inbox message (`ContentAreaViewModel.BuildMessageViewModel`), it calls `IServiceConnection.MarkMessageRead(messageId)`, which:
   - Calls `EntryService.MarkMessageRead`, transitioning `ReadStatus` from `Received` to `Read` and firing `EntryService.MessageRead` — a no-op (returns `null`) if the record is missing or already `Read`, so reopening an already-read message never re-sends a confirmation.
   - Builds a confirmation message via `IMessageFormat.CreateMessage()`, giving it its own fresh `MessageId` (an OFT delivery-status tag, unrelated to the message being confirmed), `FromUser` set to this user's own name, and `SetConfirmationMessageId(confirmation, messageId)` — every other logical field (subject, body, addresses) is left at its default, since a confirmation carries only this one piece of information.
   - Sends the confirmation to the original message's `FromUser` via `IPeerService.Send` directly — bypassing `MessageRoutingService.Route`, since there is no address expansion, persistence, or delivery-status seeding to do for a confirmation. For a self-addressed message (`FromUser` equals this user's own name), it instead calls `EntryService.UpdateDeliveryStatus` directly with no network round-trip, mirroring `MessageRoutingService.Route`'s own self-delivery bypass.
3. **Receiving the confirmation**: `PeerService.HandleMessage` checks `IMessageFormat.GetConfirmationMessageId` on every deserialized message *before* treating it as an ordinary message. If non-empty, it raises `ConfirmationReceived(messageId, confirmingUser)` instead of `MessageDelivered` — a confirmation is never mirrored to interface connections or shown as a new Inbox message.
4. **Advancing the sender's status**: `MessageRoutingService` subscribes to `PeerService.ConfirmationReceived` and re-raises its own `DeliveryStatusChanged(messageId, confirmingUser, DestinationStatus.Read)` — reusing the exact same event, and therefore the exact same `DirectServiceConnection`/`EntryService.UpdateDeliveryStatus`/UI-update pipeline, as an ordinary OFT delivery-status change (see [Delivery status](#delivery-status) above). `MessageEntity.OverallStatus` only reports `Read` once every recipient's per-user status is `Read`; it stays `Confirmed` while any recipient has confirmed but not yet read.

## Alert Messages

An alert is an ordinary message with `IMessageFormat.GetIsAlert` set to `true` — nothing about its wire format, routing, or storage differs from a non-alert message. The only difference is client-side: a Client-mode UI that receives an alert message alarms (a red box in the title bar, plus a looping sound) until the user reads it, via the same Read Confirmation flow described above. See `Docs/ViewModels.md` for `AlertViewModel` and the `IAlertConfiguration`/`IAlertSoundPlayer` control interfaces that drive this.

## Events

| Event | Raised by | Consumed by |
|-------|-----------|-------------|
| `PeerService.MessageDelivered` | PeerService | DirectServiceConnection, InterfaceService |
| `PeerService.ConfirmationReceived` | PeerService | MessageRoutingService |
| `PeerService.DeliveryStatusChanged` | PeerService | MessageRoutingService |
| `MessageRoutingService.DeliveryStatusChanged` | MessageRoutingService | DirectServiceConnection → IServiceConnection consumers |
| `EntryService.MessageRead` | EntryService | AlertViewModel |
