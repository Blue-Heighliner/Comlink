# Transport

`IPeerTransport` is the layer between the peer services (`PeerService`, `ClientPeerService`, `ServerRoutingService`) and the wire. It moves opaque, already serialized messages to a `UserEndpoint` and reports connections coming and going. A `UserEndpoint` is either an IP host and port or a MicroGate serial port name and HDLC address, and the transport, not the service, picks the medium. The services therefore have no knowledge of TLS, MSMT, HDLC, or serial ports, and a node can reach some users over IP and others over a cable at the same time.

## Shape

`PeerTransportFactory` builds a `CompositePeerTransport` from two halves:

- `MsmtPeerTransport` adapts an `IMsmtPeer` for IP endpoints (mutually authenticated TLS, dialed on demand, session connections).
- `SerialPeerTransport` holds one persistent `SerialLink` per distinct serial endpoint for MicroGate cables.

The composite routes each `Request` and `Open` by `UserEndpoint.IsSerial` and merges the `Received`, `Connected`, and `Disconnected` streams of both halves. A `Request` completes only when the remote node has acknowledged the message, and throws if it could not be delivered; both halves honor that same contract, which is what lets the services stay medium agnostic.

The IP half needs an identity certificate for the current user, which is unavailable before a user is installed or on a node that never uses IP. In that case the factory logs a warning and builds the composite without it: IP requests then fail with `IOException` while serial keeps working. This is what allows a serial-only node to run with no certificates at all.

## Endpoints and identity

A `UserEndpoint` with a `SerialPort` is serial; `IpAddress`/`Port` are then ignored. `SerialAddress` is the HDLC station address (default 255) and must match on both ends of a cable. Endpoints compare equal by `Key`: IP by host (case-insensitive) and port, serial by port name (case-insensitive) and address. Two configured users naming the same port and address share one link.

Identity works differently per medium, and the services account for it through `PeerConnection`:

- **IP**: a remote node is identified by its certificate. An inbound connection carries the certificate subject and no endpoint; an outbound one carries the endpoint that was dialed.
- **Serial**: a cable joins exactly two nodes, so the remote node is simply whoever is configured on that port. A serial connection is always reported as the link this node opened (never inbound), with the configured endpoint and no certificate subject. `ServerRoutingService` recognizes a child or sibling server on a serial link by matching that endpoint against its own `Servers` map and `GetEndpoint` results.

There is no authentication or encryption on a serial link. The trust model is the physical cable: only configure a serial endpoint for a port whose far end you control.

## Serial links

The MicroGate device speaks HDLC in asynchronous balanced mode: both ends are equals, either may initiate, and frames arrive in order without gaps while the link is up. A MicroGate peer is single use and a frame carries at most about 4 KB, so `SerialLink` adds what a message transport needs on top:

- **Connect loop**: the link creates a peer, starts it (which completes only once the far end answers), publishes `Connected`, and waits for the peer to end. On loss it fails every pending request, publishes `Disconnected`, and after a short delay creates a new peer. A device that cannot be opened is retried the same way, with a single warning logged per outage instead of one per attempt.
- **Opening**: a serial link starts connecting the first time it is opened or sent to. `PeerService` opens every configured serial user at startup so their messages are received before anything is sent to them, and the client and server roles open theirs through the connection monitor's first heartbeat.
- **Framing** (`SerialFrame`): a message is split into numbered fragments (9 byte header: kind, message id, fragment index, fragment count), sent contiguously under a send lock, and reassembled on the far end. After the last fragment is delivered to `Received` subscribers the far end sends a 6 byte reply frame carrying the accept, which completes the sender's `Request`. Malformed frames, out of order fragments, and messages over 64 MiB are dropped.
- **Request semantics**: `Request` throws `IOException` immediately when the link is down, when it drops while waiting, or when no reply arrives within a minute. `Transmitted` fires once the last fragment has been handed to the device.
- **Ordering**: sends are serialized in call order. `PeerSendOptions.Priority` is not honored on serial, unlike over MSMT where higher priority goes first.
- **Empty payloads** (the connection monitor's heartbeat) are ordinary messages and are acknowledged like any other; the receiving services ignore them.

## Configuration

Serial endpoints are configured wherever an IP endpoint is: `Users` entries, `ServerEndpoint`, and `ServerUsers` entries take `SerialPort` (and optionally `SerialAddress`) in place of `IpAddress`/`Port`. A server whose own `ServerUsers` entry is serial has no IP address to listen on and starts no TCP listener; a client whose `ServerEndpoint` is serial likewise starts none, since the one cable already carries both directions. Because each node has its own configuration, the same server can be an IP endpoint in one node's `ServerUsers` map and a serial endpoint in another's.
