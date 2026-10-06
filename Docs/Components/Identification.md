# Connection Identification

A node is configured with the places it connects and listens, not with the users it expects there. The user it runs states, on its user info, its `IpHost` and `Msmt` port it listens on for IP connections, its `Hdlc` address and ports, and its links to a parent and children, from which the points it dials follow: the other users' IP hosts and MSMT ports, and the HDLC ports it opens. Nothing in what a node connects with ties a user name to what arrives on a connection, so the same node can be reached by a different user from a different point tomorrow without any node's configuration changing. Who is on the other end of a connection is instead worked out at the moment the connection forms, and a message for a user goes over whichever connection is then identified as them.

## Where it happens

`HandshakePeerTransport` is the outermost `IPeerTransport` decorator (`PeerTransportFactory` builds composite, then an initial packet exchange and packetizing when configured, then the initial frame exchange with identification). It publishes a connection on `Connected` only once the connection has been identified, sets `PeerConnection.User` before doing so, and only publishes `Disconnected` for a connection that was published. `Connect` and `Request` wait for a connection to be identified, so the peer services never see, or send on, an anonymous one. A connection that cannot be identified is dropped, which makes the `Connect` that produced it fail with `IOException`.

The peer services keep the established connections in a `UserConnections`, keyed by the identified name (ignoring case) with the newest connection winning, and look a recipient up there. Because MSMT session connections carry requests in both directions, a connection the recipient opened is as good as one this node opened, so a client needs no listener and a server needs no address for its children.

## Identifying a connection

The engine names the user on the other end of a connection from what is known about it, described by an `IConnectionInfo` (which a host's processors narrow with a type pattern to `IIpConnectionInfo` or `ISerialConnectionInfo` for the medium specific details):

- **`IIpConnectionInfo`**: the remote host and port (an inbound connection's port is the remote node's ephemeral one), the certificate subject, and the common names in it.
- **`ISerialConnectionInfo`**: the local port name and the HDLC station addresses of both ends.
- **`IConnectionInfo`** (common to both): whether the connection was opened by the remote node and this node's own user name (`LocalUser`).

The user an initial packet or frame processor marked the connection connected as (below) wins. Otherwise the engine decides:

- **IP**: the first user (from `Users`, and every server and child client in `Servers`) whose `CertificateName` matches one of the certificate's common names. The certificate authority already vouched for the certificate, so when none matches the connection is still accepted, as a user named after the first common name. A certificate with no common name cannot be identified.
- **Serial**: the user named by the matching `Hdlc` link in the network file (matching port and address), else a user named after the port. A cable carries no certificate, so a host that needs more has its processors name the user during the initial exchange.

The identity the engine builds from the name carries the `Data` on that name's user info, which is where a host attaches whatever it wants to a user (the `Data` map of a user's entry in the network configuration file). The engine never interprets it; it travels with the identity for the host's own handlers.

Identity establishes who a connection is for routing and status only. It is not authentication beyond what the medium gives (the certificate authority for IP, the physical cable for serial), and the `FromUser` inside a frame is still whatever the sender wrote.

Each role then applies its own policy to the identity. A client accepts only its own server connection. A server accepts a connection only when the name is one of its child clients or another server in `Servers`, and drops it otherwise or when that user has been closed.



## Initial packet and frame exchange

An IP certificate names a node but says nothing app-specific, and a serial port has no name at all, so a host may have nodes introduce themselves. Everything that crosses a connection between nodes is a serialized instance of the configured frame type (through the network serializer) or, when packets are configured, of the configured packet type (through the packet serializer), and nothing else, and the introduction is no exception. It is off by default and carried out by up to two processors the host states:

- `IInitialPacketProcessor<TPacket>`, stated with `InitialProcessor` on the packet configuration (`Packets`): exchanges packets, sent straight to the wire (each is itself a packet, so it is not split), beneath the packetizer.
- `IInitialFrameProcessor<TFrame>`, stated with `InitialProcessor` on the frame configuration (`Frames`): exchanges frames, serialized and, when packets are configured, split into packets like any other frame, above the packetizer. They are not stored, routed or shown.

A processor has a `Timeout` and two methods, each handed a context (`IInitialPacketContext<TPacket>` or `IInitialFrameContext<TFrame>`) for that one connection. `OnConnected` runs on both nodes when the connection has formed; `OnReceived` runs on either node for each item the other sent while the connection is not yet marked connected. The engine has no notion of which node starts or of requests and replies: the processor decides from what the context exposes about the connection (`Connection`: whether it is inbound, and for a serial link the station addresses), so, for example, it can have the node that dialed, or the node at the higher station address, send first. The context, like every processor context, exposes a snapshot of the engine (`CurrentUser`, `Users`, `ConnectedUsers`, `IsConnected`); it can `Send` an item, mark the connection fully connected as a named user (`Connected`) or drop it (`Disconnect`). The methods and these calls are all synchronous (`void`): the calls are queued on the connection and carried out in the order made, after the method that made them returns, so a `Send` followed by `Connected` sends first, and an item that cannot be sent drops the connection. So the usual shape is: the node that decides it starts sends in `OnConnected`, the other answers in `OnReceived` and calls `Connected`, and the first calls `Connected` when the answer arrives; a longer conversation just sends more items before `Connected`.

The packet exchange runs first and the frame exchange after it, and the user named by the frame processor wins over the packet processor's. A connection that is never marked connected is dropped, and a node with no processor at a stage passes straight through it. The user a processor names is used as the identity (with the `Data` of that name's user info) ahead of the engine's own rule.

Nothing is added to what crosses the connection: there is no frame, tag or marker. Every payload a node receives before the connection is marked connected is an item for its processor, so all nodes on a network must be configured alike, as with packetization, and a processor should finish its own side of the conversation before it calls `Connected` (the node that answers sends its reply before marking itself connected, for instance, so the other node's first real payload cannot be taken for an item). The exchange has the processor's `Timeout`, counted from the connection forming, to finish or the connection is dropped, and an item of the wrong type, one that does not deserialize, or a processor that throws drops it as well. Data that arrives after a node has been marked connected but before it has finished identifying the connection is held (up to 64 payloads) and delivered in order once it has. A connection is not usable until the exchange is over, and a heartbeat (see [Peer.md](Peer.md)) waits for it like everything else.

`IConnectionInfo.LocalUser` is the name of the user this node runs as, so a processor can say who is speaking. `Sample`'s `IdentityProcessor` does this with packets: it decides that the node that dialed an IP connection, or the node at the higher station address on a serial cable, sends a `Packet` whose chunk is its user name, the other node answers with one carrying its own, and each marks the connection connected as the name it received, so a serial link needs no `User` on its outgoing point and an IP connection does not depend on certificate names.
