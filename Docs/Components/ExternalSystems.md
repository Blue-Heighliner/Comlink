# External Systems

An **external system** is a conduit between this instance and one system outside Comlink — a socket, a
message queue, an HTTP long-poll, or any other integration point a host wants to bridge into the
messaging flow. Unlike a peer or an [interface connection](Interface.md), an external system is not
another Comlink instance and does not speak MSMT; it is entirely defined by the host's own `IExternalSystem`
implementation, typically (though not necessarily) a subclass of the optional convenience base class
`ExternalSystemBase<TFrame>`.

## Interfaces

`Core/src/Public/ExternalSystems/ExternalSystem.cs` defines two types:

```csharp
public interface IExternalSystem
{
    event Func<object, Task>? MessageReceived;
    string Name { get; }
    bool IsConnected { get; }
    Task Start(CancellationToken cancellation);
    Task<bool> Send(object message);
    void AttachLogger(ILogger logger);
}

public abstract class ExternalSystemBase<TFrame> : IExternalSystem where TFrame : class
{
    protected abstract Task<bool> TryConnect(CancellationToken cancellation);
    protected virtual Task<bool> PollIsConnected(CancellationToken cancellation);
    protected abstract Task Disconnect();
    protected abstract Task<bool> Send(TFrame message);
    protected virtual bool FilterSent(TFrame message);
    protected Task Receive(TFrame message);
    protected virtual bool FilterReceived(TFrame message);
    protected void ReportDisconnected();
}
```

