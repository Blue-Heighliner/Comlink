# Exposed for external code

What the package deliberately makes public so that a host's own code can use it, beyond configuring and starting the engine: services to ask for, utilities to use directly, and the mocks for testing custom handlers. This is the one file that lists types: it is kept current whenever something is added to, or removed from, what is exposed, and the tests guard that nothing else becomes public by accident.

## Services

Asked for through dependency injection, in a constructor or in a `required` property, by a handler or by any class built from the container. Each is registered by the engine; a host that registers its own implementation for the interface takes precedence.

- `IUserCatalog` - who is on the network: the current user, every user the network configuration lists, the ones reachable right now, and the groups (`GroupInfo`) with their members expanded. Reads the current state each time, so it follows refreshes and connections. Only in the running engine.
- `INetwork<TFrame, TPriority, TLevel, TAspect>` - what a frame handler acts with: send a frame to a directly connected user, record a received message, set delivery statuses and the network indicator, send to interfaces and external systems, keep and find stored messages, expand destinations, read auto forward targets. Typed by what the host stated in `Types`. Only in the running engine.
- `IServiceConnection<TPriority, TLevel, TAspect>` - the host's high-level handle on the running engine: send a message, observe received messages and delivery status, mark a message read, list users. Only in the running engine.
- `ILogger` - writes to the engine's log under a category and a required event identifier (`CL` is the engine's prefix, so a host uses its own), with or without an exception, for components that are not the engine. In the configuration's container too, where it writes nothing.
- `IDebugMode` - whether the application was compiled with its own `DEBUG` constant (recorded in its assembly by the package's build targets). In the configuration's container too, so a configuration can state settings from it, such as `StackTrace(debug.IsDebug)`.
- `IDataPool` - rents byte buffers from a pool, as an `IMemoryOwner<byte>` of exactly the size asked for that returns the buffer when disposed. In the configuration's container too.

## Utilities

Plain types, used directly.

- `Bit` with `BitEndianness` - `Bit.Read` and `Bit.Write` read and write one bit of a span of bytes by index across the whole span (an index may be greater than 7), numbering the bits of each byte from its least significant end (`Little`) or its most significant end (`Big`).
- `BiDictionary<TLeft, TRight>` - pairs that can be looked up from either side, where each value is in exactly one pair.
- `MemoryOwner<T>` - an `IMemoryOwner<T>` over memory someone else owns: it hands the memory out and disposing it does nothing.
- `PooledBufferWriter` - an `IBufferWriter<byte>` over a pooled array, for a serializer to write into.
- `LogCategories` - the categories the engine logs under and which of them are written by default, for use with `ILogger`.

## Test mocks

The `BlueHeighliner.Comlink.Testing` package (the `Testing` project, in the `BlueHeighliner.Comlink.Testing` namespace) has a mock of every context the engine hands to a host's handlers and of every service above, so custom handlers and components can be unit tested without the engine. Each is a Moq `Mock<T>` of the context's interface (Moq 4.20.72) with the members set up from what it is constructed with, which can be changed afterwards, and with the actions a handler takes answered sensibly, so a test hands the mock's `Object` to the handler and verifies what the handler did.

- `MockDeleteContext` for `IDeleteContext`, handed to `IDeleteHandler.CanDelete`.
- `MockFramePacketCreateContext<TFrame>` for `IFramePacketCreateContext<TFrame>`, handed to `IPacketHandler.CreateFramePacket`.
- `MockDraftBlockContext<,,>` for `IDraftBlockContext<,,>`, handed to `IDraftHandler.IsBlocked`, and `MockDraftContext<,,>` for `IDraftContext<,,>`, handed to `IsAlert` and `GetHeader`.
- `MockNetworkConnectedContext`, `MockNetworkDisconnectedContext`, `MockNetworkReceivedContext<TFrame>`, `MockNetworkSentContext<,,>`, `MockNetworkReadContext<,,>` and `MockNetworkRetrievalContext`, for the contexts of the six methods of `IFrameHandler`.
- `MockFrameHandshakeContext<TFrame>` and `MockPacketHandshakeContext<TPacket>`, for the handshake contexts: `Send` reports that the other node accepted what was sent, and `Connected` and `Disconnect` finish at once.
- `MockImportFormatContext<,>` for `IImportFormatContext<,>`, handed to a custom import format's reader: every `Add` finishes at once.

Services:

- `MockUserCatalog` for `IUserCatalog`: the users, groups and connections it is given, settable afterwards.
- `MockNetwork<,,,>` for `INetwork<,,,>`: every send is accepted (`SendResult`) and every other operation finishes at once, so a test verifies what a handler did; `StoredMessages` and `AutoForwardTargets` set what it finds, and `GetDestinations` answers the distinct targets with no one left out.
- `MockServiceConnection<,,>` for `IServiceConnection<,,>`: connects at once, answers `UserInfo`, `UserNames` and `ConnectedUsers`, accepts sends and mark read, and raises the engine's events with `RaiseMessageReceived` and `RaiseDeliveryStatusChanged`.
- `MockLogger` for `ILogger`: records every entry as a `MockLogEntry` in `Entries`.
- `MockDebugMode` for `IDebugMode` and `MockDataPool` for `IDataPool`: the former says what it is told, the latter rents a new array of the size asked each time.

A test hands a mock's `Object` to the code under test and verifies what it did.

```csharp
MockNetworkReceivedContext<MyFrame> context = new(new MyFrame { Body = "hi" }, new UserInfo { Name = "ALICE" });
MockNetwork<MyFrame, MyPriority, MyLevel, MyAspect> network = new();
MyFrameHandler handler = new(new MockUserCatalog(new UserInfo { Name = "ME" }).Object, network.Object);

await handler.OnReceived(context.Object);

network.Verify(service => service.ReceiveMessage(It.IsAny<Message<MyPriority, MyLevel, MyAspect>>()), Times.Once);
```
