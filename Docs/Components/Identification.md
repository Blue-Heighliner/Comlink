# Connection Identification

A node is configured with the places it connects and listens, not with the users it expects there. The user it runs states, on its user info, the `PeerPort` it listens on for IP connections and the `OutgoingPoints` it dials: the IP hosts and ports and the serial ports it opens. Nothing ties a user name to a point, so the same node can be reached by a different user from a different point tomorrow without any node's configuration changing. Who is on the other end of a connection is instead worked out at the moment the connection forms, and a message for a user goes over whichever connection is then identified as them.

## Where it happens

`IdentifyingPeerTransport` is the outermost `IPeerTransport` decorator (`PeerTransportFactory` builds composite, then packetizing when configured, then identifying). It publishes a connection on `Connected` only once the connection has been identified, sets `PeerConnection.User` before doing so, and only publishes `Disconnected` for a connection that was published. `Connect` and `Request` wait for a connection to be identified, so the peer services never see, or send on, an anonymous one. A connection that cannot be identified is dropped, which makes the `Connect` that produced it fail with `IOException`.

The peer services keep the established connections in a `UserConnections`, keyed by the identified name (ignoring case) with the newest connection winning, and look a recipient up there. Because MSMT session connections carry requests in both directions, a connection the recipient opened is as good as one this node opened, so a client needs no listener and a server needs no address for its children.

## Identifying a connection

`IEngineBuilder.Identify` states the hook, which is optional. `ConnectionInfo` describes what is known:

- **IP**: the remote host and port (an inbound connection's port is the remote node's ephemeral one), the certificate subject, and the common names in it.
- **Serial**: the local port name and the HDLC station address.
- Whether the connection was opened by the remote node, and, when a connection message is configured, the connection message and response the remote node sent.

The hook returns a `UserIdentity`, a name plus app-specific data, or `null` to leave the decision to the engine:

- **IP**: the first user (from `Users`, and every server and child client in `Servers`) whose `CertificateName` matches one of the certificate's common names. The certificate authority already vouched for the certificate, so when none matches the connection is still accepted, as a user named after the first common name. A certificate with no common name cannot be identified.
- **Serial**: a user named after the port. A cable carries no certificate, so a host that needs real names overrides the hook, for example with a table from port and address to user, or reads the name out of a connection message.

An identity's `Data` defaults to the `Data` on the name's user info, which is where a host attaches whatever it wants to a user (the `Data` map of a user's entry in the network configuration file). The engine never interprets it; it travels with the identity for the host's own hooks. A hook that throws drops the connection.

Identity establishes who a connection is for routing and status only. It is not authentication beyond what the medium gives (the certificate authority for IP, the physical cable for serial), and the `FromUser` inside a message is still whatever the sender wrote.

Each role then applies its own policy to the identity. A peer accepts any identified connection. A client accepts only its own server connection. A server accepts a connection only when the name is one of its child clients or another server in `Servers`, and drops it otherwise or when that user has been closed.

## Connection message exchange

An IP certificate names a node but says nothing app-specific, and a serial port has no name at all, so a host may have nodes introduce themselves. This is off by default and enabled by `ConnectionMessage<TMessage>`, which states the type of the connection message and how it is built, with `ConnectionResponse<TResponse>` optionally adding a reply; both are protobuf types (or whatever `ConnectionSerializer` handles, which by default builds only those two types). The exchange runs before identification, which is then given both objects:

1. The node that opened the connection (both nodes of a serial link) builds a message with the `ConnectionMessage` function and sends it first. A `null` result is sent as an empty message.
2. The node that receives a message, when a response type is configured, builds a reply with the `ConnectionResponse` function, whose `ConnectionInfo.ConnectionMessage` is the message just received, and sends it back.
3. Each node is identified once it holds what it is waiting for: a node that received a connection is waiting for the message, a node that opened an IP connection for the response (when a response type is configured), and both ends of a serial link for both. Identification sees the remote node's message and the remote node's response to this node's message.

Once the exchange is on, every payload on every connection is framed with one leading byte (data, message, response), which is how the three are told apart, so all nodes on a network must be configured alike, as with packetization. The exchange has ten seconds to finish or the connection is dropped, and a message of the wrong type, one that does not deserialize, or one whose hook throws drops it as well. Data that arrives before the receiving node has finished identifying the connection is held (up to 64 payloads) and delivered in order once it has. Heartbeats are ordinary data, so they wait for the exchange like everything else. The exchange's own messages travel through the packetizer, when there is one, like any other payload, at the highest priority.
