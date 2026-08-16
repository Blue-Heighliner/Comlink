# Peer Networking

The peer layer handles node-to-node message delivery. Every running instance — in both `Client` and `Headless` modes — runs an `IPeerService` and exposes the same `MessageDelivered`/`ConfirmationReceived`/`DeliveryStatusChanged`/`Send`/`DeliverLocal` surface to the rest of the engine (`MessageRoutingService`, `InterfaceService`, `EntryService`) regardless of topology. Which concrete implementation is registered is controlled by `IEngineController.Role` (see [Node Roles](#node-roles) below) — the default, `NodeRole.Peer`, is direct peer-to-peer networking via `PeerService`, described in the rest of this document. Every instance also runs `InterfaceService`, which hosts a local interface listener that mirrors and injects into this same message stream, regardless of mode or role — see [Interface.md](Interface.md).

## Node Roles

`NodeRole` (`IEngineController`, see [Control.md](Control.md)) selects one of three networking topologies for a running instance. It is resolved once, synchronously, from `EngineConfig.NodeRole` at DI composition time in `EngineExtensions.UseEngine` — before the convention scanner runs — so the correct `IPeerService` implementation is registered from the start; nothing re-checks the role at runtime.

| Role | `IPeerService` implementation | Behavior |
|------|-------------------------------|----------|
| `Peer` (default) | `PeerService` | Direct peer-to-peer, as described in the rest of this document. |
| `Client` | `ClientPeerService` | All traffic flows through one long-term connection to a configured server. |
| `Server` | `ServerRoutingService` | Routes between this server's child clients and other servers. |

### Client

A `Client`-role instance has the same inbox/outbox/notes/drafts GUI and application flow as `Peer` — `MessageRoutingService`, `EntryService`, and the ViewModels are unaware of the difference — except for one addition: a single connection-status row pinned to the bottom of the window, tracking the connection described below (see [IConnectionStatusViewModel](ViewModels.md#iconnectionstatusviewmodel--connectionstatusviewmodel) and [Connection Status](#connection-status-client-and-server) below). `ClientPeerService` replaces per-recipient direct connections with a single long-term outbound [OFT](Oft.md) `IOftConnection` to the server endpoint from `IEngineController`:

1. `Start` loops indefinitely: dial the server via `IOftConnector.Connect` using `IEngineController.ConnectionOptions`, then await the connection's own `DisconnectedHandler` firing. Whether the dial fails or an established connection later disconnects, the loop simply retries after a fixed interval — there is no give-up condition.
2. `Send(userName, message)` ignores `userName` for addressing purposes — the message is transmitted as-is over the one shared connection, and the *server* performs the actual user-to-connection routing (see [Server](#server) below). Because `MessageRoutingService.Route` still calls `IPeerService.Send` once per resolved recipient (e.g. once per member of an addressed group), `ClientPeerService` coalesces concurrent `Send` calls that share the same `IEngineController.GetMessageId(message)` into a single physical transmission, so a group-addressed message is not sent to the server multiple times.
3. Inbound messages received over the connection are dispatched through the same confirmation-vs-ordinary classification `PeerService` uses (shared via `PeerMessageDispatcher`), raising `MessageDelivered`/`ConfirmationReceived` identically.
4. `Send` returns `false` immediately whenever no connection is currently established — it does not block waiting for a reconnect.

No per-message OFT delivery-status tracking is performed across the hop to the server — `DeliveryStatusChanged` is declared (to satisfy `IPeerService`) but never raised.

### Server

A `Server`-role instance has no inbox/outbox/notes/drafts GUI at all — `MainWindow` shows up to two connections tables instead of the normal 3-panel layout and hides the title bar's compose/export/import/print controls entirely, since `ServerRoutingService` is a routing hub between this server's child clients and every other server in the cluster, not a message-composing peer (see [Connection Status](#connection-status-client-and-server) below). It is driven entirely by `IEngineController.Servers`, a map keyed by server user name describing the **whole cluster topology**: every server's listen endpoint and full child-client list, not just the local server's own.

1. **Startup**: `Start` looks up this instance's own entry (by `ICurrentUserProvider.UserName`) to find its listen endpoint, then calls `IOftHoster.Host` on it — the same endpoint child clients dial in on *and* that other servers dial in on. For every *other* server in the map, it spawns an independent retry loop (`IOftConnector.Connect`, same disconnect-and-retry pattern as Client) forming a full mesh of outbound server-to-server connections.
2. **Classifying inbound connections**: `IOftListener.ConnectedHandler` inspects each accepted connection's `Identity.Info` (the remote side's hail payload, which — via `IEngineController.ConnectionOptions` — carries its user name) against this server's own `ChildClients` list and the user map's server names:
   - A known child client is tracked and its `ReceivedHandler` routed to the from-child path.
   - A known other server is used purely as an inbound receive source — the corresponding *outbound* leg the retry loop already owns is what's used to *send* to that server, so each ordered server pair can have both an inbound- and outbound-established connection without conflict.
   - Anything else is disposed and ignored.
3. **Routing from a child client**: the raw address list (`IEngineController.GetAddresses`, unexpanded — group addressing is not resolved at the server) is checked against (a) this server's other children, each addressed one delivered to directly, and (b) every other server that owns at least one addressed child, each forwarded the same raw bytes exactly once regardless of how many of its children are addressed.
4. **Routing from another server**: assumed already routed by that server — only delivered to this server's own children that are addressed, and never re-forwarded to any other server, so a message can never loop.
5. Messages are relayed as raw bytes (re-deserialized only to read `GetAddresses`/`GetPriority`) — a server does not persist, mirror to interfaces, or otherwise treat routed traffic as its own inbox, and `ConfirmationReceived`/`DeliveryStatusChanged` are never raised for it.

`IPeerService.Send`/`DeliverLocal` are still implemented (an instance in `Server` role composing its own message is treated exactly like a message arriving from a child), for interface completeness, though this is not part of the role's intended usage.

### Retry behavior (Client and Server)

Both `ClientPeerService`'s connection to its server and `ServerRoutingService`'s per-remote-server connections use the same shape of retry loop: dial, and if that succeeds, wait for the connection's `DisconnectedHandler` to fire; either a failed dial or a later disconnect leads back to a fixed retry delay before dialing again, forever, until the instance shuts down. There is no backoff and no maximum retry count — a server or client that is temporarily unreachable is expected to eventually come back.

### Connection Status (Client and Server)

`ClientPeerService` and `ServerRoutingService` each also implement `Peer.IConnectionStatusService` directly, tracking every connect and disconnect they already drive above so the UI can display it live (see [IConnectionStatusViewModel](ViewModels.md#iconnectionstatusviewmodel--connectionstatusviewmodel)). Each reported `Peer.PeerConnectionStatus` carries a `Peer.PeerConnectionKind` (`Server` or `Client`), which the ViewModel layer uses to split rows into two separate tables:

- **Client**: one `Server`-kind entry for its single server connection — `UserName` is the server's own hailed identity (`IOftConnection.Identity.Info`, populated the moment the connection is established; empty string beforehand), `IsConnected` reflects whether the connection is currently live, and `LastConnectedAt`/`LastDisconnectedAt` record when it last transitioned. There is never a `Client`-kind entry, since a client has no children of its own.
- **Server**: one `Client`-kind entry per own child client (from `ChildClients` on this server's own entry in `IEngineController.Servers`) plus one `Server`-kind entry per other server in the cluster — the same `UserName`/`IsConnected`/`LastConnectedAt`/`LastDisconnectedAt` shape, tracked independently per remote name. `MainWindow` shows the `Server`-kind entries in a "SERVERS" table and the `Client`-kind entries in a "CLIENTS" table below it, each hidden outright while it has no rows (e.g. a standalone server with no peer servers configured shows only the client table, and vice versa).

`NodeRole.Peer` registers `Peer.NullConnectionStatusService` instead (always an empty list) — direct peer-to-peer connections are formed ad hoc per send, not configured, long-term links worth a status row.

## Message Format

Peer traffic carries exactly one payload shape and nothing else: an instance of `IEngineController.MessageType`, serialized with **protobuf-net** (binary), via `PeerSerializer` working from that runtime `Type` rather than a compile-time generic parameter. There is no envelope and no message-type discriminator on the wire. OFT-level delivery (did the bytes arrive) is tracked entirely through OFT's own delivery status (see [Delivery status](#delivery-status) below); the only application-level reply that exists is the user-read confirmation described in [Read Confirmation](#read-confirmation), and it is itself just an ordinary instance of `IEngineController.MessageType` with one field set — there is still no separate envelope or command discriminator.

The concrete message type is **injectable**, not hardwired. The message-format members of `IEngineController` (see [Control.md](Control.md#message-format)) are what a host registers to supply its own DTO — they provide the type itself (`MessageType`, `CreateMessage()`) and map the engine's logical fields onto that type's real fields:

```csharp
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
int GetPriority(object message);
void SetPriority(object message, int value);
string GetTag(object message);
void SetTag(object message, string value);
```

Every layer that carries or stores a message — `PeerService`, `InterfaceService`, `MessageRoutingService`, `EntryService`, and `MessageEntity.Message` in the database (see [Data.md](Data.md)) — works purely in terms of `object`, calling into `IEngineController` for every logical field it needs. The engine has no message type of its own and never assumes a particular field name or wire layout beyond what a host's own `[ProtoContract]`/`[ProtoMember]` attributes declare on its DTO.

These members are **required**, with no generic-free default (see [Control.md](Control.md#message-format)) — `DefaultEngineController<TMessage>` declares them `protected abstract`, so the class itself is `abstract` and a host must always define and register a subclass implementing them; a host that registers no `IEngineController` at all fails at startup with a DI resolution error.

The `Sample` project's `SampleEngineController` maps every logical field onto a `SampleMessage` DTO (`Id`/`Sender`/`Title`/`Text`/`Recipients` fields) to demonstrate a working implementation — the mapping, not any particular field name, is what the engine actually depends on. See `Sample/src/SampleEngineController.cs`.

`SampleEngineController` derives from `DefaultEngineController<TMessage>` (see [Control.md](Control.md#message-format)), which does the `object`-to-`TMessage` cast for the message-field members once on your behalf and exposes type-safe `protected abstract` members instead, so implementations never write the cast themselves.

## Connection Lifecycle

`PeerService` wraps one `IOftPeer`, created via `IOftPeerFactory` and configured with `IEngineController.ConnectionOptions` (see [Oft.md](Oft.md)). The underlying `IOftPeer` transparently owns both directions — there is no separate inbound/outbound class:

### Inbound
1. `PeerService.Start` calls `IOftPeer.Listen` on `0.0.0.0:PeerPort` and blocks until cancelled.
2. `IOftPeer.ReceivedHandler` fires for every message received on any connection the peer holds (inbound or outbound), with the pooled payload copied out.
3. The copied bytes are deserialized as an instance of `IEngineController.MessageType` off the OFT callback thread and `MessageDelivered` fires with that `object`.
4. Deserialization failures are caught and simply dropped; OFT itself handles connection-level errors and retries.

### Outbound
- `PeerService.Send(userName, message)` takes `message` as `object` (an instance of `IEngineController.MessageType`), resolves `userName` to a `UserEndpoint` via `IEngineController`, reads `IEngineController.GetMessageId(message)` for the delivery-status tag, then calls `IOftPeer.Send(host, port, data, priority: IEngineController.GetPriority(message), tag: (messageId, userName))`. `IEngineController.GetPriority(message)` is passed verbatim as OFT's own `priority` argument — larger values are sent first by OFT — so the message's stored priority number (see `IEngineController` in `Docs/Control.md`) directly controls OFT-level send ordering, independent of the same value also being embedded inside the serialized message content itself.
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
   - Builds a confirmation message via `IEngineController.CreateMessage()`, giving it its own fresh `MessageId` (an OFT delivery-status tag, unrelated to the message being confirmed), `FromUser` set to this user's own name, and `SetConfirmationMessageId(confirmation, messageId)` — every other logical field (subject, body, addresses) is left at its default, since a confirmation carries only this one piece of information.
   - Sends the confirmation to the original message's `FromUser` via `IPeerService.Send` directly — bypassing `MessageRoutingService.Route`, since there is no address expansion, persistence, or delivery-status seeding to do for a confirmation. For a self-addressed message (`FromUser` equals this user's own name), it instead calls `EntryService.UpdateDeliveryStatus` directly with no network round-trip, mirroring `MessageRoutingService.Route`'s own self-delivery bypass.
3. **Receiving the confirmation**: `PeerService.HandleMessage` checks `IEngineController.GetConfirmationMessageId` on every deserialized message *before* treating it as an ordinary message. If non-empty, it raises `ConfirmationReceived(messageId, confirmingUser)` instead of `MessageDelivered` — a confirmation is never mirrored to interface connections or shown as a new Inbox message.
4. **Advancing the sender's status**: `MessageRoutingService` subscribes to `PeerService.ConfirmationReceived` and re-raises its own `DeliveryStatusChanged(messageId, confirmingUser, DestinationStatus.Read)` — reusing the exact same event, and therefore the exact same `DirectServiceConnection`/`EntryService.UpdateDeliveryStatus`/UI-update pipeline, as an ordinary OFT delivery-status change (see [Delivery status](#delivery-status) above). `MessageEntity.OverallStatus` only reports `Read` once every recipient's per-user status is `Read`; it stays `Confirmed` while any recipient has confirmed but not yet read.

## Alert Messages

An alert is an ordinary message with `IEngineController.GetIsAlert` set to `true` — nothing about its wire format, routing, or storage differs from a non-alert message. The only difference is client-side: a Client-mode UI that receives an alert message alarms (a red box in the title bar, plus a looping sound) until the user reads it, via the same Read Confirmation flow described above. See `Docs/ViewModels.md` for `AlertViewModel` and the `IEngineController` members that drive this.

## Events

| Event | Raised by | Consumed by |
|-------|-----------|-------------|
| `PeerService.MessageDelivered` | PeerService | DirectServiceConnection, InterfaceService |
| `PeerService.ConfirmationReceived` | PeerService | MessageRoutingService |
| `PeerService.DeliveryStatusChanged` | PeerService | MessageRoutingService |
| `MessageRoutingService.DeliveryStatusChanged` | MessageRoutingService | DirectServiceConnection → IServiceConnection consumers |
| `EntryService.MessageRead` | EntryService | AlertViewModel |
