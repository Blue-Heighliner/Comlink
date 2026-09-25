# Peer Networking

The peer layer handles node-to-node message delivery. Every running instance - in both `Client` and `Headless` modes - runs an `IPeerService` and exposes the same `MessageDelivered`/`ConfirmationReceived`/`DeliveryStatusChanged`/`Send`/`DeliverLocal` surface to the rest of the engine (`MessageRoutingService`, `EntryService`) regardless of topology. Which concrete implementation is registered is controlled by `IEngineController.Role` (see [Node Roles](#node-roles) below) - the default, `NodeRole.Peer`, is direct peer-to-peer networking via `PeerService`, described in the rest of this document. None of these services talks to MSMT or a serial port itself: they send and receive through an `IPeerTransport`, which reaches each user over IP or over a MicroGate serial cable depending on that user's configured endpoint (see Transport.md). Every instance also runs `InterfaceService`, which hosts a local interface listener that injects into this same message stream (routing a message out exactly as if the local user had composed it), regardless of mode or role - it does not mirror inbound peer messages back out to it; see [Interface.md](Interface.md).

## Node Roles

`NodeRole` (`IEngineController`, see [Control.md](Control.md)) selects one of three networking topologies for a running instance. It is resolved once, synchronously, from `EngineConfig.NodeRole` at DI composition time in `EngineExtensions.UseEngine` — before the convention scanner runs — so the correct `IPeerService` implementation is registered from the start; nothing re-checks the role at runtime.

| Role | `IPeerService` implementation | Behavior |
|------|-------------------------------|----------|
| `Peer` (default) | `PeerService` | Direct peer-to-peer, as described in the rest of this document. |
| `Client` | `ClientPeerService` | All traffic flows through one long-term connection to a configured server. |
| `Server` | `ServerRoutingService` | Routes between this server's child clients and other servers. |

### Client

A `Client`-role instance has the same inbox/outbox/notes/drafts GUI and application flow as `Peer` - `MessageRoutingService`, `EntryService`, and the ViewModels are unaware of the difference - except for one addition: a single connection-status row pinned to the bottom of the window, tracking the connection described below (see [IConnectionStatusViewModel](ViewModels.md#iconnectionstatusviewmodel--connectionstatusviewmodel) and [Connection Status](#connection-status-client-and-server) below). `ClientPeerService` sends every outbound message to the configured server endpoint from `IEngineController`, and - because [MSMT](MsmtIntegration.md#no-server-initiated-delivery)'s client-request/server-response model means the server can never push a message down a connection it did not itself initiate - also runs its own IP listener so the server can dial back in to deliver messages to it (a serial server link is one bidirectional cable, so it needs none):

1. `Start` resolves `IEngineController.ServerEndpoint` (logging an error and returning without starting if none is configured), creates the transport, and, when the server endpoint is IP, calls `StartListener` on `IEngineController.PeerPort`.
2. `Send(userName, message)` ignores `userName` for addressing purposes — the message is transmitted as-is to the server, and the *server* performs the actual user-to-connection routing (see [Server](#server) below). Because `MessageRoutingService.Route` still calls `IPeerService.Send` once per resolved recipient (e.g. once per member of an addressed group), `ClientPeerService` coalesces concurrent `Send` calls that share the same `IEngineController.GetMessageId(message)` into a single physical transmission, so a group-addressed message is not sent to the server multiple times.
3. Inbound messages received over the peer's receiver are dispatched through the same confirmation-vs-ordinary classification `PeerService` uses (shared via `PeerMessageDispatcher`), raising `MessageDelivered`/`ConfirmationReceived` identically.
4. `Send` returns `false` immediately if `Start` never successfully created a transport (no configured endpoint).
5. A background `PeerConnectionMonitor` proactively opens and maintains a connection to the server - see [Connection Status](#connection-status-client-and-server) below.

No per-message MSMT delivery-status tracking is performed across the hop to the server — `DeliveryStatusChanged` is declared (to satisfy `IPeerService`) but never raised.

### Server

A `Server`-role instance has no inbox/outbox/notes/drafts GUI at all — `MainWindow` shows up to two connections tables instead of the normal 3-panel layout and hides the title bar's compose/export/import/print controls entirely, since `ServerRoutingService` is a routing hub between this server's child clients and every other server in the cluster, not a message-composing peer (see [Connection Status](#connection-status-client-and-server) below). It is driven entirely by `IEngineController.Servers`, a map keyed by server user name describing the **whole cluster topology**: every server's listen endpoint and full child-client list, not just the local server's own.

1. **Startup**: `Start` looks up this instance's own entry (by `ICurrentUserProvider.UserName`) to find its listen endpoint, creates the transport, and, when that endpoint is IP, calls `StartListener` on its port - the endpoint child clients and other servers connect to (a serial own endpoint has no address to listen on, so none is started). It then starts a background `PeerConnectionMonitor` per own child and per sibling server, proactively dialing each one - see [Connection Status](#connection-status-client-and-server) below. Real message delivery (to a child or to another server) is otherwise unaffected by this and still happens on demand, by dialing out at the moment routing actually needs to.
2. **Classifying connections**: over IP, `IPeerTransport.Connected` for an inbound connection (one this server's listener accepted rather than one it dialed out itself) inspects the connection's `Identity.Subject` (a distinguished-name string, e.g. `CN=Client1`) against this server's own `ChildClients` list and the user map's server names, by comparing its common name to what `IEngineController.GetCertificateName` would produce for each candidate name:
   - A known child client is tracked and its messages routed to the from-child path.
   - A known other server is tracked and its messages routed to the from-server path.
   - Anything else is dropped (`IMsmtConnection.Drop`) and ignored.
3. **Routing from a child client**: the raw address list (`IEngineController.GetAddresses`, unexpanded — group addressing is not resolved at the server) is checked against (a) this server's other children, each addressed one delivered to directly by dialing its own endpoint (`IEngineController.GetEndpoint`), and (b) every other server that owns at least one addressed child, each forwarded the same raw bytes exactly once — by dialing that server's own endpoint — regardless of how many of its children are addressed. `IEngineController.Servers`/`ServerUsers` only supplies each **server's** own listen endpoint, not its children's — a server's own `Users`/`GetEndpoint` directory must separately list an endpoint for each of its own `ChildClients` (matching that child's own `PeerPort`), or delivery to that child silently no-ops (`GetEndpoint` returns `null`, `TrySendToChild` returns without sending or logging). A child never needs its server's own children in its own `Users` map — only the server does.
4. **Routing from another server**: assumed already routed by that server — only delivered to this server's own children that are addressed, by dialing each one's own endpoint, and never re-forwarded to any other server, so a message can never loop.
5. Messages are relayed as raw bytes (re-deserialized only to read `GetAddresses`/`GetPriority`) — a server does not persist, mirror to interfaces, or otherwise treat routed traffic as its own inbox, and `ConfirmationReceived`/`DeliveryStatusChanged` are never raised for it.

`IPeerService.Send`/`DeliverLocal` are still implemented (an instance in `Server` role composing its own message is treated exactly like a message arriving from a child), for interface completeness, though this is not part of the role's intended usage.

### Connection Status (Client and Server)

`ClientPeerService` and `ServerRoutingService` each also implement `Peer.IConnectionStatusService` directly, subscribing to their peer's `Connected`/`Disconnected` events so the UI can display live status (see [IConnectionStatusViewModel](ViewModels.md#iconnectionstatusviewmodel--connectionstatusviewmodel)). Each reported `Peer.PeerConnectionStatus` carries a `Peer.PeerConnectionKind` (`Server` or `Client`), which the ViewModel layer uses to split rows into two separate tables:

- **Client**: one `Server`-kind entry for its single server connection - `UserName` is always the empty string (MSMT connections carry no self-declared identity the way the transport this replaced did), `IsConnected` reflects whether the connection to the server's configured endpoint is currently live (matched by endpoint against `IEngineController.ServerEndpoint`, whether IP or serial), and `LastConnectedAt`/`LastDisconnectedAt` record when it last transitioned. There is never a `Client`-kind entry, since a client has no children of its own.
- **Server**: one `Client`-kind entry per own child client (from `ChildClients` on this server's own entry in `IEngineController.Servers`) plus one `Server`-kind entry per other server in the cluster — the same `IsConnected`/`LastConnectedAt`/`LastDisconnectedAt` shape, tracked independently per remote name (a child's row reflects its own *inbound* — `Receiver`-linked — connection; another server's row reflects whichever direction — inbound or outbound — most recently connected/disconnected, matched by certificate subject for inbound and by target endpoint for outbound). `MainWindow` shows the `Server`-kind entries in a "SERVERS" table and the `Client`-kind entries in a "CLIENTS" table below it, each hidden outright while it has no rows (e.g. a standalone server with no peer servers configured shows only the client table, and vice versa).

`NodeRole.Peer` registers `Peer.NullConnectionStatusService` instead (always an empty list) — direct peer-to-peer connections are formed ad hoc per send, not configured, long-term links worth a status row.

**Keeping a hierarchical connection genuinely live**: `IEngineController.ConnectionOptions` always configures `MsmtOperationMode.Session` (see [MsmtIntegration.md](MsmtIntegration.md#session-mode)), so a connection persists across multiple sends instead of closing after each one. Without something to actually send, though, a connection would only ever open the moment a real message needed to be routed - leaving the status table showing every row disconnected for as long as the app happens to be idle. A background `PeerConnectionMonitor`, started per hierarchical target (`ClientPeerService`'s one server; `ServerRoutingService`'s own children and sibling servers), avoids this by sending an empty heartbeat payload immediately on startup and repeatedly thereafter:

- A heartbeat reuses the exact same on-demand, Session-mode connection `IPeerTransport.Request` would use for a real message - so it establishes and keeps the connection open, observed the normal way through `Connected`/`Disconnected`, rather than opening a separate side channel. Over serial, the first heartbeat is what opens the link to the port, which then reconnects on its own.
- `IMsmtPeer.Test` is deliberately **not** used for this: `Test` opens and immediately closes its own one-shot connection every call, which — because the remote peer's receiver can't distinguish a reachability probe from a real connection until it reads the first message — would make the remote side's own `Connected`/`Disconnected` events flap on every check, corrupting its status table instead of stabilizing it.
- An empty payload is recognized as a heartbeat and never reaches application logic: `PeerMessageDispatcher.Dispatch` returns immediately without deserializing it, and `ServerRoutingService.OnReceived` skips it before any routing decision, so it is never mistaken for a real (if malformed) message, never logged as "received", and never relayed.
- Each heartbeat's outcome governs how soon the next one fires: while the last heartbeat did not succeed — including the very first one, which commonly races the remote peer's own receiver still starting up, since a fresh `ECONNREFUSED` is near-instant rather than a slow timeout — the next retry follows quickly (2 seconds by default) instead of waiting on the full steady-state interval (30 seconds by default). The same fast retry re-engages immediately after any later drop, so a hierarchical connection's status table reflects a transient startup race, or a genuine mid-session disconnect, within a couple of seconds rather than sitting incorrectly "down" for up to 30.

## Message Format

Peer traffic carries exactly one payload shape and nothing else: an instance of `IEngineController.MessageType`, serialized with **protobuf-net** (binary), via `PeerSerializer` working from that runtime `Type` rather than a compile-time generic parameter. There is no envelope and no message-type discriminator on the wire. MSMT-level delivery (did the bytes arrive) is tracked entirely through MSMT's own delivery status (see [Delivery status](#delivery-status) below); the only application-level reply that exists is the user-read confirmation described in [Read Confirmation](#read-confirmation), and it is itself just an ordinary instance of `IEngineController.MessageType` with one field set — there is still no separate envelope or command discriminator.

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

`PeerService` wraps one `IPeerTransport`, created via `IPeerTransportFactory` once a current user is registered. Over IP that is an MSMT peer configured with `IEngineController.ConnectionOptions` (see [MsmtIntegration.md](MsmtIntegration.md)); over serial it is one MicroGate link per configured serial user. The transport transparently owns both directions - there is no separate inbound/outbound class:

### Inbound
1. `PeerService.Start` calls `IPeerTransport.StartListener` on `PeerPort` (all interfaces, IP only), opens the link to every configured user reached over serial so their messages are received before anything is sent to them, and blocks until cancelled. If no identity certificate is available the IP half is left out, with a warning, and serial still works.
2. `IPeerTransport.Received` publishes for every message received on any connection (IP or serial), with the payload copied out; the subscriber never rejects, so the transport acknowledges once every subscriber has run, since transport-level acceptance and application-level message validity are not distinguished here.
3. The copied bytes are deserialized as an instance of `IEngineController.MessageType` and `MessageDelivered` fires with that `object`.
4. Deserialization failures are caught and simply dropped; the transport itself handles connection-level errors and reconnects (MSMT on the next send to the same target, a serial link on its own).

### Outbound
- `PeerService.Send(userName, message)` takes `message` as `object` (an instance of `IEngineController.MessageType`), resolves `userName` to a `UserEndpoint` via `IEngineController`, reads `IEngineController.GetMessageId(message)` for the delivery-status tag, then calls `IPeerTransport.Request(endpoint, data, new PeerSendOptions { Priority = IEngineController.GetPriority(message), Transmitted = ... })`. `IEngineController.GetPriority(message)` is passed verbatim as the send priority - over IP higher values are sent first by MSMT, while a serial link sends in call order and ignores it - independent of the same value also being embedded inside the serialized message content itself.
- Over IP, the MSMT peer maintains its own connection cache internally, keyed by target host/port, reusing an existing connection or creating one as needed. Over serial, one persistent link per port and address is kept up by the transport.
- `IPeerTransport.Request` asynchronously awaits the remote node's acknowledgement itself - either returning whether the remote node accepted the message or throwing if the send never got that far (e.g. the target refused the connection, or the serial link is down) - so `PeerService.Send` does not return until the message has been fully delivered (see [Delivery status](#delivery-status)): a resolution failure (unknown user), a thrown exception, or a negatively-acknowledged send all cause it to return `false`.

## Delivery status

There is no application-level ack/confirm reply riding on the transport's delivery-status stream itself - that part of delivery status comes entirely from the transport's own `Request` outcome and its `Transmitted` progress callback. (A separate, later application-level reply - the user-read confirmation message - does exist; see [Read Confirmation](#read-confirmation) below.)

1. `PeerService.Send` tags every send with `(messageId, userName)`, passes a `Transmitted` callback for the intermediate `Sent` status, and derives the final status directly from the awaited `IPeerTransport.Request` outcome - raising `IPeerService.DeliveryStatusChanged(messageId, userName, DestinationStatus)` in both cases; there is no separate transport-level enum exposed through `IPeerService`.

   | Transport outcome | `DestinationStatus` |
   |---|---|
   | `Transmitted` callback (payload handed off, awaiting acknowledgement) | `Sent` |
   | `Request` returns `true` | `Confirmed` |
   | `Request` returns `false`, or throws | `Failed` |
2. Because `PeerService.Send` awaits `Request`'s outcome itself, returning `true` already means the message reached `Confirmed` — so `MessageRoutingService.Route`'s own per-user result (`UserDeliveryResult.Success`) is the message's final status, not an intermediate one. The `DeliveryStatusChanged` event exists for any consumer that wants to observe the in-flight `Sent` transition live, independent of when `Route` itself returns.
3. Unlike raw transport-level status tracking, `Request` fully owns resolving a send that never reaches an acknowledgement (e.g. a refused connection) - it throws rather than leaving the caller to distinguish that case from a genuine negative acknowledgement, so `PeerService.Send`'s `catch` block (also used the same way by `ClientPeerService`/`ServerRoutingService`, which call `Request` directly with no separate tag-tracking layer) is what turns any such failure into `false`.

**Self-addressing**: If a recipient user name matches the sending user, no network connection is made — the message is delivered in-process (`IPeerService.DeliverLocal`) and `MessageRoutingService` raises `DeliveryStatusChanged` as `Confirmed` immediately.

## Read Confirmation

Beyond MSMT's own delivery status, the engine tracks one more step per recipient: whether the user actually opened the message. This has two sides — the recipient's own **Received → Read** status on their Inbox record, and the sender's **Confirmed → Read** status on their Outbox record's per-user `DeliveryStatus`, driven by a confirmation message sent back over the wire. See [Data.md](Data.md#messageentity) for where these statuses live and `Docs/Components/ViewModels.md` for the alert-message UI that also depends on this flow.

1. **Storage**: `EntryService.StoreIncomingMessage` sets a new Inbox record's `MessageEntity.ReadStatus` to `DestinationStatus.Received`. This status has no equivalent in an Outbox record's `DeliveryStatuses` — from the sender's perspective there is no separate "recipient received it" signal beyond the existing MSMT `Confirmed` status.
2. **Reading**: When a Client-mode user opens an unread Inbox message (`ContentAreaViewModel.BuildMessageViewModel`), it calls `IServiceConnection.MarkMessageRead(messageId)`, which:
   - Calls `EntryService.MarkMessageRead`, transitioning `ReadStatus` from `Received` to `Read` and firing `EntryService.MessageRead` — a no-op (returns `null`) if the record is missing or already `Read`, so reopening an already-read message never re-sends a confirmation.
   - Builds a confirmation message via `IEngineController.CreateMessage()`, giving it its own fresh `MessageId` (an MSMT delivery-status tag, unrelated to the message being confirmed), `FromUser` set to this user's own name, `SetConfirmationMessageId(confirmation, messageId)`, and a single `To` address naming the original sender — needed because `Client`/`Server` roles route purely by the address list, never by the `Send` recipient argument; every other logical field (subject, body) is left at its default, since a confirmation carries only this one piece of information.
   - Sends the confirmation to the original message's `FromUser` via `IPeerService.Send` directly — bypassing `MessageRoutingService.Route`, since there is no address expansion, persistence, or delivery-status seeding to do for a confirmation. For a self-addressed message (`FromUser` equals this user's own name), it instead calls `EntryService.UpdateDeliveryStatus` directly with no network round-trip, mirroring `MessageRoutingService.Route`'s own self-delivery bypass.
3. **Receiving the confirmation**: `PeerService.HandleMessage` checks `IEngineController.GetConfirmationMessageId` on every deserialized message *before* treating it as an ordinary message. If non-empty, it raises `ConfirmationReceived(messageId, confirmingUser)` instead of `MessageDelivered` — a confirmation is never mirrored to interface connections or shown as a new Inbox message.
4. **Advancing the sender's status**: `MessageRoutingService` subscribes to `PeerService.ConfirmationReceived` and re-raises its own `DeliveryStatusChanged(messageId, confirmingUser, DestinationStatus.Read)` — reusing the exact same event, and therefore the exact same `DirectServiceConnection`/`EntryService.UpdateDeliveryStatus`/UI-update pipeline, as an ordinary MSMT delivery-status change (see [Delivery status](#delivery-status) above). `MessageEntity.OverallStatus` only reports `Read` once every recipient's per-user status is `Read`; it stays `Confirmed` while any recipient has confirmed but not yet read.

## Alert Messages

An alert is an ordinary message with `IEngineController.GetIsAlert` set to `true` — nothing about its wire format, routing, or storage differs from a non-alert message. The only difference is client-side: a Client-mode UI that receives an alert message alarms (a red box in the title bar, plus a looping sound) until the user reads it, via the same Read Confirmation flow described above. See `Docs/Components/ViewModels.md` for `AlertViewModel` and the `IEngineController` members that drive this.

## Events

| Event | Raised by | Consumed by |
|-------|-----------|-------------|
| `PeerService.MessageDelivered` | PeerService | DirectServiceConnection |
| `PeerService.ConfirmationReceived` | PeerService | MessageRoutingService |
| `PeerService.DeliveryStatusChanged` | PeerService | MessageRoutingService |
| `MessageRoutingService.DeliveryStatusChanged` | MessageRoutingService | DirectServiceConnection → IServiceConnection consumers |
| `EntryService.MessageRead` | EntryService | AlertViewModel |
