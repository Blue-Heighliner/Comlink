# MSMT Integration

Comlink's peer layer is built on [Mercury Secure Message Transport (MSMT)](Msmt.md), consumed via the
[`BlueHeighliner.Msmt`](https://www.nuget.org/packages/BlueHeighliner.Msmt/) NuGet package — a proper
transitive dependency of the published `BlueHeighliner.Comlink` package, not an embedded/bundled DLL. MSMT
is an application-layer protocol running on TLS 1.3 that provides:

- **Mutual certificate authentication** — every connection, in both directions, always authenticates both
  sides against a trusted certificate authority; there is no unauthenticated or unencrypted mode.
- **Framing and acknowledgement** — every message is individually acknowledged (or negatively
  acknowledged) by the receiving side before the sender's call returns.
- **Priority queueing** — a peer's queued sends are transmitted in priority order, highest first; a
  lower-priority send already in flight is never preempted.
- **Session Mode connections** - a negotiated, longer-lived, bidirectional connection either side may send
  more than one message over, kept alive automatically by MSMT's own keep-alive while idle.

See [Msmt.md](Msmt.md) for the protocol standard itself; this page covers only how Comlink integrates with it.

## Certificates

MSMT peer authentication is mandatory - there is no way to run without it. Every instance needs:

- **An identity certificate**.
- **A trusted certificate authority** that every peer's identity certificate must chain to.

The certificates always come from files, designated by the network configuration file: its `CertificateStore` is a folder holding one `{USERNAME}.pfx` PKCS#12 identity per user, from which a node loads the one named for the running user, and its `AuthorityCertificate` is the public certificate of the one authority trusted to sign every identity. Both are resolved relative to the network configuration file's own directory, and nothing is ever looked up in the system certificate store. A user's certificate carries the user's name as its common name, which is how a server recognizes who connected to it. Without both keys, or without the user's `.pfx` file or the authority file, building the MSMT options throws and the peer/interface listeners simply don't start, retried the next time the host restarts. See `Scripts/Scenarios/` for a working example: each scenario's `Config.json` names a store folder containing every user's `.pfx`, all signed by one shared `Scripts/Scenarios/Root.cer` authority. See [Config.md](Config.md) for both keys.

## Session Peer

Comlink uses MSMT's session-mode API, `IMsmtSessionPeer` (created via `IMsmtSessionPeer.IFactory.Create(MsmtSessionPeerOptions)`),
never the message-mode `IMsmtMessagePeer`: every Comlink connection (client/server hierarchy, and
interface) is a negotiated session connection that persists across multiple sends and is kept alive
automatically by MSMT's own idle keep-alive, rather than a fresh TLS connection (or rekey) per message.
Unlike message mode, a session connection is a real object the caller gets back and manages - MSMT itself
never reconnects or discards one on Comlink's behalf beyond a handshake timeout, a stall, or the negotiated
session lifetime ending. `MsmtPeerTransport` (see below) owns this: it opens one outbound connection to a
given point when it is connected to, and reuses that same connection for every later request over it
until MSMT reports it disconnected, at which point the next connect opens a fresh one.
Every peer service additionally runs a background `PeerConnectionMonitor` per outgoing point
(a client's server; a server's or peer's configured points), sending a
heartbeat (a frame from the host's optional heartbeat handler) on an interval so each of those connections is proactively opened and kept alive even
when no real message is being sent - see [Peer.md](Peer.md#connection-status-client-and-server) for the
full mechanism and why `IMsmtReachabilityChecker.Reach` is not used for this.

```csharp
IMsmtSessionPeer peer = peerFactory.Create(options);
peer.Receiver = (connection, payload, responder) =>
{
    using (payload) { /* copy out what is needed */ }
    responder?.Accept(ReadOnlyMemory<byte>.Empty);
};
peer.StartListener(port);
IMsmtConnection connection = peer.Connect(new MsmtNameTarget { Host = host, Port = port, ServerName = host });
await connection.Wait();
MsmtResponse response = await connection.Request(payload);
```

Peer events are exposed as `IObservable<T>` rather than plain C# events. `IMsmtSessionPeer.Connected`
publishes only incoming connections - one this peer dials out itself via `Connect` is handed straight back
instead, never raised there - while `Disconnected` covers both directions. A received message must be
acknowledged explicitly through the `IMsmtResponder` the `Receiver` delegate is given (`Accept`/`Reject`, exactly once,
`null` when the sender asked for no acknowledgement); nothing is auto-accepted. The delegate also owns the pooled payload
and must dispose it, so the transport copies the bytes out and disposes it straight away.

## Bidirectional Connections

A session connection is bidirectional, and Comlink uses it that way: a request goes over whichever
connection is currently identified as the recipient (see [Identification.md](Identification.md)), whether
this node dialed it or the recipient did. That is why a client needs no listener of its own and a server
needs no address for its children: the server delivers back down the connection the client opened. Because no
node is configured with the users it expects, a connection is matched to a user by the certificate it
presents, not by where it was dialed: `IMsmtConnection.Direction` distinguishes an accepted connection from one
this node dialed, and `IMsmtConnection.Identity` exposes the remote peer's certificate subject (a plain
distinguished-name string, e.g. `CN=Client1`), which `MsmtPeerTransport` turns into the connection's
`IIpConnectionInfo` (host, port, and every common name). `InterfaceService` sends a frame the processor asks for (`SendInterface`) down the connection each interface client opened, and mirrors nothing on its own.

## Comlink Integration

| Component | Role |
|-----------|------|
| `ClientPeerService` and `ServerRoutingService` | Each wraps a single `IPeerTransport` (IP through `MsmtPeerTransport`, which wraps the `IMsmtSessionPeer`, and serial through `SerialPeerTransport`) for `UserRole.Peer`; keeps a connection to each outgoing point and sends to a user over the connection identified as them, serializes/deserializes instances of `IEngineController.FrameType` (see [Configuration.md](Configuration.md#frame-format)), and dispatches `FrameDelivered`/`DeliveryStatusChanged` events derived directly from the transport's `Request` outcome and its `Transmitted` progress callback. |
| `ClientPeerService` (`Core/src/Internal/Peer/ClientPeerService.cs`) | Implements `UserRole.Client`: sends every outbound message over its one connection to the server, which delivers back down that same connection. Proactively maintains the connection via `PeerConnectionMonitor`. |
| `ServerRoutingService` (`Core/src/Internal/Peer/ServerRoutingService.cs`) | Implements `UserRole.Server`: accepts connections from child clients and other servers, keeps a connection to each outgoing point, and delivers to any recipient (a child or another server) over the connection identified as them. Proactively maintains each outgoing point via `PeerConnectionMonitor`. |
| `PeerConnectionMonitor` (`Core/src/Internal/Peer/PeerConnectionMonitor.cs`) | Connects to an outgoing point and sends a periodic heartbeat (an empty message) over the connection so it opens and stays open without needing a real message. See [Session Peer](#session-peer). |
| `MsmtPeerTransport` (`Core/src/Internal/Peer/Transport/MsmtPeerTransport.cs`) | Adapts an `IMsmtSessionPeer` to the peer transport used by `PeerService`, `ClientPeerService`, and `ServerRoutingService`, caching one outbound connection per point and sending over inbound ones as well. MSMT itself remains IP only; serial goes through `SerialPeerTransport`. |
| `InterfaceService` (`Core/src/Internal/Peer/InterfaceService.cs`, always active) | Uses its own `IMsmtSessionPeer` to host the local interface listener described in [Interface.md](Interface.md). |
| `ConnectionOptions` (`IEngineController`) | Builds the `MsmtSessionPeerOptions` (identity certificate, trusted authority, then the host's `Connections().Msmt` adjustment: timeouts, keep-alive, session lifetimes) used for both inbound and outbound MSMT session peer connections. See [Configuration.md](Configuration.md#msmt-certificates). |
| `CertificateStore`/`AuthorityCertificate` (network file) | Designate the folder of `{USERNAME}.pfx` identities and the authority certificate file. See [Configuration.md](Configuration.md#msmt-certificates). |

`EngineExtensions.UseEngine` calls the package's `AddMsmt()` to register `IMsmtSessionPeer.IFactory` (and
`IMsmtMessagePeer.IFactory`, unused by Comlink) by convention.
