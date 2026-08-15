# Interface Contract

The engine always exposes a local **interface listener** — in both `Client` and `Headless` mode — that
lets an external program plug directly into this user's message stream. An interface connection is not
a request/response control channel — it uses the same transport and message type as a peer connection
(see [Oft.md](Oft.md) and [Peer.md](Peer.md#message-format)) and carries nothing but instances of
`IEngineController.MessageType`, with no envelope or command discriminator. That type is injectable by the
host (see [Control.md](Control.md)); an external program must encode/decode whatever concrete type the
running engine is configured with.

An interface connection represents no user of its own. It is purely a relay:

- **Inbound → interface**: every message this user receives from a peer is mirrored, unmodified, to
  every currently connected interface.
- **Interface → outbound**: every message an interface sends is routed out to peers exactly as if this
  user's own installed identity had composed and sent it — `Subject`, `Body`, and `Addresses` are read
  from the message via `IEngineController`; `MessageId`, `FromUser`, and `SentAt` are ignored and
  re-assigned by `MessageRoutingService.Route`, the same call `DirectServiceConnection.SendMessage`
  makes for a GUI-composed send.

## Connection

- **Address**: `127.0.0.1` (loopback only)
- **Port**: configurable via `IEngineController.InterfacePort`; default **50020**
- **Transport**: OFT, `OftSecurityMode.Trusted` (no TLS — local loopback IPC only)
- Multiple simultaneous interface connections are supported; each receives every mirrored message independently

## Delivery status

There is no acknowledgement message on the wire in either direction. Delivery status for a message an
interface causes to be routed out is tracked the same way any outbound send is: through OFT's own
`OftDeliveryStatus` stream, surfaced by `IPeerService`/`IMessageRoutingService` as `DestinationStatus`
transitions (see [Peer.md](Peer.md#delivery-status)). An interface has no way to observe those
transitions directly — it is a one-way injection point, not a client of the routing result.

## Example (C#, using the OFT reference implementation)

```csharp
using BlueHeighliner.OpenFrameTransport;

OftConnectionOptions options = new() { Info = "", SecurityMode = OftSecurityMode.Trusted };
await using IOftConnection connection = await new OftConnector().Connect("127.0.0.1", 50020, options);

// Mirrored inbound messages arrive here, encoded as whatever type the running engine's host
// registered for IEngineController (SampleMessage in the Sample host — see Control.md).
connection.ReceivedHandler = data =>
{
    // Deserialize with protobuf-net using that type, then dispose data.
};

// Anything sent here, in the same type, is routed out to peers as if this user sent it.
await connection.Send(messageBytes);
```
