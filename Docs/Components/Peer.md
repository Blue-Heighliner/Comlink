# Peer Networking

The peer layer handles node-to-node frame delivery: a frame is anything the host's frame type carries, and a message is the kind of frame (recognized by the message handler) the user sees and that is stored; everything else (heartbeats, if the host states them, receipts, retrieval requests, and frames exchanged by the host's own processors) is routed and delivered the same way but never shown or stored. Every running instance - in both `Client` and `Headless` modes - runs an `IPeerService` and exposes the same `FrameDelivered`/`ReadReceiptReceived`/`DeliveryStatusChanged`/`Send`/`DeliverLocal` surface to the rest of the engine (`MessageRoutingService`, `EntryService`) regardless of topology. Which concrete implementation is registered is controlled by `IEngineController.Role` (see [Node Roles](#user-roles) below) - the default, `UserRole.Peer`, is direct peer-to-peer networking via `PeerService`, described in the rest of this document. None of these services talks to MSMT or a serial port itself: they send and receive through an `IPeerTransport`, which sends over connections that are opened to the node's configured outgoing points or accepted on its listener, over IP or over a MicroGate serial cable (see Transport.md). A node configures only where it connects and listens, never which users are at the other end: the user behind each connection is worked out as the connection forms (see [Identification.md](Identification.md)), and a message for a user goes over whichever connection is currently identified as them. Every instance also runs `InterfaceService`, which hosts a local interface listener that injects into this same message stream (routing a message out exactly as if the local user had composed it), regardless of mode or role - it does not mirror inbound peer messages back out to it; see [Interface.md](Interface.md).

## User Roles

`UserRole` (the `Role` of the current user's entry in the network configuration file, see [Configuration.md](Configuration.md#user-info)) selects one of three networking topologies for a running instance. `EngineExtensions.UseEngine` registers `RolePeerService` as the one `IPeerService` and `IConnectionStatusService`; it reads `IEngineController.Role` when networking starts, after the user is installed or named by `--user`, and creates the matching implementation (`ClientPeerService`, `ServerRoutingService` or `RelayPeerService`; a user with no role is a client), forwarding its events. Until then no user is connected and there are no connection statuses; nothing re-checks the role at runtime.

| Role | `IPeerService` implementation | Behavior |
|------|-------------------------------|----------|
| `Client` (default) | `ClientPeerService` | All traffic flows through one long-term connection to a configured server. |
| `Server` | `ServerRoutingService` | Routes between this server's child clients and other servers. |
| `Relay` | `RelayPeerService` | A direct network path between its child clients and one server. |

### Client

A `Client`-role instance has the inbox/outbox/notes/drafts GUI and application flow - `MessageRoutingService`, `EntryService`, and the ViewModels are unaware of the topology - with one addition: a single connection-status row pinned to the bottom of the window, tracking the connection described below (see [IConnectionStatusViewModel](ViewModels.md#iconnectionstatusviewmodel--connectionstatusviewmodel) and [Connection Status](#connection-status-client-and-server) below). `ClientPeerService` sends every outbound message over its one long-term connection to the server, its parent (`IEngineController.ParentPoints`, built from the parent's `IpHost` and `Msmt.Port`, or the node's `Hdlc` ports). Connections are bidirectional (see [MsmtIntegration.md](MsmtIntegration.md#bidirectional-connections)), so the server delivers messages back down that same connection and the client never runs a listener of its own:

1. `Start` takes `IEngineController.ParentPoints` (logging an error and returning without starting if the client has no parent), creates the transport, and starts a `PeerConnectionMonitor` for that point. If the parent's link is forced to `MsmtListen` there is no point to dial: the client starts a listener on `PeerPort` instead, and the connection its parent opens, identified as the parent, is its server connection.
2. `Send(userName, message)` ignores `userName` for addressing purposes - the message is transmitted as-is to the server, and the *server* performs the actual user-to-connection routing (see [Server](#server) below). Because `MessageRoutingService.Route` still calls `IPeerService.Send` once per resolved recipient (e.g. once per member of an addressed group), `ClientPeerService` coalesces concurrent `Send` calls for the same frame instance into a single physical transmission, so a group-addressed message is not sent to the server multiple times.
3. Inbound messages are accepted only from the server connection and dispatched through the receipt-vs-ordinary classification in `PeerFrameDispatcher`, raising `FrameDelivered`/`ReadReceiptReceived`/`ReceiveReceiptReceived` identically.
4. `Send` returns `false` immediately if `Start` never successfully created a transport (no parent), or while no connection to the server is up.
5. A background `PeerConnectionMonitor` proactively opens and maintains the connection to the server - see [Connection Status](#connection-status-client-and-server) below.

No per-message MSMT delivery-status tracking is performed across the hop to the server - `DeliveryStatusChanged` is declared (to satisfy `IPeerService`) but never raised.

### Server

A `Server`-role instance has no inbox/outbox/notes/drafts GUI at all - `MainWindow` shows up to two connections tables instead of the normal 3-panel layout and hides the title bar's compose/export/import/print controls entirely, since `ServerRoutingService` is a routing hub between this server's child clients and every other server in the cluster, not a message-composing peer (see [Connection Status](#connection-status-client-and-server) below). It is driven by `IEngineController.Servers`, a map keyed by server user name describing the **whole cluster topology**: every server and its full child-client list, not just the local server's own. The map says who belongs where, not how to reach anyone; where a server listens and connects is configured on the node itself (`PeerPort`, `OutgoingPoints`).

1. **Startup**: `Start` checks that this instance's own name (by `ICurrentUserProvider.UserName`) is in the topology, creates the transport, calls `StartListener` on `IEngineController.PeerPort` (the port children and other servers connect to), and starts a background `PeerConnectionMonitor` per outgoing point (its parent and any children whose links make it dial them) - see [Connection Status](#connection-status-client-and-server) below. Nothing in the topology is dialed by name: a child or another server may equally be one that connects to this server, and either way it is recognized by the identity its connection is given.
2. **Classifying connections**: `IPeerTransport.Connected` publishes a connection only once it has been identified (see [Identification.md](Identification.md)), and the server matches that identity's name, ignoring case, against its own `Children` and the topology's other server names:
   - A known child client (or a relay, which is listed among the children) is tracked and its messages routed to the from-child path.
   - A known other server is tracked and its messages routed to the from-server path.
   - Anything else (including this server's own name) is dropped (`PeerConnection.Drop`) and ignored, as is any connection from a user that was closed.
3. **Routing from a child client**: the frame's route (`IEngineController.Route`: a message's addresses, a receipt's or retrieval request's destination; unexpanded - group addressing is not resolved at the server) are checked against (a) this server's other children, each addressed one delivered to over the connection identified as that child, and (b) every other server that owns at least one addressed child, each forwarded the same raw bytes exactly once over the connection identified as that server, regardless of how many of its children are addressed. A recipient with no connection identified as them is skipped with a warning in the log. A recipient whose own assigned security level ranks lower than the message's (`IEngineController.GetSecurityLevel`/`GetUserSecurityLevel`) is dropped from the address list before either check, the same way, with the same warning; unrecognized or no security levels configured skips this entirely.
4. **Routing from another server**: assumed already routed by that server - only delivered to this server's own children that are addressed, over their connections, and never re-forwarded to any other server, so a message can never loop.
5. Messages are relayed as raw bytes (re-deserialized only to read `Route`/`GetPriority`), keeping the message's priority on every hop and sending to all recipients at once so a slow or unreachable one does not hold up the rest. Nothing from a closed user is relayed. A server does not persist, mirror to interfaces, or otherwise treat routed traffic as its own inbox, and `ReadReceiptReceived`/`DeliveryStatusChanged` are never raised for it.

A server composes no messages, it only transports them: `IPeerService.Send` always returns `false` (as it does for a relay), so anything that tries to send from a server, such as a network processor or an interface message, fails.

**Relays under a server**: a child of a server may be a relay (a user whose `Role` is `Relay`). The server then reaches the relay's own `ChildClients` through it: `IEngineController.Servers` records, per server, which children are relays and the clients behind each (`ServerUserConfig.Relays`, taken from the relay's own entry). A message addressed to a client behind a relay is sent once, as the raw bytes, to the relay (not to the client), and a server whose children include relays is forwarded a message for a client behind them exactly like one for a direct child. A message a relay forwards up is routed like any from-child message, and a retrieval request arriving through a relay is answered to its original sender (`GetFromUser`) rather than the relay.

### Relay

A `Relay`-role instance has the same GUI as a `Server`: no inbox, outbox, notes or drafts, just the connections tables and the activity log (`IsServerMode` covers both roles). `RelayPeerService` is a direct network path between the clients behind it and the one server it is linked to, and nothing more. It listens on `PeerPort` for connections from the users named in its own `Children` (dialing any whose link says `MsmtConnect` or `Hdlc`), and keeps one connection open to its parent, the server, maintained and monitored the same way a client maintains its server connection (or accepted from it when that link is `MsmtListen`). The server lists the relay among its own `Children`.

1. **Classifying connections**: the connection to the parent (the outgoing one to its point, or an inbound one identified as the parent) is the server connection. Any other connection is accepted only when its identity is one of this relay's `Children` (and not closed); anything else is dropped.
2. **From a child**: every frame that has a route is forwarded to the server, whoever it is for. A relay never turns traffic around between its own children, so the server sees and routes everything, including traffic between two clients of the same relay.
3. **From the server**: a frame is forwarded to each of this relay's children that its route names, concurrently.
4. **Untouched**: what is forwarded is the very payload that arrived, with its priority. A frame is only deserialized to read its route and priority and to recognize heartbeats, which are connection upkeep and are never forwarded. The initial packet and frame exchange that introduces the two ends of a connection (see [Identification.md](Identification.md#initial-packet-and-frame-exchange)) is point-to-point: the transport consumes it before the relay sees any traffic, and a frame that has no route is never forwarded either, so nothing of the exchange can cross the relay. A relay does not modify, store, receipt, deliver locally or mirror to interfaces anything it forwards, applies no security level filtering (the server did), and raises no delivery events. `Send` and `SendPacket` return `false` and `DeliverLocal` does nothing, since a relay composes nothing.
5. **Status**: one `Client`-kind row per child and one `Server`-kind row for the server connection, with the same close and refresh actions as the other roles.

### Connection Status (Client and Server)

`ClientPeerService` and `ServerRoutingService` each also implement `IConnectionStatusService` directly, subscribing to their peer's `Connected`/`Disconnected` events so the UI can display live status (see [IConnectionStatusViewModel](ViewModels.md#iconnectionstatusviewmodel--connectionstatusviewmodel)). Each reported `PeerConnectionStatus` carries a `PeerConnectionKind` (`Server` or `Client`), which the ViewModel layer uses to split rows into two separate tables:

- **Client**: one `Server`-kind entry for its single server connection - `UserName` is the name the server's connection was identified as, once a connection has been made (kept after it drops; empty until the first connection), since a client is configured with the server's address and not its name; `IsConnected` reflects whether the connection to the server is currently live (whether IP or serial), and `LastConnectedAt`/`LastDisconnectedAt` record when it last transitioned. There is never a `Client`-kind entry, since a client has no children of its own.
- **Server**: one `Client`-kind entry per own child (from `Children` on this server's own entry in `IEngineController.Servers`) plus one `Server`-kind entry per other server in the cluster — the same `IsConnected`/`LastConnectedAt`/`LastDisconnectedAt` shape, tracked independently per remote name (a row reflects every connection identified as that user, whichever end opened it, and goes down only when the last one is lost). `MainWindow` shows the `Server`-kind entries in a "SERVERS" table and the `Client`-kind entries in a "CLIENTS" table below it, each hidden outright while it has no rows (e.g. a standalone server with no peer servers configured shows only the client table, and vice versa).

**Closing and refreshing a connection**: right-clicking a connection row offers Close (Open once closed) and Refresh, backed by `IConnectionStatusService.SetClosed`/`Refresh`. Closing a connection drops every connection identified as that user, stops the monitor of each outgoing point that last reached that user from heartbeating it so nothing re-forms it, makes the transport refuse to connect to those points (a closed serial link also releases its port and stops reconnecting), and rejects any connection that user opens to this node, so a closed user cannot get back in either. Nothing is delivered to or forwarded through a closed user, and the row reads CLOSED in grey. Opening it lets the monitor heartbeat again at once and the connection re-forms. Refreshing drops the user's connections and heartbeats their outgoing points straight away so a fresh one forms; it does nothing to a closed connection. A client's single row acts on its server connection. Closed state is not persisted: it lasts until reopened or the application restarts. The heartbeat loop is steered through the `PeerLinkControl` its `PeerConnectionMonitor.Maintain` call returns.

**When a row counts as up**: an IP connection only counts as up once a heartbeat over it has been acknowledged, not when the TLS connection is merely established. A node that has closed a connection accepts it and drops it again without ever answering, so counting the bare connection flashed the row green on every reconnect attempt. `PeerConnectionMonitor.Maintain` reports each acknowledged heartbeat to its caller for this. A serial link only comes up when the far end answers, so it counts as up immediately. A row goes down only once no live connection to that user remains, since another server can be connected both inbound and outbound at once and losing one direction does not take it offline.

**Everything on the wire is a frame or a packet**: a connection carries serialized instances of the configured frame type (through the frame serializer) and, when packets are configured, of the configured packet type (through the packet serializer), and nothing else. That covers the initial packet and frame exchange that introduces a node (see [Identification.md](Identification.md#initial-packet-and-frame-exchange)) and the heartbeat below, neither of which has a frame, tag or special payload of its own. **This is a hard rule**, so that systems that do not run this software can interoperate: no connection, whether a TCP stream or HDLC information frames, ever carries anything of this software's own besides those serialized frames or packets. The only other bytes are the standardized MSMT protocol's own framing and acknowledgements, which sit beneath all of this and are not something the engine configures; the serial link adds no framing, fragmentation or acknowledgement at all (see [Transport.md](Transport.md)).

**Keeping a hierarchical connection genuinely live**: Comlink always uses MSMT's session-mode `IMsmtSessionPeer` (see [MsmtIntegration.md](MsmtIntegration.md#session-peer)), so a connection persists across multiple sends instead of closing after each one. Without something to actually send, though, a connection would only ever open the moment a real message needed to be routed - leaving the status table showing every row disconnected for as long as the app happens to be idle. A background `PeerConnectionMonitor`, started per outgoing point (`ClientPeerService`'s one server point; each of `ServerRoutingService`'s and `PeerService`'s points), avoids this by connecting to the point and sending an empty heartbeat payload over the connection immediately on startup and repeatedly thereafter:

- A heartbeat reuses the same Session-mode connection a real message would use (`IPeerTransport.Connect` returns the cached one) - so it establishes and keeps the connection open, observed the normal way through `Connected`/`Disconnected`, rather than opening a separate side channel. Heartbeats only ever cross MSMT connections, never HDLC: over serial the monitor sends nothing, the first connect is what creates the link to the port, which then reconnects on its own, and the point counts as up while HDLC reports the link connected.
- `IMsmtReachabilityChecker.Reach` is deliberately **not** used for this: it opens and immediately closes its own one-shot connection every call, which - because the remote peer's receiver can't distinguish a reachability probe from a real connection until it reads the first message - would make the remote side's own `Connected`/`Disconnected` events flap on every check, corrupting its status table instead of stabilizing it.
- Heartbeats are optional and explicit: the host states a heartbeat handler (`Heartbeat<THandler>` on the frame configuration), and without one no heartbeat is sent and the monitor counts an IP connection as up once it is established, like a serial one. The host may instead state the heartbeat on the packet configuration (`Heartbeat<THandler>` on `Packets`), which takes precedence while packetization is on: the monitor then serializes a heartbeat packet with the packet serializer and sends it flagged `PeerSendOptions.IsPacket`, which `PacketizingPeerTransport` passes down as it is, and `PacketAssembler` discards it on arrival (it is neither a frame packet nor an error). Otherwise an IP heartbeat is a frame created by that handler, serialized with the frame serializer like any frame (and packetized like one when packets are configured), sent at the priority the handler names: nothing else ever crosses a connection. `IEngineController.IsHeartbeat`, which asks the handler, recognizes it. It never reaches application logic: `PeerFrameDispatcher.Dispatch` acknowledges it without delivering it, and `ServerRoutingService` skips it before any routing or storage decision, so it is never logged as "received", stored or relayed.
- Each heartbeat's outcome governs how soon the next one fires: while the last heartbeat did not succeed - including the very first one, which commonly races the remote peer's own receiver still starting up, since a fresh `ECONNREFUSED` is near-instant rather than a slow timeout - the next retry follows quickly instead of waiting on the full steady-state interval. Both come from the heartbeat handler (`IHeartbeatHandler.RetryInterval` and `Interval`), the packet one first, and are 2 and 30 seconds when no handler is stated.
- A connection dropping is not always the monitor's own next heartbeat noticing - it can just as well be reported by `Disconnected` while the monitor is still asleep for the rest of its steady-state interval, following an earlier successful heartbeat. `ClientPeerService`/`ServerRoutingService.OnDisconnected` wake the monitor for that point directly whenever this leaves it with no live connection, so it retries (and the fast-retry cadence above kicks in) right away instead of waiting out the remainder of that sleep - otherwise a hierarchical connection's status table could sit incorrectly "down" for up to the full steady interval after a drop it did not itself detect.

## Message Format

Peer traffic carries exactly one payload shape and nothing else: an instance of `IEngineController.FrameType`, serialized through `IEngineController.FrameSerializer` - by default `ProtobufSerializer`, protobuf-net (binary) - which returns a pool-backed `IMemoryOwner<byte>` so a send does not allocate a fresh buffer every time. `INetworkSerializer.Deserialize` takes no type: the serializer makes its own wire format self-describing, and the default does so with one fixed outer `ProtobufEnvelope` around every payload, carrying the value's assembly-qualified type name and its own protobuf-net encoding as nested bytes. A receiver therefore rebuilds the right concrete type from the bytes alone, and callers that need a specific one (`ServerRoutingService`, `InterfaceService`) check the result against `IEngineController.FrameType` and drop a mismatch. That envelope belongs to the serialization layer, not to Comlink's own protocol: there is still no Comlink-level envelope or command discriminator. MSMT-level delivery (did the bytes arrive) is tracked entirely through MSMT's own delivery status (see [Delivery status](#delivery-status) below); the only application-level reply that exists is the user-read confirmation described in [Read Confirmation](#receipts), and it is itself just an ordinary instance of `IEngineController.FrameType` with one field set — there is still no separate envelope or command discriminator.

The concrete frame type is **injectable**, not hardwired. The frame mapping a host states with `Frames` (see [Configuration.md](Configuration.md#frame-format)) provides the type itself, maps the fields every frame has (id, sender, addresses) with a getter and a setter, and states a handler per kind of frame (message, retrieval request, read receipt, receive receipt) that creates, recognizes and reads that kind. Internally these become the `IEngineController` members `FrameType`, `CreateFrame()`, a `Get`/`Set` pair per common field, and per kind an `Is...` check, a `Create...` method and `Get...` readers, all `object`-typed.

Every layer that carries or stores a message (`ClientPeerService`, `InterfaceService`, `MessageRoutingService`, `EntryService`, and `MessageEntity.Message` in the database, see [Data.md](Data.md)) works purely in terms of `object`, calling into `IEngineController` for every logical field it needs. The engine has no frame type of its own and never assumes a particular field name or wire layout beyond what a host's own `[ProtoContract]`/`[ProtoMember]` attributes declare on its DTO.

The mapping is **required**, with no default: the engine has no frame DTO of its own, so a host that never calls `Frames`, or leaves a field or handler unstated, fails at `Engine.Start` with an error naming what is missing.

The `Sample` project's `EngineConfiguration` maps the common fields onto a `Frame` DTO (`Id`/`Sender`/`Recipients` fields) and states `MessageHandler`, `RetrievalHandler`, `ReadReceiptHandler` and `ReceiveReceiptHandler` (which read `Title`/`Text` and the flags) to demonstrate a working configuration; the mapping and handlers, not any particular field name, are what the engine actually depends on. See `Sample/src/EngineConfiguration.cs`.

## Connection Lifecycle

Each role's service wraps one `IPeerTransport`, created via `IPeerTransportFactory` once a current user is registered. Over IP that is an MSMT peer configured with `IEngineController.ConnectionOptions` (see [MsmtIntegration.md](MsmtIntegration.md)); over serial it is one MicroGate link per outgoing serial point. The transport transparently owns both directions - there is no separate inbound/outbound class - and outermost it identifies every connection (see [Identification.md](Identification.md)):

### Inbound
1. `Start` calls `IPeerTransport.StartListener` on `PeerPort` (all interfaces, IP only) where the role listens (a server for its children and other servers; a client or relay only when a link is in listen mode) and starts a monitor for every outgoing point, then blocks until cancelled. If no identity certificate is available the IP half is left out, with a warning, and serial still works.
2. `IPeerTransport.Received` publishes for every message received on any identified connection (IP or serial), with the payload copied out; the subscriber never rejects, so over MSMT the transport acknowledges once every subscriber has run (a serial link has no acknowledgement to send), since transport-level acceptance and application-level message validity are not distinguished here.
3. The copied bytes are deserialized as an instance of `IEngineController.FrameType` and `FrameDelivered` fires with that `object`.
4. Deserialization failures are caught and simply dropped; the transport itself handles connection-level errors and reconnects (the monitor's next heartbeat re-dials an IP point, a serial link reconnects on its own).

### Outbound
- `Send(userName, message)` takes `message` as `object` (an instance of `IEngineController.FrameType`) and calls `IPeerTransport.Request(connection, data, new PeerSendOptions { Priority = IEngineController.GetPriority(message), ... })` over the role's connection (a client's one connection to its server; a server's connection to the child or server named). A user nobody is identified as fails at once; nothing is dialed on demand, since a node only connects to its outgoing points. `IEngineController.GetPriority(message)` is passed as the send priority on every link, independent of the same value also being embedded inside the serialized message content itself.
- Over IP, the MSMT peer keeps the connection to each outgoing point cached and reuses it; a connection the remote node opened is used as it stands, since session connections carry requests both ways. Over serial, one persistent link per port and address is kept up by the transport.
- `IPeerTransport.Request` asynchronously awaits the transport's own acknowledgement itself (MSMT's from the remote node, or over a serial link the frame having been sent) - either returning whether the message was accepted or throwing if the send never got that far (e.g. the connection is gone, or the serial link is down) - so `Send` does not return until the message has been fully delivered to the next hop: an unidentified user, a thrown exception, or a negatively-acknowledged send all cause it to return `false`. `Request` fully owns resolving a send that never reaches an acknowledgement (e.g. a refused connection): it throws rather than leaving the caller to distinguish that case from a negative acknowledgement, and each service's `catch` turns any such failure into `false`.

## Delivery status

An outgoing message's per-recipient status moves **Sent → Received → Read** (or ends at `Failed`). The first step comes from the send itself, the other two from receipt frames the recipient's node sends back.

1. `MessageRoutingService.Route` sends once per recipient through `IPeerService.Send`, and a `true` result means "accepted by the next hop" (a client's server, for a client), so `UserDeliveryResult.Success` and `StoreSentMessage` seed that user as `Sent`, or `Failed` for `false`. A role may also raise `IPeerService.DeliveryStatusChanged(messageId, userName, DestinationStatus)`; the client and server do not track delivery across the hop, so for them it is declared and never raised.

   | Event | `DestinationStatus` |
   |---|---|
   | `Send` returns `true` (stored with the sent record) | `Sent` |
   | `Send` returns `false`, or throws | `Failed` |
   | Receive receipt frame arrives | `Received` |
   | Read receipt frame arrives | `Read` |
2. Receipt frames can arrive before the Outbox record is stored, so `EntryService.UpdateDeliveryStatus` keeps such a status and applies it when the record is stored. A status only moves forward (`Sending < Sent < Failed < Received < Read`), so a late `Sent` never overwrites `Received`, while a receipt still overrides a reported failure.

**Self-addressing**: a message addressed to the sending user is sent like any other, so a client's goes up to its server, which routes it back to the client as one of its children; its status advances through the receipts that return.

**Invalid messages**: a message always has an identifier before it is sent (see [Configuration.md](Configuration.md#message-identifiers)) and only carries priorities and security levels the configuration states (see [Message Composition](Configuration.md#message-composition)), so one received without an identifier, or with a priority or security level that is not stated, is an error and is dropped, with the reason logged: a client does not deliver it, a server neither stores nor routes it, a relay does not forward it, and an external system's message without one is not processed.

## Receipts

A receipt is a non-message frame (see the `ReceiveReceipt` and `ReadReceipt` handlers in [Configuration.md](Configuration.md#frame-format)) that is created only for a message frame and carries only the identifier of the message it is for, which is also what identifies the receipt (it has no identifier of its own), plus the sender, and a destination, the original sender, which the engine gives the handler (`ReceiptCreateContext.To`) and the handler reads back (`GetDestination`); `Client`/`Server` roles route purely by the route. Receipts are sent directly through `IPeerService.Send`, bypassing `MessageRoutingService.Route`, since there is no address expansion, persistence or status seeding to do. They are never stored, shown, forwarded or mirrored to interface connections. See [Data.md](Data.md#messageentity) for where statuses live and `Docs/Components/ViewModels.md` for the alert UI that depends on the read flow.

1. **Receive receipt**: when `DirectServiceConnection` sees a delivered message (one the message handler recognizes) from another user, it immediately sends a receive receipt to the sender, regardless of whether anyone has opened it. A message the user sent to themselves is receipted like any other, through the server.
2. **Storage**: `EntryService.StoreIncomingMessage` sets a new Inbox record's `MessageEntity.ReadStatus` to `Received`.
3. **Reading**: when a Client-mode user opens an unread Inbox message (`ContentAreaViewModel.BuildMessageViewModel`), it calls `IServiceConnection.MarkMessageRead(messageId)`, which calls `EntryService.MarkMessageRead` (`Received` to `Read`, firing `EntryService.MessageRead`; a no-op returning `null` if the record is missing or already `Read`, so reopening never re-sends) and sends a read receipt to the original sender.
4. **Receiving a receipt**: `PeerFrameDispatcher` asks the read and receive receipt handlers' `IsValid` about every deserialized frame before treating it as ordinary, raising `ReadReceiptReceived` or `ReceiveReceiptReceived` (`messageId`, sending user) instead of `FrameDelivered`.
5. **Advancing the sender's status**: `MessageRoutingService` subscribes to both events and re-raises `DeliveryStatusChanged` as `Received` or `Read`, reusing the same `DirectServiceConnection`/`EntryService.UpdateDeliveryStatus`/UI pipeline as any status change. `MessageEntity.OverallStatus` reports the least advanced status across recipients, with `Failed` taking priority.

## Alert Messages

Whether a message is an alert is not chosen by the user: the host's message handler decides it from the message's other fields (`IMessageHandler.IsAlert(frame)`, for example its tag), so a host can make, say, every message with a certain tag an alert, and there is no alert flag to store or send. An alert is an ordinary message for which `IEngineController.GetIsAlert` is `true` — nothing about its wire format, routing, or storage differs from a non-alert message. The only difference is client-side: a Client-mode UI that receives an alert message alarms (a red box in the title bar, plus a looping sound) until the user reads it, via the read receipt flow described above. See `Docs/Components/ViewModels.md` for `AlertViewModel` and the `IEngineController` members that drive this.

## Network Processor

Every `IPeerService` implementation also raises `UserConnected`/`UserDisconnected` - once when a user goes from
unreachable to reachable over at least one live connection, and once when the last such connection is lost - and
exposes `GetConnectedUsers()`/`IsUserConnected(userName)`, a live view of who that currently includes. What counts
as "a live connection" matches each role's own connection tracking described above: `ClientPeerService` has exactly one possible entry, its server;
`ServerRoutingService` tracks every configured child client and cluster server by name. `SendPacket(userName, packet, cancellation)`
sends a raw, already-built packet (serialized via `IEngineController.PacketSerializer`) directly to one user's
connection, the same way `Send` does for a message but bypassing the normal packetization/reassembly a full
message goes through, and carrying no delivery-status tracking of its own.

`EngineHooksService` (started by `EngineHost` alongside the peer and interface listeners, in both Client and
Headless mode, a no-op if the host configured no network processor) subscribes to `UserConnected`/`UserDisconnected`/
`FrameDelivered`, and runs the host's own network processor (`IFrameBuilder<TFrame>.Processor`, `OnConnected`/`OnDisconnected`/`OnReceived`,
see [Configuration.md](Configuration.md#network-processor)) for each - every event handed its own
freshly-built internal context, which the processor sees typed as `INetworkConnectedContext<TFrame>`/`INetworkDisconnectedContext<TFrame>`/`INetworkReceivedContext<TFrame>`, so `Send` routes
through `IMessageRoutingService.RouteFrame` on that context's behalf. A
failing processor method is logged and never stops a later event from being handled.

## Auto Forward

`AutoForwardService` (started by `EngineHost` alongside the peer and interface listeners, in both Client and
Headless mode, a no-op if the host configured no auto forward controllers at all - see
[Configuration.md](Configuration.md#auto-forward-controllers)) subscribes to `FrameDelivered` and, for each
delivered message, checks every `IEngineController.AutoForwardControllers` entry this instance's own installed
user is named in: a controller whose `Filter` accepts the message is forwarded to every user currently on that
controller's locally-saved target list (`IAutoForwardTargetsRepository`, see `Docs/Components/Data.md`), unless
that list is empty, in which case nothing happens. A forwarded message is a freshly built instance of the
configured frame type carrying the original's body and other content fields unchanged, addressed to
the target list and routed via `IMessageRoutingService.RouteFrame` from this instance's own installed user - the
same routing path a hook-originated send uses - so it becomes an ordinary Outbox record and a new message ID, not
a re-send of the original. The current user's own name is always excluded from the forwarded address list, even
if present in the saved target list, since that message would otherwise be re-delivered right back to this same
instance, matching the same controller's filter again and forwarding forever. A controller with no access, or an
inaccessible one, or a `Filter` that throws, is skipped without affecting any other configured controller - a
failure is logged the same way a failing processor method is.

## Message Storage & Retrieval

Every server (see [Configuration.md](Configuration.md#server-storage)) keeps a copy
of what its own children send. `ServerRoutingService` hands every message it receives from a child in `HandleFromChild`, but not what another server forwards, to `IMessageStorageService.Store` before routing it; `Store` does nothing
unless the current user is a server, skips receipts and retrieval requests, keeps one copy per message
ID in the `stored_messages` collection (see [Data.md](Data.md#storedmessageentity)), and logs rather than throws so
storage can never interrupt routing.

A retrieval request is a frame the retrieval handler recognizes. A server detects it after deserializing,
instead of the ordinary path: if it is addressed to this server, `IMessageStorageService.Find` returns a copy per
matching stored message (fits the criteria, whoever sent or received it - the requester, whose copies these are, is
for a request from a child the authenticated connection's user, for one forwarded by another server it is the message's sender field), and
each copy is routed on its own address list - a copy addressed to the requester reaches a child of this server
directly, or the sibling server that owns the requester - without being stored again; if it is addressed to another
server, it is forwarded to that server, which answers the same way. A client or peer that receives a retrieval
request (`PeerFrameDispatcher`) ignores it. A client sends the request through
`IMessageRoutingService.RouteFrame` (`IRetrievalService`), which names the chosen storage server to the handler (`RetrievalCreateContext.Server`) which it stores as the request's destination, like any
message, and the answers arrive through the normal `FrameDelivered` path.

## Events

| Event | Raised by | Consumed by |
|-------|-----------|-------------|
| `IPeerService.FrameDelivered` | the role's service | DirectServiceConnection, EngineHooksService, AutoForwardService |
| `IPeerService.ReadReceiptReceived` | the role's service | MessageRoutingService |
| `IPeerService.DeliveryStatusChanged` | the role's service | MessageRoutingService |
| `IPeerService.UserConnected` / `UserDisconnected` | the role's service | EngineHooksService |
| `MessageRoutingService.DeliveryStatusChanged` | MessageRoutingService | DirectServiceConnection → IServiceConnection consumers |
| `EntryService.MessageRead` | EntryService | AlertViewModel |
