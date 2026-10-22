# Exposed for external code

What the package deliberately makes public so that a host's own code can use it, beyond configuring and starting the engine: services to ask for, utilities to use directly, and the mocks for testing custom handlers. This is the one file that lists types: it is kept current whenever something is added to, or removed from, what is exposed, and the tests guard that nothing else becomes public by accident.

## Services

Asked for through dependency injection, in a constructor or in a `required` property, by a handler or by any class built from the container. Each is registered by the engine; a host that registers its own implementation for the interface takes precedence.

- `IUserCatalog<TProfile>` - who is on the network, each as a `UserInfo<TProfile>`: the current user, every user the network configuration lists, the ones reachable right now, and the groups (`GroupInfo`) with their members expanded. Reads the current state each time, so it follows refreshes and connections. Only in the running engine.
- `INetwork<TFrame, TPriority, TLevel, TAspect>` - what a frame handler acts with: send a frame to a directly connected user, record a received message, set delivery statuses and the network indicator, send to interfaces and external systems, keep and find stored messages, expand destinations, read auto forward targets. Typed by what the host stated in `Types`. Only in the running engine.
- `IServiceConnection<TPriority, TLevel, TAspect, TProfile>` - the host's high-level handle on the running engine: send a message, observe received messages and delivery status, mark a message read, list users. Only in the running engine.
- `ILogger` - writes to the engine's log under a category and a required event identifier (`CL` is the engine's prefix, so a host uses its own), with or without an exception, for components that are not the engine. In the configuration's container too, where it writes nothing.
- `IDebugMode` - whether the application was compiled with its own `DEBUG` constant (recorded in its assembly by the package's build targets). In the configuration's container too, so a configuration can state settings from it, such as `StackTrace(debug.IsDebug)`.
- `IDataStore` - keeps the host's own records between runs: `Save(id, record)` writes a record of any class, typically a record type, as JSON under an identifier (replacing what was there) and `Load<T>(id)` reads it back or returns `null`. One file per identifier in the current user's data folder, so users sharing a machine never share records. Only in the running engine.
- `IDataPool` - rents byte buffers from a pool, as an `IMemoryOwner<byte>` of exactly the size asked for that returns the buffer when disposed. In the configuration's container too.

## Utilities

Plain types, used directly.

- `Bit` with `BitEndianness` - `Bit.Read` and `Bit.Write` read and write one bit of a span of bytes by index across the whole span (an index may be greater than 7), numbering the bits of each byte from its least significant end (`Little`) or its most significant end (`Big`).
- `BiDictionary<TLeft, TRight>` - pairs that can be looked up from either side, where each value is in exactly one pair.
- `MemoryOwner<T>` - an `IMemoryOwner<T>` over memory someone else owns: it hands the memory out and disposing it does nothing.
- `PooledBufferWriter` - an `IBufferWriter<byte>` over a pooled array, for a serializer to write into.
- `IUserInfo`, `UserInfo<TProfile>` and `NoProfile` - everything known about a user, as the engine hands it to a host: always the `UserInfo<TProfile>` typed by the profile record the host stated in its `Types` call (`NoProfile` for none), which implements the non-generic `IUserInfo` for code that does not care about the profile.
- `LogCategories` - the categories the engine logs under and which of them are written by default, for use with `ILogger`.

## Test fakes

The `BlueHeighliner.Comlink` package also has, in the `BlueHeighliner.Comlink.Testing` namespace, a fake of every context the engine hands to a host's handlers and of every service above, so custom handlers and components can be unit tested without the engine and without any mocking library. Each is a plain class implementing the interface, so a test hands the fake itself to the code under test. A fake gets what it answers from its constructor and settable properties, accepts what the code does with it, and records that in lists a test asserts on.

- `FakeDeleteContext` for `IDeleteContext`, handed to `IDeleteHandler.CanDelete`.
- `FakeFramePacketCreateContext<TFrame>` for `IFramePacketCreateContext<TFrame>`, handed to `IPacketHandler.CreateFramePacket`.
- `FakeDraftBlockContext<,,>` for `IDraftBlockContext<,,>`, handed to `IDraftHandler.IsBlocked`, and `FakeDraftContext<,,>` for `IDraftContext<,,>`, handed to `IsAlert` and `GetHeader`.
- `FakeNetworkConnectedContext`, `FakeNetworkDisconnectedContext`, `FakeNetworkReceivedContext<TFrame>`, `FakeNetworkSentContext<,,>`, `FakeNetworkReadContext<,,>` and `FakeNetworkRetrievalContext`, for the contexts of the six methods of `IFrameHandler`.
- `FakeFrameHandshakeContext<TFrame>` and `FakePacketHandshakeContext<TPacket>`, for the handshake contexts: what a handler sends is recorded in `Sent` and accepted (`SendResult`), `Connected` and `Disconnect` are recorded and finish at once.
- `FakeImportFormatContext<,>` for `IImportFormatContext<,>`, handed to a custom import format's reader: every addition is recorded in `Messages`, `Drafts`, `Notes` and `StagedSends`.

Services:

- `FakeUserCatalog<TProfile>` for `IUserCatalog<TProfile>`: the users, groups and connections it is given, settable afterwards.
- `FakeNetwork<,,,>` for `INetwork<,,,>`: every send is accepted (`SendResult`) and every other operation finishes at once, each recorded in a list of its own (`Sent`, `SentToInterfaces`, `SentStatuses` and so on); `FoundMessages` and `AutoForwardTargets` set what it finds, and `GetDestinations` answers the distinct targets with no one left out.
- `FakeServiceConnection<,,,>` for `IServiceConnection<,,,>`: connects at once, answers `UserInfo`, `UserNames` and `ConnectedUsers`, accepts and records sends and mark read, and raises the engine's events with `RaiseMessageReceived` and `RaiseDeliveryStatusChanged`.
- `FakeLogger` for `ILogger`: records every entry as a `FakeLogEntry` in `Entries`, and refuses an entry with no identifier as the engine's does.
- `FakeDebugMode` for `IDebugMode` and `FakeDataPool` for `IDataPool`: the former says what it is told, the latter rents a new array of the size asked each time.
- `FakeDataStore` for `IDataStore`: keeps the saved instances in memory in `Items`, by identifier whatever the case, so a test can set up what a component finds and read what it saved.

```csharp
FakeNetworkReceivedContext<MyFrame> context = new(new MyFrame { Body = "hi" }, "ALICE");
FakeNetwork<MyFrame, MyPriority, MyLevel, MyAspect> network = new();
MyFrameHandler handler = new(new FakeUserCatalog<NoProfile>(new UserInfo<NoProfile> { Name = "ME" }), network);

await handler.OnReceived(context);

Assert.Single(network.Sent);
```
