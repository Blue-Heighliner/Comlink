# Interface Contract

The engine always exposes a local **interface listener** — in both `Client` and `Headless` mode — that
lets an external program compose messages through this user's own identity. An interface connection is
not a request/response control channel — it uses the same transport and message type as a peer connection
(see [MsmtIntegration.md](MsmtIntegration.md) and [Peer.md](Peer.md#message-format)) and carries nothing
but instances of `IEngineController.MessageType`, with no envelope or command discriminator. That type is
injectable by the host (see [Control.md](Control.md)); an external program must encode/decode whatever
concrete type the running engine is configured with.

An interface connection represents no user of its own:

- **Interface → outbound**: every message an interface sends is routed out to peers exactly as if this
  user's own installed identity had composed and sent it — `Subject`, `Body`, and `Addresses` are read
  from the message via `IEngineController`; `MessageId`, `FromUser`, and `SentAt` are ignored and
  re-assigned by `MessageRoutingService.Route`, the same call `DirectServiceConnection.SendMessage`
  makes for a GUI-composed send.
- **Inbound → interface**: not currently supported. Mirroring a message this user receives from a peer
  out to a connected interface would need that interface client's connection kept open and correlated to
  its own inbound peer traffic, rather than treated as a one-way injection point - see
  [MsmtIntegration.md](MsmtIntegration.md#no-server-initiated-delivery) for why Comlink never writes back
  down a connection a remote party opened to it, interface connections included; not yet provided.

## Connection

- **Address**: `127.0.0.1` (loopback only)
- **Port**: configurable via `IEngineController.InterfacePort`; default **50020**
- **Transport**: MSMT session mode, mutual TLS authenticated using this instance's own `IEngineController.ConnectionOptions` - an interface client must present a certificate signed by the same trusted authority (see [Control.md](Control.md#msmt-certificates))
- Multiple simultaneous interface connections are supported

## Delivery status

There is no acknowledgement message on the wire in either direction beyond MSMT's own message
acknowledgement. Delivery status for a message an interface causes to be routed out is tracked the same
way any outbound send is: through MSMT's own delivery-status stream, surfaced by
`IPeerService`/`IMessageRoutingService` as `DestinationStatus` transitions (see
[Peer.md](Peer.md#delivery-status)). An interface has no way to observe those transitions directly — it is
a one-way injection point, not a client of the routing result.

## Example (C#, using the MSMT reference implementation)

```csharp
using BlueHeighliner.Msmt;

IMsmtSessionPeer client = new IMsmtSessionPeer.Factory().Create(new MsmtSessionPeerOptions
{
    Credentials = new MsmtCredentials { Identity = myCertificate, TrustedAuthorities = trustedAuthorities }
});

IMsmtConnection connection = client.Connect(new MsmtNameTarget { Host = "127.0.0.1", Port = 50020, ServerName = "127.0.0.1" });
await connection.Wait();

// Anything sent here, encoded as whatever type the running engine's host registered for
// IEngineController (SampleMessage in the Sample host — see Control.md), is routed out to peers
// as if this user sent it.
connection.Send(messageBytes);
```
