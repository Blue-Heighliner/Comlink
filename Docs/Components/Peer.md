# Peer Networking

The peer layer handles node-to-node message delivery. Every running instance - in both `Client` and `Headless` modes - runs an `IPeerService` and exposes the same `MessageDelivered`/`ConfirmationReceived`/`DeliveryStatusChanged`/`Send`/`DeliverLocal` surface to the rest of the engine (`MessageRoutingService`, `EntryService`) regardless of topology. Which concrete implementation is registered is controlled by `IEngineController.Role` (see [Node Roles](#node-roles) below) - the default, `NodeRole.Peer`, is direct peer-to-peer networking via `PeerService`, described in the rest of this document. None of these services talks to MSMT or a serial port itself: they send and receive through an `IPeerTransport`, which sends over connections that are opened to the node's configured outgoing points or accepted on its listener, over IP or over a MicroGate serial cable (see Transport.md). A node configures only where it connects and listens, never which users are at the other end: the user behind each connection is worked out as the connection forms (see [Identification.md](Identification.md)), and a message for a user goes over whichever connection is currently identified as them. Every instance also runs `InterfaceService`, which hosts a local interface listener that injects into this same message stream (routing a message out exactly as if the local user had composed it), regardless of mode or role - it does not mirror inbound peer messages back out to it; see [Interface.md](Interface.md).

## Node Roles

`NodeRole` (stated with `IEngineBuilder.Role`, see [Configuration.md](Configuration.md#network-topology)) selects one of three networking topologies for a running instance. `EngineExtensions.UseEngine` registers `IPeerService` as a factory that reads `IEngineController.Role` once, when the service is first resolved, so a role set in code and one set by `config.json`'s `NodeRole` (which wins when present) select the same implementation; nothing re-checks the role at runtime. `IConnectionStatusService` resolves to that same instance when it implements the interface, and to `NullConnectionStatusService` otherwise.

| Role | `IPeerService` implementation | Behavior |
|------|-------------------------------|----------|
| `Peer` (default) | `PeerService` | Direct peer-to-peer, as described in the rest of this document. |
| `Client` | `ClientPeerService` | All traffic flows through one long-term connection to a configured server. |
| `Server` | `ServerRoutingService` | Routes between this server's child clients and other servers. |

### Client

A `Client`-role instance has the same inbox/outbox/notes/drafts GUI and application flow as `Peer` - `MessageRoutingService`, `EntryService`, and the ViewModels are unaware of the difference - except for one addition: a single connection-status row pinned to the bottom of the window, tracking the connection described below (see [IConnectionStatusViewModel](ViewModels.md#iconnectionstatusviewmodel--connectionstatusviewmodel) and [Connection Status](#connection-status-client-and-server) below). `ClientPeerService` sends every outbound message over its one long-term connection to the server, the first of `IEngineController.OutgoingPoints`. Connections are bidirectional (see [MsmtIntegration.md](MsmtIntegration.md#bidirectional-connections)), so the server delivers messages back down that same connection and the client never runs a listener of its own:

1. `Start` takes the first of `IEngineController.OutgoingPoints` (logging an error and returning without starting if there is none), creates the transport, and starts a `PeerConnectionMonitor` for that point.
2. `Send(userName, message)` ignores `userName` for addressing purposes - the message is transmitted as-is to the server, and the *server* performs the actual user-to-connection routing (see [Server](#server) below). Because `MessageRoutingService.Route` still calls `IPeerService.Send` once per resolved recipient (e.g. once per member of an addressed group), `ClientPeerService` coalesces concurrent `Send` calls that share the same `IEngineController.GetMessageId(message)` into a single physical transmission, so a group-addressed message is not sent to the server multiple times.
3. Inbound messages are accepted only from the server connection and dispatched through the same confirmation-vs-ordinary classification `PeerService` uses (shared via `PeerMessageDispatcher`), raising `MessageDelivered`/`ConfirmationReceived` identically.
4. `Send` returns `false` immediately if `Start` never successfully created a transport (no outgoing point), or while no connection to the server is up.
5. A background `PeerConnectionMonitor` proactively opens and maintains the connection to the server - see [Connection Status](#connection-status-client-and-server) below.

No per-message MSMT delivery-status tracking is performed across the hop to the server - `DeliveryStatusChanged` is declared (to satisfy `IPeerService`) but never raised.

### Server

A `Server`-role instance has no inbox/outbox/notes/drafts GUI at all - `MainWindow` shows up to two connections tables instead of the normal 3-panel layout and hides the title bar's compose/export/import/print controls entirely, since `ServerRoutingService` is a routing hub between this server's child clients and every other server in the cluster, not a message-composing peer (see [Connection Status](#connection-status-client-and-server) below). It is driven by `IEngineController.Servers`, a map keyed by server user name describing the **whole cluster topology**: every server and its full child-client list, not just the local server's own. The map says who belongs where, not how to reach anyone; where a server listens and connects is configured on the node itself (`PeerPort`, `OutgoingPoints`).

1. **Startup**: `Start` checks that this instance's own name (by `ICurrentUserProvider.UserName`) is in the topology, creates the transport, calls `StartListener` on `IEngineController.PeerPort` (the port child clients and other servers connect to), and starts a background `PeerConnectionMonitor` per outgoing point (typically the other servers this one dials) - see [Connection Status](#connection-status-client-and-server) below. Nothing in the topology is dialed by name: a child or another server may equally be one that connects to this server, and either way it is recognized by the identity its connection is given.
2. **Classifying connections**: `IPeerTransport.Connected` publishes a connection only once it has been identified (see [Identification.md](Identification.md)), and the server matches that identity's name, ignoring case, against its own `ChildClients` and the topology's other server names:
   - A known child client is tracked and its messages routed to the from-child path.
   - A known other server is tracked and its messages routed to the from-server path.
   - Anything else (including this server's own name) is dropped (`PeerConnection.Drop`) and ignored, as is any connection from a user that was closed.
3. **Routing from a child client**: the raw address list (`IEngineController.GetAddresses`, unexpanded - group addressing is not resolved at the server) is checked against (a) this server's other children, each addressed one delivered to over the connection identified as that child, and (b) every other server that owns at least one addressed child, each forwarded the same raw bytes exactly once over the connection identified as that server, regardless of how many of its children are addressed. A recipient with no connection identified as them is skipped with a warning in the log. A recipient whose own assigned security level ranks lower than the message's (`IEngineController.GetSecurityLevel`/`GetUserSecurityLevel`) is dropped from the address list before either check, the same way, with the same warning; unrecognized or no security levels configured skips this entirely.
4. **Routing from another server**: assumed already routed by that server - only delivered to this server's own children that are addressed, over their connections, and never re-forwarded to any other server, so a message can never loop.
5. Messages are relayed as raw bytes (re-deserialized only to read `GetAddresses`/`GetPriority`), keeping the message's priority on every hop and sending to all recipients at once so a slow or unreachable one does not hold up the rest. Nothing from a closed user is relayed. A server does not persist, mirror to interfaces, or otherwise treat routed traffic as its own inbox, and `ConfirmationReceived`/`DeliveryStatusChanged` are never raised for it.

`IPeerService.Send`/`DeliverLocal` are still implemented (an instance in `Server` role composing its own message is treated exactly like a message arriving from a child), for interface completeness, though this is not part of the role's intended usage.

### Connection Status (Client and Server)

`ClientPeerService` and `ServerRoutingService` each also implement `Peer.IConnectionStatusService` directly, subscribing to their peer's `Connected`/`Disconnected` events so the UI can display live status (see [IConnectionStatusViewModel](ViewModels.md#iconnectionstatusviewmodel--connectionstatusviewmodel)). Each reported `Peer.PeerConnectionStatus` carries a `Peer.PeerConnectionKind` (`Server` or `Client`), which the ViewModel layer uses to split rows into two separate tables:

- **Client**: one `Server`-kind entry for its single server connection - `UserName` is the name the server's connection was identified as, once a connection has been made (kept after it drops; empty until the first connection), since a client is configured with the server's address and not its name; `IsConnected` reflects whether the connection to the server is currently live (whether IP or serial), and `LastConnectedAt`/`LastDisconnectedAt` record when it last transitioned. There is never a `Client`-kind entry, since a client has no children of its own.
- **Server**: one `Client`-kind entry per own child client (from `ChildClients` on this server's own entry in `IEngineController.Servers`) plus one `Server`-kind entry per other server in the cluster — the same `IsConnected`/`LastConnectedAt`/`LastDisconnectedAt` shape, tracked independently per remote name (a row reflects every connection identified as that user, whichever end opened it, and goes down only when the last one is lost). `MainWindow` shows the `Server`-kind entries in a "SERVERS" table and the `Client`-kind entries in a "CLIENTS" table below it, each hidden outright while it has no rows (e.g. a standalone server with no peer servers configured shows only the client table, and vice versa).

`NodeRole.Peer` registers `Peer.NullConnectionStatusService` instead (always an empty list) — direct peer-to-peer connections are not tracked as a fixed list of configured links worth a status row.

**Closing and refreshing a connection**: right-clicking a connection row offers Close (Open once closed) and Refresh, backed by `IConnectionStatusService.SetClosed`/`Refresh`. Closing a connection drops every connection identified as that user, stops the monitor of each outgoing point that last reached that user from heartbeating it so nothing re-forms it, makes the transport refuse to connect to those points (a closed serial link also releases its port and stops reconnecting), and rejects any connection that user opens to this node, so a closed user cannot get back in either. Nothing is delivered to or forwarded through a closed user, and the row reads CLOSED in grey. Opening it lets the monitor heartbeat again at once and the connection re-forms. Refreshing drops the user's connections and heartbeats their outgoing points straight away so a fresh one forms; it does nothing to a closed connection. A client's single row acts on its server connection. Closed state is not persisted: it lasts until reopened or the application restarts. The heartbeat loop is steered through the `PeerLinkControl` its `PeerConnectionMonitor.Maintain` call returns.

**When a row counts as up**: an IP connection only counts as up once a heartbeat over it has been acknowledged, not when the TLS connection is merely established. A node that has closed a connection accepts it and drops it again without ever answering, so counting the bare connection flashed the row green on every reconnect attempt. `PeerConnectionMonitor.Maintain` reports each acknowledged heartbeat to its caller for this. A serial link only comes up when the far end answers, so it counts as up immediately. A row goes down only once no live connection to that user remains, since another server can be connected both inbound and outbound at once and losing one direction does not take it offline.

**Keeping a hierarchical connection genuinely live**: Comlink always uses MSMT's session-mode `IMsmtSessionPeer` (see [MsmtIntegration.md](MsmtIntegration.md#session-peer)), so a connection persists across multiple sends instead of closing after each one. Without something to actually send, though, a connection would only ever open the moment a real message needed to be routed - leaving the status table showing every row disconnected for as long as the app happens to be idle. A background `PeerConnectionMonitor`, started per outgoing point (`ClientPeerService`'s one server point; each of `ServerRoutingService`'s and `PeerService`'s points), avoids this by connecting to the point and sending an empty heartbeat payload over the connection immediately on startup and repeatedly thereafter:

- A heartbeat reuses the same Session-mode connection a real message would use (`IPeerTransport.Connect` returns the cached one) - so it establishes and keeps the connection open, observed the normal way through `Connected`/`Disconnected`, rather than opening a separate side channel. Over serial, the first connect is what creates the link to the port, which then reconnects on its own.
- `IMsmtReachabilityChecker.Reach` is deliberately **not** used for this: it opens and immediately closes its own one-shot connection every call, which - because the remote peer's receiver can't distinguish a reachability probe from a real connection until it reads the first message - would make the remote side's own `Connected`/`Disconnected` events flap on every check, corrupting its status table instead of stabilizing it.
- An empty payload is recognized as a heartbeat and never reaches application logic: `PeerMessageDispatcher.Dispatch` returns immediately without deserializing it, and `ServerRoutingService.OnReceived` skips it before any routing decision, so it is never mistaken for a real (if malformed) message, never logged as "received", and never relayed.
- Each heartbeat's outcome governs how soon the next one fires: while the last heartbeat did not succeed - including the very first one, which commonly races the remote peer's own receiver still starting up, since a fresh `ECONNREFUSED` is near-instant rather than a slow timeout - the next retry follows quickly (2 seconds by default) instead of waiting on the full steady-state interval (30 seconds by default).
- A connection dropping is not always the monitor's own next heartbeat noticing - it can just as well be reported by `Disconnected` while the monitor is still asleep for the rest of its steady-state interval, following an earlier successful heartbeat. `ClientPeerService`/`ServerRoutingService.OnDisconnected` wake the monitor for that point directly whenever this leaves it with no live connection, so it retries (and the fast-retry cadence above kicks in) right away instead of waiting out the remainder of that sleep - otherwise a hierarchical connection's status table could sit incorrectly "down" for up to the full steady interval after a drop it did not itself detect.

## Message Format

Peer traffic carries exactly one payload shape and nothing else: an instance of `IEngineController.MessageType`, serialized through `IEngineController.NetworkSerializer` - by default `ProtobufNetworkSerializer`, protobuf-net (binary) - which returns a pool-backed `IMemoryOwner<byte>` so a send does not allocate a fresh buffer every time. `INetworkSerializer.Deserialize` takes no type: the serializer makes its own wire format self-describing, and the default does so with one fixed outer `ProtobufEnvelope` around every payload, carrying the value's assembly-qualified type name and its own protobuf-net encoding as nested bytes. A receiver therefore rebuilds the right concrete type from the bytes alone, and callers that need a specific one (`ServerRoutingService`, `InterfaceService`) check the result against `IEngineController.MessageType` and drop a mismatch. That envelope belongs to the serialization layer, not to Comlink's own protocol: there is still no Comlink-level envelope or command discriminator. MSMT-level delivery (did the bytes arrive) is tracked entirely through MSMT's own delivery status (see [Delivery status](#delivery-status) below); the only application-level reply that exists is the user-read confirmation described in [Read Confirmation](#read-confirmation), and it is itself just an ordinary instance of `IEngineController.MessageType` with one field set — there is still no separate envelope or command discriminator.

The concrete message type is **injectable**, not hardwired. The message mapping a host states with `IEngineBuilder.Message<TMessage>` (see [Configuration.md](Configuration.md#message-format)) provides the type itself and maps the engine's logical fields onto that type's real fields: message id, sender, subject, body, addresses, sent time, confirmation id, alert flag, priority and tag, each a getter and a setter. Internally these become the `IEngineController` members `MessageType`, `CreateMessage()`, and a `Get`/`Set` pair per field, all `object`-typed.

Every layer that carries or stores a message (`PeerService`, `InterfaceService`, `MessageRoutingService`, `EntryService`, and `MessageEntity.Message` in the database, see [Data.md](Data.md)) works purely in terms of `object`, calling into `IEngineController` for every logical field it needs. The engine has no message type of its own and never assumes a particular field name or wire layout beyond what a host's own `[ProtoContract]`/`[ProtoMember]` attributes declare on its DTO.

The mapping is **required**, with no default: the engine has no message DTO of its own, so a host that never calls `Message<TMessage>`, or leaves a field unmapped, fails at `Engine.Start` with an error naming what is missing.

The `Sample` project's `SampleEngineConfiguration` maps every logical field onto a `SampleMessage` DTO (`Id`/`Sender`/`Title`/`Text`/`Recipients` fields) to demonstrate a working configuration; the mapping, not any particular field name, is what the engine actually depends on. See `Sample/src/SampleEngineConfiguration.cs`.

## Connection Lifecycle

`PeerService` wraps one `IPeerTransport`, created via `IPeerTransportFactory` once a current user is registered. Over IP that is an MSMT peer configured with `IEngineController.ConnectionOptions` (see [MsmtIntegration.md](MsmtIntegration.md)); over serial it is one MicroGate link per outgoing serial point. The transport transparently owns both directions - there is no separate inbound/outbound class - and outermost it identifies every connection (see [Identification.md](Identification.md)):

### Inbound
1. `PeerService.Start` calls `IPeerTransport.StartListener` on `PeerPort` (all interfaces, IP only) and starts a monitor for every outgoing point, then blocks until cancelled. If no identity certificate is available the IP half is left out, with a warning, and serial still works.
2. `IPeerTransport.Received` publishes for every message received on any identified connection (IP or serial), with the payload copied out; the subscriber never rejects, so the transport acknowledges once every subscriber has run, since transport-level acceptance and application-level message validity are not distinguished here.
3. The copied bytes are deserialized as an instance of `IEngineController.MessageType` and `MessageDelivered` fires with that `object`.
4. Deserialization failures are caught and simply dropped; the transport itself handles connection-level errors and reconnects (the monitor's next heartbeat re-dials an IP point, a serial link reconnects on its own).

### Outbound
- `PeerService.Send(userName, message)` takes `message` as `object` (an instance of `IEngineController.MessageType`), finds the newest connection identified as `userName`, reads `IEngineController.GetMessageId(message)` for the delivery-status tag, then calls `IPeerTransport.Request(connection, data, new PeerSendOptions { Priority = IEngineController.GetPriority(message), Transmitted = ... })`. A user nobody is identified as fails at once with `DestinationStatus.Failed`; nothing is dialed on demand, since a node only connects to its outgoing points. `IEngineController.GetPriority(message)` is passed verbatim as the send priority - over IP higher values are sent first by MSMT, while a serial link sends in call order and ignores it - independent of the same value also being embedded inside the serialized message content itself.
- Over IP, the MSMT peer keeps the connection to each outgoing point cached and reuses it; a connection the remote node opened is used as it stands, since session connections carry requests both ways. Over serial, one persistent link per port and address is kept up by the transport.
- `IPeerTransport.Request` asynchronously awaits the remote node's acknowledgement itself - either returning whether the remote node accepted the message or throwing if the send never got that far (e.g. the connection is gone, or the serial link is down) - so `PeerService.Send` does not return until the message has been fully delivered (see [Delivery status](#delivery-status)): an unidentified user, a thrown exception, or a negatively-acknowledged send all cause it to return `false`.

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

## Connection & Message Hooks

Every `IPeerService` implementation also raises `UserConnected`/`UserDisconnected` - once when a user goes from
unreachable to reachable over at least one live connection, and once when the last such connection is lost - and
exposes `GetConnectedUsers()`/`IsUserConnected(userName)`, a live view of who that currently includes. What counts
as "a live connection" matches each role's own connection tracking described above: `PeerService` tracks arbitrary
identified connections via `IUserConnections`; `ClientPeerService` has exactly one possible entry, its server;
`ServerRoutingService` tracks every configured child client and cluster server by name. `SendPacket(userName, packet, cancellation)`
sends a raw, already-built packet (serialized via `IEngineController.PacketSerializer`) directly to one user's
connection, the same way `Send` does for a message but bypassing the normal packetization/reassembly a full
message goes through, and carrying no delivery-status tracking of its own.

`EngineHooksService` (started by `EngineHost` alongside the peer and interface listeners, in both Client and
Headless mode, a no-op if the host configured no hooks at all) subscribes to `UserConnected`/`UserDisconnected`/
`MessageDelivered`, and runs the host's own hooks (`IEngineBuilder.OnUserConnected`/`OnUserDisconnected`/`OnMessageReceived`,
see [Configuration.md](Configuration.md#connection--message-hooks)) for each - every hook for one firing handed the
same freshly-built `IUserConnectionHookContext`/`IMessageReceivedHookContext`, so `SendMessage`/`SendPacket` route
through `IMessageRoutingService.RouteMessage`/`IPeerService.SendPacket` respectively on that context's behalf. A
failing hook is logged and never stops the rest, of that event or a later one, from running.

## Auto Forward

`AutoForwardService` (started by `EngineHost` alongside the peer and interface listeners, in both Client and
Headless mode, a no-op if the host configured no auto forward controllers at all - see
[Configuration.md](Configuration.md#auto-forward-controllers)) subscribes to `MessageDelivered` and, for each
delivered message, checks every `IEngineController.AutoForwardControllers` entry this instance's own installed
user is named in: a controller whose `Filter` accepts the message is forwarded to every user currently on that
controller's locally-saved target list (`IAutoForwardTargetsRepository`, see `Docs/Components/Data.md`), unless
that list is empty, in which case nothing happens. A forwarded message is a freshly built instance of the
configured message type carrying the original's subject, body, and other content fields unchanged, addressed to
the target list and routed via `IMessageRoutingService.RouteMessage` from this instance's own installed user - the
same routing path a hook-originated send uses - so it becomes an ordinary Outbox record and a new message ID, not
a re-send of the original. The current user's own name is always excluded from the forwarded address list, even
if present in the saved target list, since that message would otherwise be re-delivered right back to this same
instance, matching the same controller's filter again and forwarding forever. A controller with no access, or an
inaccessible one, or a `Filter` that throws, is skipped without affecting any other configured controller - a
failure is logged the same way a failing hook is.

## Events

| Event | Raised by | Consumed by |
|-------|-----------|-------------|
| `PeerService.MessageDelivered` | PeerService | DirectServiceConnection, EngineHooksService, AutoForwardService |
| `PeerService.ConfirmationReceived` | PeerService | MessageRoutingService |
| `PeerService.DeliveryStatusChanged` | PeerService | MessageRoutingService |
| `PeerService.UserConnected` / `UserDisconnected` | PeerService, ClientPeerService, ServerRoutingService | EngineHooksService |
| `MessageRoutingService.DeliveryStatusChanged` | MessageRoutingService | DirectServiceConnection → IServiceConnection consumers |
| `EntryService.MessageRead` | EntryService | AlertViewModel |
