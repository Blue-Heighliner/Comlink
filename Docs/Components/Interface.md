# Interface Contract

The engine always exposes a local **interface listener** — in both `Client` and `Headless` mode — that
lets an external program compose messages through this user's own identity. An interface connection is
not a request/response control channel — it uses the same transport and frame type as a peer connection
(see [MsmtIntegration.md](MsmtIntegration.md) and [Peer.md](Peer.md#frame-format)) and carries nothing
but instances of the host's frame type, with no command discriminator. That type is
injectable by the host (see [Configuration.md](Configuration.md)), and so is how it is serialized: an external program must
encode whatever concrete type the running engine is configured with using the engine's
the frame serializer. With the default, `ProtobufSerializer`, that is a protobuf-net
envelope holding the type's assembly-qualified name and the message's own protobuf-net encoding as nested bytes,
and only the engine's own frame type is accepted. Payloads on an interface connection are never packetized,
whatever the host's packet type is.

An interface connection represents no user of its own:

- **Interface → processor**: every frame an interface sends is handed to the network processor's `OnReceived` with `FrameOrigin.Interface`. The engine reads nothing from it; the processor decides what it means, typically sending it on as if this user's own installed identity had composed it.
- **Processor → interfaces**: the processor can send a frame to every connected interface with `SendInterface(priority, frame)`. Interface connections are bidirectional MSMT session connections, so the frame goes down the connection the interface itself opened, and an interface receives frames the same way it sends them: serialized instances of the host's frame type, with the interface's own MSMT receiver answering each. Nothing is sent to an interface unless the processor sends it; the engine never mirrors anything itself, and an interface that is not connected misses what is sent meanwhile.

## Connection

- **Address**: `127.0.0.1` (loopback only)
- **Port**: configurable via `InterfacePort`; default **50020**
- **Transport**: MSMT session mode, mutual TLS authenticated using this instance's own MSMT options - an interface client must present a certificate signed by the same trusted authority (see [Configuration.md](Configuration.md#msmt-certificates))
- Multiple simultaneous interface connections are supported

## Delivery status

There is no acknowledgement message on the wire in either direction beyond MSMT's own message acknowledgement. An interface has no way to observe delivery status: it is a one-way injection point.

## Example (C#, using the MSMT reference implementation)

```csharp
using BlueHeighliner.Msmt;

IMsmtSessionPeer client = new IMsmtSessionPeer.Factory().Create(new MsmtSessionPeerOptions
{
    Credentials = new MsmtCredentials { Identity = myCertificate, TrustedAuthorities = trustedAuthorities }
});

IMsmtConnection connection = client.Connect(new MsmtNameTarget { Host = "127.0.0.1", Port = 50020, ServerName = "127.0.0.1" });
await connection.Wait();

// Anything sent here, serialized the way the running engine's FrameSerializer does it for the message
// type its host registered (Frame in the Sample host), is routed out to peers as if this user
// sent it.
connection.Send(messageBytes);
```
