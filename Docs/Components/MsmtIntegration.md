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
- **Three connection lifecycle modes** — a fresh TLS connection per message (default), one connection
  rekeyed periodically across several messages, or one longer-lived session negotiated up front.
- **Automatic keep-alives** — an idle Session Mode connection sends reachability checks on its own so it
  isn't dropped for inactivity.

See [Msmt.md](Msmt.md) for the protocol standard itself; this page covers only how Comlink integrates with it.

## Certificates

MSMT peer authentication is mandatory - there is no way to run without it. Every instance needs:

- **An identity certificate**.
- **A trusted certificate authority** that every peer's identity certificate must chain to.

Two independent sources are supported, chosen per `config.json`:

- **System certificate store** (the default): looked up by subject name via `IEngineController.GetCertificateName`
  (default: the user name itself, unprefixed) and `IEngineController.TrustedAuthorityCertificateName` (default: `COMLINK-ROOT`).
  Resolved once a current user is registered; before that (a fresh install with no installed user yet),
  `IEngineController.ConnectionOptions` throws and the peer/interface listeners simply don't start, retried
  the next time the host restarts after a user is installed. A real deployment provisions its own
  certificates under these subject names through whatever process manages its certificate store.
- **Certificate files**: `config.json`'s `PeerCertificateFile`/`TrustedAuthorityCertificateFile` fields load
  a PKCS#12 identity file and a public authority file directly from disk instead, resolved relative to the
  config file's own directory. See `Scripts/Scenarios/` for a working example: each scenario's config points
  at a `.pfx` identity file in the same directory, all signed by one shared `Scripts/Scenarios/Root.cer`
  authority. See [Config.md](Config.md) for both fields.

## Session Mode

`IEngineController.ConnectionOptions` always sets `MsmtOptions.Mode` to `MsmtOperationMode.Session` — every
Comlink connection (peer, client/server hierarchy, and interface) negotiates a session up front rather than
opening a fresh TLS connection per message or rekeying periodically. A session connection persists across
multiple sends and is kept alive automatically by MSMT's own idle keep-alive, instead of tearing down after
every message. `ClientPeerService` and `ServerRoutingService` additionally run a background
`MsmtConnectionMonitor` per hierarchical target (a client's server; a server's own children and sibling
servers), sending an empty heartbeat payload on an interval so each of those connections is proactively
opened and kept alive even when no real message is being sent — see
[Peer.md](Peer.md#connection-status-client-and-server) for the full mechanism and why `IMsmtPeer.Test` is
not used for this.

## Peer-to-Peer API Shape

`IMsmtPeer` (created via `IMsmtPeerFactory.Create(MsmtOptions)`) manages one listener for incoming
connections and a separate outgoing connection per remote target sent to, created on demand. Peer events are
exposed as `IObservable<T>` rather than plain C# events; `Request` sends a payload and asynchronously awaits
the remote peer's acknowledgement directly, without any separate tag-tracking on Comlink's side:

```csharp
IMsmtPeer peer = peerFactory.Create(options);
peer.Received.Subscribe(args => { /* handle payload, then dispose it; auto-acknowledged unless Deferred */ });
peer.StartListener(port);
MsmtResponse response = await peer.Request(new MsmtTarget { Host = host, Port = port }, payload);
```

## No Server-Initiated Delivery

MSMT's client-request/server-response model means a connection a remote peer initiated can only ever be
used to acknowledge what that peer sends - it can never be used to push something new back down it. Every
Comlink component that needs to deliver a message to a specific remote peer (`PeerService`,
`ClientPeerService`, `ServerRoutingService`) always does so by dialing out to that peer's own receiver as an
MSMT client, identified by certificate subject rather than any self-declared name - never by writing to a
connection that peer itself opened. On the receiving side, `IMsmtConnection.Sender`/`Receiver` distinguish
which direction a given connection represents (a link this instance dialed out on vs. one its listener
accepted), and `IMsmtConnection.Identity`/`IMsmtLink.Identity` expose the remote peer's certificate subject
(a plain distinguished-name string, e.g. `CN=Client1`) for `ServerRoutingService` to match against
`IEngineController.GetCertificateName` — see [Peer.md](Peer.md#connection-status-client-and-server).
`InterfaceService` does not mirror inbound peer messages out to a connected interface client: an interface
tool would need to run its own MSMT receiver for Comlink to dial back into, a materially different
integration shape than "open a socket and read" that is not yet provided. See [Peer.md](Peer.md) and
[Interface.md](Interface.md).

## Comlink Integration

| Component | Role |
|-----------|------|
| `PeerService` (`Core/src/Peer/PeerService.cs`) | Wraps a single `IMsmtPeer` for `NodeRole.Peer`; resolves user names to endpoints via `IEngineController.GetEndpoint`, serializes/deserializes instances of `IEngineController.MessageType` (see [Control.md](Control.md#message-format)), and dispatches `MessageDelivered`/`DeliveryStatusChanged` events derived directly from `IMsmtPeer.Request`'s outcome and its `PackageChanged` progress stream. |
| `ClientPeerService` (`Core/src/Peer/ClientPeerService.cs`) | Implements `NodeRole.Client`: sends every outbound message to the configured server, and also runs its own listener so the server can dial back in to deliver messages to it. Proactively maintains its connection to the server via `MsmtConnectionMonitor`. |
| `ServerRoutingService` (`Core/src/Peer/ServerRoutingService.cs`) | Implements `NodeRole.Server`: accepts connections from child clients and other servers, and delivers to any recipient (a child or another server) by dialing out to that recipient's own endpoint. Proactively maintains a connection to each own child and each sibling server via `MsmtConnectionMonitor`. |
| `MsmtConnectionMonitor` (`Core/src/Peer/MsmtConnectionMonitor.cs`) | Sends a periodic empty heartbeat `Request` to a hierarchical target so its connection opens and stays open without needing a real message. See [Session Mode](#session-mode). |
| `InterfaceService` (`Core/src/Peer/InterfaceService.cs`, always active) | Uses its own `IMsmtPeer` to host the local interface listener described in [Interface.md](Interface.md). |
| `IEngineController.ConnectionOptions` | Builds the `MsmtOptions` (identity certificate, trusted authority) used for both inbound and outbound MSMT peer connections. See [Control.md](Control.md#msmt-certificates). |
| `IEngineController.GetCertificateName(userName)`/`TrustedAuthorityCertificateName` | Map the local user name, and the trusted certificate authority, to certificate subject names to look up in the system store. See [Control.md](Control.md#msmt-certificates). |

`EngineExtensions.UseEngine` calls the package's `AddMsmt()` to register `IMsmtPeerFactory` by convention.
