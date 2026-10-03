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

Two independent sources are supported, chosen per user in the network configuration file:

- **System certificate store** (the default): looked up by subject name via the user's `CertificateName` in
  the network configuration file (default: the user name itself, unprefixed) and the trusted authority name
  (`TrustedAuthority` or the file's `TrustedAuthorityCertificateName`, default: `COMLINK-ROOT`).
  Resolved once a current user is registered; before that (a fresh install with no installed user yet),
  building the MSMT options throws and the peer/interface listeners simply don't start, retried
  the next time the host restarts after a user is installed. A real deployment provisions its own
  certificates under these subject names through whatever process manages its certificate store.
- **Certificate files**: the network file's `CertificateStore` (a folder holding one `{USERNAME}.pfx` identity per user) and
  `AuthorityCertificate` load the running user's PKCS#12 identity file and a public authority file directly from disk instead,
  resolved relative to the network configuration file's own directory. See `Scripts/Scenarios/` for a working example:
  each scenario's `Config.json` names a store folder containing every user's `.pfx`, all signed by one shared
  `Scripts/Scenarios/Root.cer` authority. See [Config.md](Config.md) for both keys.

## Session Peer

Comlink uses MSMT's session-mode API, `IMsmtSessionPeer` (created via `IMsmtSessionPeer.IFactory.Create(MsmtSessionPeerOptions)`),
never the message-mode `IMsmtMessagePeer`: every Comlink connection (peer, client/server hierarchy, and
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
`IIpConnectionInfo` (host, port, and every common name). `InterfaceService` does not mirror inbound peer messages
back out to a connected interface client over the connection it opened in; see [Peer.md](Peer.md) and
[Interface.md](Interface.md).

## Comlink Integration

| Component | Role |
|-----------|------|
| `PeerService` (`Core/src/Internal/Peer/PeerService.cs`) | Wraps a single `IPeerTransport` (IP through `MsmtPeerTransport`, which wraps the `IMsmtSessionPeer`, and serial through `SerialPeerTransport`) for `UserRole.Peer`; keeps a connection to each outgoing point and sends to a user over the connection identified as them, serializes/deserializes instances of `IEngineController.FrameType` (see [Configuration.md](Configuration.md#frame-format)), and dispatches `FrameDelivered`/`DeliveryStatusChanged` events derived directly from the transport's `Request` outcome and its `Transmitted` progress callback. |
| `ClientPeerService` (`Core/src/Internal/Peer/ClientPeerService.cs`) | Implements `UserRole.Client`: sends every outbound message over its one connection to the server, which delivers back down that same connection. Proactively maintains the connection via `PeerConnectionMonitor`. |
| `ServerRoutingService` (`Core/src/Internal/Peer/ServerRoutingService.cs`) | Implements `UserRole.Server`: accepts connections from child clients and other servers, keeps a connection to each outgoing point, and delivers to any recipient (a child or another server) over the connection identified as them. Proactively maintains each outgoing point via `PeerConnectionMonitor`. |
| `PeerConnectionMonitor` (`Core/src/Internal/Peer/PeerConnectionMonitor.cs`) | Connects to an outgoing point and sends a periodic heartbeat (an empty message) over the connection so it opens and stays open without needing a real message. See [Session Peer](#session-peer). |
| `MsmtPeerTransport` (`Core/src/Internal/Peer/Transport/MsmtPeerTransport.cs`) | Adapts an `IMsmtSessionPeer` to the peer transport used by `PeerService`, `ClientPeerService`, and `ServerRoutingService`, caching one outbound connection per point and sending over inbound ones as well. MSMT itself remains IP only; serial goes through `SerialPeerTransport`. |
| `InterfaceService` (`Core/src/Internal/Peer/InterfaceService.cs`, always active) | Uses its own `IMsmtSessionPeer` to host the local interface listener described in [Interface.md](Interface.md). |
| `ConnectionOptions` (`IEngineController`) | Builds the `MsmtSessionPeerOptions` (identity certificate, trusted authority, then the host's `MsmtOptions` adjustment: timeouts, keep-alive, session lifetimes) used for both inbound and outbound MSMT session peer connections. See [Configuration.md](Configuration.md#msmt-certificates). |
| `CertificateName`/`TrustedAuthority` (`IEngineBuilder`) | Map the local user name, and the trusted certificate authority, to certificate subject names to look up in the system store. See [Configuration.md](Configuration.md#msmt-certificates). |

`EngineExtensions.UseEngine` calls the package's `AddMsmt()` to register `IMsmtSessionPeer.IFactory` (and
`IMsmtMessagePeer.IFactory`, unused by Comlink) by convention.
