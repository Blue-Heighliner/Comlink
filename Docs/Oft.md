# Open Frame Transport (OFT)

Comlink's peer layer is built on [Open Frame Transport (OFT)](https://github.com/Blue-Heighliner/Open-Frame-Transport), consumed via the [`BlueHeighliner.OpenFrameTransport`](https://www.nuget.org/packages/BlueHeighliner.OpenFrameTransport) NuGet package. OFT is an application-layer protocol running on top of TCP and TLS 1.3 that provides:

- **Framing** — a byte stream is broken into discrete messages.
- **Acknowledgement** — every packet is individually acknowledged before the next one is sent, giving the connection simple, deterministic flow control.
- **Priority interruption** — many messages can be in flight logically at once; a high-priority message transparently interrupts a lower-priority one in progress, which resumes automatically afterward.
- **Cancellation** — a queued or in-progress send can be cancelled at any time before it completes.
- **Security modes** — each connection chooses one of four TLS modes, from no TLS at all up to mutual authentication (see below).
- **TLS rekeying** — a connection's TLS session can be rekeyed in place, manually or on an interval, without reconnecting.
- **Polling** — idle connections are verified alive on a fixed interval and closed if the peer stops responding.

The full protocol specification and wire format live in the upstream repository's `Docs/OFT.md`. This page covers only how Comlink integrates with it.

## Security Modes (`OftSecurityMode`)

| Mode | TLS | Client identity | Server identity |
|------|-----|------------------|------------------|
| `Trusted` | None | — | — |
| `Secure` (default) | Yes, ephemeral cert | Not authenticated | Not authenticated (ephemeral cert accepted unconditionally) |
| `ServerAuthentication` | Yes | Not authenticated | Authenticated (not valid for a peer — no client/server delineation) |
| `DualAuthentication` | Yes, mutual | Authenticated | Authenticated |

Comlink's peer connections always use one `IOftPeer`, which has no client/server delineation, so only `Trusted`, `Secure`, and `DualAuthentication` are valid — `ServerAuthentication` throws.

## Peer-to-Peer API Shape

`IOftPeer` (created via `IOftPeerFactory.Create(OftPeerOptions)`) is a convenience layer over the lower-level connector/hoster: sending to a `host:port` transparently reuses a cached connection or creates and caches a new one, idle/expired/excess connections are evicted automatically, and inbound listening is optional (`IOftPeer.Listen`).

```csharp
IOftPeer peer = peerFactory.Create(options);
peer.ReceivedHandler = (identity, data) => { /* handle payload, then dispose data */ };
await peer.Listen(new IPEndPoint(IPAddress.Any, port), cancellation);
await peer.Send(host, port, payload, cancellationToken: cancellation);
```

## Comlink Integration

| Component | Role |
|-----------|------|
| `PeerService` (`Engine/src/Peer/PeerService.cs`) | Wraps a single `IOftPeer`; resolves user names to endpoints via `IUserLocator`, serializes/deserializes instances of `IMessageFormat.MessageType` (see [Control.md](Control.md#imessageformat)), and dispatches `MessageDelivered`/`DeliveryStatusChanged` events driven by OFT's own delivery status. |
| `InterfaceService` (`Engine/src/Interface/InterfaceService.cs`, always active) | Uses `IOftHoster` directly (not `IOftPeer`, since interface connections are inbound-only and represent no user) to host the local interface listener described in [Interface.md](Interface.md). |
| `IOftCertificateProvider` / `OftCertificateProvider` | Builds the `OftPeerOptions` (certificate, certificate validation, security mode) used for both inbound and outbound peer connections. See [Control.md](Control.md). |
| `IOftPeerCertificateName` / `OftPeerCertificateName` | Maps the local user name to a certificate subject name to look up in the system store. See [Control.md](Control.md). |

`EngineExtensions.UseEngine` calls the package's `AddOpenFrameTransport()` to register `IOftConnector`, `IOftHoster`, and `IOftPeerFactory` by convention.

See [Peer.md](Peer.md) for the Engine-level peer message format and delivery-status mapping built on top of OFT, and [Interface.md](Interface.md) for the local interface listener.
