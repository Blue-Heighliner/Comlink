# Peer Networking

The peer layer handles direct node-to-node message delivery. Every running instance — in both `Client` and `Headless` modes — runs a `PeerService` that wraps a single [OFT](Oft.md) `IOftPeer`, accepting inbound connections from other nodes and sending outbound ones. Every instance also runs `InterfaceService`, which hosts a local interface listener that mirrors and injects into this same message stream, regardless of mode — see [Interface.md](Interface.md).

## Message Format

Peer traffic carries exactly one payload shape and nothing else: an instance of `IMessageFormat.MessageType`, serialized with **protobuf-net** (binary), via `PeerSerializer` working from that runtime `Type` rather than a compile-time generic parameter. There is no envelope, no message-type discriminator, and no application-level acknowledgement or confirmation reply — delivery is tracked entirely through OFT's own delivery status (see [Delivery status](#delivery-status) below).

The concrete message type is **injectable**, not hardwired. `IMessageFormat` (see [Control.md](Control.md)) is the control interface a host registers to supply its own DTO — it provides the type itself (`MessageType`, `CreateMessage()`) and maps the engine's logical fields onto that type's real fields:

```csharp
public interface IMessageFormat
{
    Type MessageType { get; }
    object CreateMessage();
    string GetMessageId(object message);
    void SetMessageId(object message, string value);
    string GetFromSite(object message);
    void SetFromSite(object message, string value);
    string GetSubject(object message);
    void SetSubject(object message, string value);
    string GetBody(object message);
    void SetBody(object message, string value);
    List<MessageAddress> GetAddresses(object message);
    void SetAddresses(object message, List<MessageAddress> value);
    DateTime GetSentAt(object message);
    void SetSentAt(object message, DateTime value);
}
```

Every layer that carries or stores a message — `PeerService`, `InterfaceService`, `MessageRoutingService`, `EntryService`, and `MessageEntity.Message` in the database (see [Data.md](Data.md)) — works purely in terms of `object`, calling into `IMessageFormat` for every logical field it needs. The engine has no message type of its own and never assumes a particular field name or wire layout beyond what a host's own `[ProtoContract]`/`[ProtoMember]` attributes declare on its DTO.

`IMessageFormat` is a **required** control interface (see [Control.md](Control.md#required-no-default)) — the engine ships no default implementation and no built-in message type at all. `EngineApplication.Start<TMessageFormat>` (the entry point used by `Sample`) takes the implementation as a required generic type parameter and registers it itself, so a host cannot omit it without a compile error; a host that bypasses `EngineApplication` and calls `UseEngine` directly without registering `IMessageFormat` fails at startup with a DI resolution error instead.

The `Sample` project registers `SampleMessageFormat` over a `SampleMessage` DTO (`Id`/`Sender`/`Title`/`Text`/`Recipients` fields) to demonstrate a working implementation — the mapping, not any particular field name, is what the engine actually depends on. See `Sample/src/SampleMessageFormat.cs`.

## Connection Lifecycle

`PeerService` wraps one `IOftPeer`, created via `IOftPeerFactory` and configured with `IOftCertificateProvider.GetPeerOptions()` (see [Oft.md](Oft.md)). The underlying `IOftPeer` transparently owns both directions — there is no separate inbound/outbound class:

### Inbound
1. `PeerService.Start` calls `IOftPeer.Listen` on `0.0.0.0:PeerPort` and blocks until cancelled.
2. `IOftPeer.ReceivedHandler` fires for every message received on any connection the peer holds (inbound or outbound), with the pooled payload copied out.
3. The copied bytes are deserialized as an instance of `IMessageFormat.MessageType` off the OFT callback thread and `MessageDelivered` fires with that `object`.
4. Deserialization failures are caught and simply dropped; OFT itself handles connection-level errors and retries.

### Outbound
- `PeerService.Send(siteName, message)` takes `message` as `object` (an instance of `IMessageFormat.MessageType`), resolves `siteName` to a `SiteEndpoint` via `ISiteLocator`, reads `IMessageFormat.GetMessageId(message)` for the delivery-status tag, then calls `IOftPeer.Send(host, port, data, tag: (messageId, siteName))`.
- `IOftPeer` maintains its own connection cache internally, keyed by `host:port`, reusing an existing connection or creating one as needed.
- The `Send` call does not return until OFT has fully delivered the message (see [Delivery status](#delivery-status)) — a resolution failure (unknown site) or an OFT-level send failure (e.g. `OftDisconnectedException`) both cause `PeerService.Send` to return `false`.

## Delivery status

There is no application-level ack/confirm reply on the wire. Delivery status comes entirely from OFT's own `OftDeliveryStatus` stream:

1. `PeerService.Send` tags every send with `(messageId, siteName)` and sets a single `IOftPeer.DeliveryStatusHandler` (in the constructor, alongside `ReceivedHandler`) that decodes the tag and raises `IPeerService.DeliveryStatusChanged(messageId, siteName, OftDeliveryStatus)`.
2. `MessageRoutingService` subscribes to that event and maps each `OftDeliveryStatus` to the app-level `DestinationStatus` used for persistence and UI:

   | `OftDeliveryStatus` | `DestinationStatus` |
   |---|---|
   | `Queued`, `Sending`, `Interrupted`, `Resumed` | `Sending` |
   | `Sent` | `Sent` |
   | `Acknowledged` | `Confirmed` |
   | `Cancelled` | `Failed` |
3. Because `IOftConnection.Send` only completes once OFT has fully acknowledged the message, `PeerService.Send` returning `true` already means the message reached `Acknowledged` — so `MessageRoutingService.Route`'s own per-site result (`SiteDeliveryResult.Success`) is the message's final status, not an intermediate one. The `DeliveryStatusChanged` event exists for any consumer that wants to observe the in-flight transitions live, independent of when `Route` itself returns.

**Self-addressing**: If a recipient site name matches the sending site, no network connection is made — the message is delivered in-process (`IPeerService.DeliverLocal`) and `MessageRoutingService` raises `DeliveryStatusChanged` as `Confirmed` immediately.

## Events

| Event | Raised by | Consumed by |
|-------|-----------|-------------|
| `PeerService.MessageDelivered` | PeerService | DirectServiceConnection, InterfaceService |
| `PeerService.DeliveryStatusChanged` | PeerService | MessageRoutingService |
| `MessageRoutingService.DeliveryStatusChanged` | MessageRoutingService | DirectServiceConnection → IServiceConnection consumers |