`IExternalSystem` is deliberately not generic over the frame type — it is declared `object`-typed on
`Send`/`MessageReceived` so `ExternalSystemsService` (below) can hold and drive every configured external
system uniformly, the same reasoning as the engine's message-format members (see
[Configuration.md](Configuration.md#frame-format)). `IEngineBuilder.ExternalSystem`/`ExternalServer` (see
[Configuration.md](Configuration.md#external-systems)) take a plain `IExternalSystem` too, so
a host is free to implement `IExternalSystem` directly if it wants full control. In practice, a host
instead subclasses the optional convenience base class `ExternalSystemBase<TFrame>`, which implements
`IExternalSystem` on your behalf and exposes only type-safe `TFrame`-typed members — `protected abstract`
methods for the real connection behavior (plus one `protected virtual` method, `PollIsConnected` — see
[Lifecycle](#lifecycle) below), and `protected Task Receive(TFrame message)` to report an inbound
message. `TFrame` should match the host's own frame type (the one given to `Frames<TFrame>`).

`ExternalSystemBase<TFrame>`'s constructor deliberately does not take an `ILoggerFactory` — each
external system is constructed directly by the host's `IEngineConfiguration`, not resolved from the running
engine's container; a logger injected into the configuration comes from the container the configuration was built in,
which writes to none of the engine's logs (the engine's logging providers, e.g. `DailyFileLoggerProvider`, need the
engine's configuration for their log file location, so they cannot exist until it has run). Instead, `AttachLogger` is
called once by `ExternalSystemsService`, using its own `ILoggerFactory` from the running container, before `Start` — an external
system logs to a no-op logger for any activity before that point.

## Lifecycle

`ExternalSystemBase<TFrame>.Start(CancellationToken)` runs a loop for as long as `cancellation` is not
cancelled:

1. While not connected, calls `TryConnect(cancellation)`. On success, `IsConnected` becomes `true`. On
   failure (a `false` return, or any thrown exception other than `OperationCanceledException`, which is
   logged and treated as a failed attempt), waits a retry interval (5 seconds by default) and tries again.
2. Once connected, waits a poll interval (5 seconds by default) or until `ReportDisconnected` is called
   (see below), then — unless `ReportDisconnected` was what woke it — calls `PollIsConnected(cancellation)`
   to check whether the connection is still alive. A `false` result (a `ReportDisconnected` call, a `false`
   return from `PollIsConnected`, or a thrown exception from it, logged and treated the same way)
   transitions `IsConnected` back to `false`, calls `Disconnect()` to let the implementor release any
   resources, and the loop returns to step 1 to attempt reconnection.

`PollIsConnected` is `protected virtual`, not `protected abstract` — its default implementation always
returns `true`, so an implementation whose external system requires no active polling (e.g. one that
instead learns about disconnection through an event or callback) can simply not override it. Such an
implementation calls `ReportDisconnected()` (a `protected` method, no return value) whenever its connection
tells it it has been lost; this interrupts the current poll wait immediately, so the disconnect/reconnect
cycle reacts right away rather than waiting up to the poll interval. `ReportDisconnected` is a no-op if not
currently connected.

`Send(object message)` returns `false` immediately without calling the abstract `Send(TFrame message)`
while not connected; while connected, it casts to `TFrame`, calls `FilterSent` (see below), and — if that
returns `true` — calls `Send(TFrame message)`, catching and logging any exception (from either) as a
failed send (returning `false`) rather than propagating it.

`Receive(TFrame message)` is called by the implementor (e.g. from its own background read loop,
socket callback, or poll) whenever the external system delivers a new message. It first calls
`FilterReceived` (see below); if that returns `false`, the message is silently dropped. Otherwise it
enqueues the message onto an internal, per-instance channel and returns — it does not wait for the message
to actually reach `MessageReceived`, so it is safe to call concurrently, or without awaiting a previous call
first, if the implementor's own connection can genuinely deliver messages that way (e.g. parallel socket
reads). A single internal loop, running for the lifetime of `Start`, drains that channel and delivers each
message to `MessageReceived` one at a time, in enqueue order — so `ExternalSystemsService` (and any other
subscriber) never sees two deliveries overlap, and messages are always processed in the order they were
enqueued, regardless of how many `Receive` calls were in flight at once. A message enqueued before `Start`
has been called, or after it has returned, is logged and dropped, since there is no delivery loop running
to receive it.

`FilterSent(TFrame message)` and `FilterReceived(TFrame message)` are both `protected virtual` and
synchronous — intended for simple, cheap filtering only (e.g. by tag, priority, or sender), not I/O —
defaulting to always returning `true` (allow everything). `FilterSent` runs inside `Send(object message)`,
before the abstract `Send(TFrame message)`; a filtered send is treated exactly like a failed one
(`Send(object message)` returns `false`). `FilterReceived` runs inside `Receive(TFrame message)`, before
the message is enqueued; a filtered receive is silently dropped, exactly as if `Receive` had never been
called for it.

## `ExternalSystems` and `ExternalSystemsService`

The external systems added with `ExternalSystem` (see [Configuration.md](Configuration.md#external-systems)) are the list
of external systems this instance communicates with, resolved once at startup. `ExternalSystemsService`
(`Core/src/Internal/ExternalSystems/ExternalSystemsService.cs`, an internal hosted-service-style component started
by `EngineHost` alongside the peer and interface listeners) reads this list once and then:

- Runs every external system's own `Start` loop concurrently, for the lifetime of the app.
- Subscribes to `IPeerService.FrameDelivered` — raised for every frame this instance receives (only those that are messages are relayed),
  whether from a genuine peer, or from `DeliverLocal` (used for self-addressed sends and, as below, for
  external-system-received messages) — and relays that message out through `Send` on every external
  system **except** the one it was originally received from, if any.
- Subscribes to every external system's own `MessageReceived` event. When one fires, the message is
  passed to `IPeerService.DeliverLocal`, which processes it exactly like an ordinary received message
  (stored, shown in the UI, etc. — the same path a self-addressed send already used) and, in turn, raises
  `FrameDelivered`, triggering the relay-to-other-external-systems step above.

The "except the one it was originally received from" exclusion uses an `AsyncLocal<IExternalSystem?>` to
track which external system (if any) is the source of the in-flight `DeliverLocal` call, since a plain
field would race under concurrent delivery from multiple external systems at once. A message the app
receives from a peer (not an external system) has no such source, so it is relayed to every configured
external system.

If `ExternalSystems` returns an empty list (the Engine default), `ExternalSystemsService.Start`
returns immediately without subscribing to anything.

## `ExternalServer`

`ExternalServer` (see [Configuration.md](Configuration.md#external-systems)) designates one entry of
the external systems — or none, the default — as the exclusive upstream hub for every message this instance
would otherwise send out. When it is set, `ExternalSystemsService` changes the relay step above:

- A message **not** received from `ExternalServer` (composed locally by the user and sent to a remote
  peer, received from a genuine peer connection, or received from any other configured external system) is
  sent exclusively to `ExternalServer`, bypassing every other external system entirely.
- A message received **from** `ExternalServer` is relayed to every other external system exactly as it
  would be without one configured (the normal "except the source" behavior above).

Outbound peer sends are covered too: `MessageRoutingService.Route` — the entry point for a message the
local user composes and sends — checks `ExternalServer` before dialing each remote recipient individually
over the peer network. If set, the message is sent to `ExternalServer` **once**, regardless of how many
remote recipients it is addressed to (since the external server, not this instance, is now responsible for
delivering it onward), and every one of those recipients' `UserDeliveryResult.Success` reflects that single
send's outcome. A self-addressed portion of a send is unaffected — it still delivers locally via
`IPeerService.DeliverLocal`, exactly as it would with no `ExternalServer` configured, since it never leaves
the instance in the first place.

`ExternalServer` must be one of the same instances also returned by `ExternalSystems`, not a separate
instance managed on the side — `ExternalSystemsService` still runs its connect/poll/receive lifecycle
(`Start`, `AttachLogger`, `MessageReceived`) exactly like any other configured external system; the
property only designates *which* one, if any, is treated as the exclusive hub for outbound traffic.

## Sample

`Sample` states no external system. A host implementation replaces `TryConnect`, `Disconnect`, and `Send` of
`ExternalSystemBase<TFrame>` with genuine connection logic for its own external system, and either overrides
`PollIsConnected` or calls `ReportDisconnected` (or both), depending on how its own external system reports
connection loss.
