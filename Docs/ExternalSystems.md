# External Systems

An **external system** is a conduit between this instance and one system outside Comlink — a socket, a
message queue, an HTTP long-poll, or any other integration point a host wants to bridge into the
messaging flow. Unlike a peer or an [interface connection](Interface.md), an external system is not
another Comlink instance and does not speak OFT; it is entirely defined by the host's own
`ExternalSystemBase<TMessage>` subclass.

## Interfaces

`Engine/src/ExternalSystems/ExternalSystem.cs` defines two types:

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

public abstract class ExternalSystemBase<TMessage> : IExternalSystem where TMessage : class
{
    protected abstract Task<bool> TryConnect(CancellationToken cancellation);
    protected virtual Task<bool> PollIsConnected(CancellationToken cancellation);
    protected abstract Task Disconnect();
    protected abstract Task<bool> Send(TMessage message);
    protected Task Receive(TMessage message);
    protected void ReportDisconnected();
}
```

`IExternalSystem` is deliberately not generic over the message type — it is declared `object`-typed on
`Send`/`MessageReceived` so `ExternalSystemsService` (below) can hold and drive every configured external
system uniformly, the same reasoning as `IEngineController`'s message-format members (see
[Control.md](Control.md#message-format)). A host never implements `IExternalSystem` directly; it always
subclasses `ExternalSystemBase<TMessage>`, which implements `IExternalSystem` on your behalf and exposes
only type-safe `TMessage`-typed members — `protected abstract` methods for the real connection behavior
(plus one `protected virtual` method, `PollIsConnected` — see [Lifecycle](#lifecycle) below), and
`protected Task Receive(TMessage message)` to report an inbound message. `TMessage` should match the
host's own `IEngineController.MessageType`.

`ExternalSystemBase<TMessage>`'s constructor deliberately does not take an `ILoggerFactory` — each
external system is constructed directly by `IEngineController.GetExternalSystems()`, not resolved through
DI, and that member lives on the same type backing `IEngineController` itself; taking `ILoggerFactory`
there would create a circular dependency through the logging providers (e.g. `DailyFileLoggerProvider`)
that themselves depend on `IEngineController` for their log file location. Instead, `AttachLogger` is
called once by `ExternalSystemsService`, using its own (safely resolved, since it is an ordinary singleton
rather than part of `IEngineController`'s own construction) `ILoggerFactory`, before `Start` — an external
system logs to a no-op logger for any activity before that point.

## Lifecycle

`ExternalSystemBase<TMessage>.Start(CancellationToken)` runs a loop for as long as `cancellation` is not
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

`Send(object message)` returns `false` immediately without calling the abstract `Send(TMessage message)`
while not connected; while connected, it casts to `TMessage` and calls it, catching and logging any
exception as a failed send (returning `false`) rather than propagating it.

`Receive(TMessage message)` is called by the implementor (e.g. from its own background read loop,
socket callback, or poll) whenever the external system delivers a new message. It only enqueues the
message onto an internal, per-instance channel and returns — it does not wait for the message to actually
reach `MessageReceived`, so it is safe to call concurrently, or without awaiting a previous call first, if
the implementor's own connection can genuinely deliver messages that way (e.g. parallel socket reads). A
single internal loop, running for the lifetime of `Start`, drains that channel and delivers each message to
`MessageReceived` one at a time, in enqueue order — so `ExternalSystemsService` (and any other subscriber)
never sees two deliveries overlap, and messages are always processed in the order they were enqueued,
regardless of how many `Receive` calls were in flight at once. A message enqueued before `Start` has
been called, or after it has returned, is logged and dropped, since there is no delivery loop running to
receive it.

## `GetExternalSystems` and `ExternalSystemsService`

`IEngineController.GetExternalSystems()` (see [Control.md](Control.md#external-systems)) returns the list
of external systems this instance communicates with, resolved once at startup. `ExternalSystemsService`
(`Engine/src/ExternalSystems/ExternalSystemsService.cs`, an internal hosted-service-style component started
by `EngineHost` alongside the peer and interface listeners) reads this list once and then:

- Runs every external system's own `Start` loop concurrently, for the lifetime of the app.
- Subscribes to `IPeerService.MessageDelivered` — raised for every message this instance receives,
  whether from a genuine peer, or from `DeliverLocal` (used for self-addressed sends and, as below, for
  external-system-received messages) — and relays that message out through `Send` on every external
  system **except** the one it was originally received from, if any.
- Subscribes to every external system's own `MessageReceived` event. When one fires, the message is
  passed to `IPeerService.DeliverLocal`, which processes it exactly like an ordinary received message
  (stored, shown in the UI, etc. — the same path a self-addressed send already used) and, in turn, raises
  `MessageDelivered`, triggering the relay-to-other-external-systems step above.

The "except the one it was originally received from" exclusion uses an `AsyncLocal<IExternalSystem?>` to
track which external system (if any) is the source of the in-flight `DeliverLocal` call, since a plain
field would race under concurrent delivery from multiple external systems at once. A message the app
receives from a peer (not an external system) has no such source, so it is relayed to every configured
external system.

If `GetExternalSystems()` returns an empty list (the Engine default), `ExternalSystemsService.Start`
returns immediately without subscribing to anything.

## Sample

`Sample/src/SampleExternalSystem.cs` provides `SampleExternalSystem`, a self-contained demo — not a real
network integration — that "connects" after a short delay, stays connected indefinitely, and periodically
synthesizes an inbound demo message, so the receive path (mirroring to every other external system, and
normal processing as a received message) is visible without needing an actual external system to connect
to. It never loses its simulated connection, so it leaves `PollIsConnected` at its default rather than
overriding it. `SampleEngineController.GetExternalSystems()` returns a single instance of it. A real host
implementation replaces `TryConnect`, `Disconnect`, and `Send` with genuine connection logic for its own
external system, and either overrides `PollIsConnected` or calls `ReportDisconnected` (or both), depending
on how its own external system reports connection loss.
