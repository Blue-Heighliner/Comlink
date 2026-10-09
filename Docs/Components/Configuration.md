# Engine Configuration

A host tells the engine how to run by implementing `IEngineConfiguration` and naming it to `Engine.Start<T>`, which constructs it through dependency injection. The engine calls `Configure` once, before anything else starts, handing it an `IEngineBuilder`; the first call must be `Types`, which fixes the frame, packet, priority and message level types, and every call after it is optional except `Frames<THandler>()`, which states the frame handler and starts stating the heartbeat and other frame settings (through `IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>`), and each returns a builder so a configuration reads as one fluent expression. `Packets<THandler>(maxPayloadSize)`, `Priority(x)`, `Level(x)`, `Aspect(x)` and `AddressType(x)` likewise start sub-configurations (`IPacketBuilder`, `IPriorityLevelBuilder`, `IMessageLevelBuilder`, `IMessageAspectBuilder`, `IAddressTypeBuilder`), but no lambda is involved: each sub-configuration builder is also an `IEngineBuilder`, so its own calls (`Mode(...)`, `Color(...)`, `Label(...)`) and every ordinary setting follow on the same chain, in any order, and a sub-configuration may be started again later to add to it. They are one object internally, collected as the host states things and completed when `Configure` returns. The builder is the only public way to change what the engine does; everything it collects is read internally through `IEngineController`, which is not part of the public surface. See each area below for what it covers.

## Concept

Engine never reads environment variables, hardcodes paths, or calls host-specific APIs directly. Instead, every piece of external configuration and rule-based behaviour is either a call on `IEngineBuilder` or part of the network configuration file, which describes the users of the network. A host states only what differs from the engine's defaults:

```csharp
public sealed class MyEngineConfiguration : IEngineConfiguration
{
    public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect>()
        .Display<MyDisplayHandler>()
        .Frames<MyFrameHandler>()
            // ...the frames...
        ;
}

await Engine.Start<MyEngineConfiguration>(args);
```

`IEngineBuilder` has only `Types`, which fixes the frame, packet, priority, message level and message aspect types and returns the `IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>` that carries every other setting, so a handler states its priority as a member of the host's own priority enum, a draft handler states its message level as a member of the host's message level enum, and the priority and message level sub-configurations take members of those enums, all checked by the compiler. `Types<TFrame, TPriority, TLevel, TAspect>()` is for a configuration without packets and states `NoPacket` as the packet type, and a configuration without priorities or message levels states `NoPriority` (one level, `NORMAL`) or `NoMessageLevel` (no members, which turns message levels off). The builders (internal) implement these by recording what they are told; nothing is interpreted while configuring. `EngineController` (internal) reads the recorded state through `IEngineController` and supplies the default for every setting the host left alone, which is what every service, ViewModel and repository in the engine depends on. Keeping the recording separate from the reading means a configuration can be checked as a whole (`Engine.Start` throws an `InvalidOperationException` naming missing types or an unstated handler before any service starts), and that tests can replace a single behavior of the controller.

**A configuration describes non-config-file behavior only. It must never read `NetworkConfig` itself, and it must never read an environment variable.** Everything about the network's users, and the settings of the node a user runs, comes from the network configuration file, applied by the engine itself: the users' info is read by `EngineController`, and the node settings (identity certificate file, alert, tag and print settings) are applied as a decorator layered on top of it (see [Network Configuration File](#network-configuration-file) below). This split keeps "what does this app do out of the box" (the configuration) and "who is on this network and how do they run" (the file) as two independent, separately testable concerns, and means a host is never tempted to reimplement file parsing just to add one small piece of non-file behavior.

**Dependency injection:** `Engine.Start<T>` builds a bootstrap container holding logging (`ILoggerFactory`, `ILogger<T>`) plus whatever the host's `configureServices` argument registers, and constructs `T` from it, so a configuration's constructor can take services. The bootstrap container is deliberately separate from the running engine's: the engine's own logging providers need the engine's configuration (for the log file location), so a configuration built from the running container could not take a logger without a cycle, and it has to be built before the container exists anyway, because it decides whether `--config` is read and so what the container is built from. The same `configureServices` registrations are applied again to the running engine's container, which is also where a host registers anything else it wants running alongside the engine (for example a hosted service that uses `IServiceConnection`); a service registered there therefore exists once in each container. The bootstrap container lives until the engine exits, since a configuration may have handed the builder functions that use what was injected.

## Network Configuration File

The engine defines the schema of one network configuration file, shared by every node of a network (see [Config.md](Config.md) for every field). It holds the info for all users of the network, who is in which group, and the trusted certificate authority, so no user, port or connection is stated in code. It is read from the path given by the `--config` command-line argument, otherwise from `Config.json` in the current working directory; a missing default file is an empty network, while a `--config` path that does not exist is an error. The file can be read again while the application runs (right-click the user name in the title bar and choose "Refresh"): connections no longer defined are brought down and newly defined ones opened while unchanged connections are left alone, and the rest is applied as it is next read. The `--user` argument names the user the process runs as, which lets a node skip the install screen.

`EngineController` reads the file for everything about users (`GetUserInfo`, `Users`, `UserGroups`, and so on, see [User Info](#user-info)). `EngineExtensions.UseEngine` registers `IEngineController` as a `ConfiguredEngineController` wrapping it, which takes the loaded `NetworkConfig` and `ICurrentUserProvider` and applies the node settings of the current user's entry: member by member, the entry's value when it is set and the wrapped controller's value otherwise; every other member delegates straight to the wrapped controller. It is registered explicitly, never by convention scanning. The current user is the one named by `--user`, or else the installed user, except for the headless choice, which only the `--user` user decides since an installed user is not known until networking starts. The user also decides the data folder (`%APPDATA%/{AppName}/{USERNAME}`), which the decorator points at the `--user` user even before the install state has been read.

**Bootstrap ordering:** whether the command-line arguments may override the file and the user is itself a setting (`CommandLineOverrides`), so `Engine.Start` constructs the configuration and builds the `EngineBuilder` first, reads its `AreCommandLineOverridesAllowed`, and only then loads `NetworkConfig`, passing the arguments only if allowed. There is no field in the file for it (that would be circular), and a configuration cannot depend on the file for the same reason. When overrides are not allowed, `--config` and `--user` are ignored entirely, as if the arguments had never been passed, and only `Config.json` in the working directory is read.

## Settings

### Frame Format

```csharp
engine.Types<MyFrame, MyPacket, MyPriority, MyMessageLevel, MyMessageAspect>()
    .AutoForwarder("Escalation")
    .Frames<MyFrameHandler>()
        .Heartbeat<MyFrameHeartbeatHandler>()
    .Packets<MyPacketHandler>(16 * 1024)
        .Heartbeat<MyPacketHeartbeatHandler>()
        .Window(1);
```

A **frame** is the data format of all network traffic other than packets: every heartbeat (when the host states one), receipt, retrieval request and user message, and any other frame the host's frame handler exchanges, is an instance of the host's one frame type. The engine neither looks inside a frame nor knows what a frame means: what a frame is, who it is for, how it is routed, receipted or stored is the frame handler's (see [Frame Handler](#frame-handler)). A **message** is what the user sees: the `Message<TPriority, TLevel, TAspect>` record, typed by the host's own enums (`Id`, `FromUser`, `Body`, `Addresses`, `SentAt`, `Priority`, `Tag`, `MessageLevel`, `MessageAspect`, `IsAlert`) is how the engine and the handler exchange it. The engine stores and shows `Message` records (as `MessageData` in the Inbox and Outbox) and the handler converts between them and its frames. The only optional frame kind the engine itself knows is the heartbeat: `Heartbeat<THandler>` states an `IFrameHeartbeatHandler<TFrame, TPriority>` that recognizes and creates the empty frame a node sends over each MSMT connection to verify it, names its priority, and states how long a connection waits between heartbeats (`Interval`) and, while they fail, before the next try (`RetryInterval`); heartbeats are never sent over HDLC, and when no handler is stated none are sent at all and an MSMT connection counts as up once it is established. With packetization on, a heartbeat may instead be stated on the packet configuration (`Heartbeat<THandler>` on the packet configuration, an `IPacketHeartbeatHandler<TPacket, TPriority>` with the same members, over the packet type): it is then sent as a packet of its own, beneath packetization, so it is never split or reassembled and the receiving assembler discards it, and it takes precedence over a frame heartbeat. `AutoForward<TController>` adds an auto forwarder (see [Auto Forward Controllers](#auto-forward-controllers)).

`Types` supplies the concrete frame type used throughout the engine, on the wire (peer and interface connections). `Frames<THandler>` states the handler and starts the frame settings such as the heartbeat; `AutoForwarder` is stated on the engine builder. The frame type needs no field mapping: the engine never reads a field of it. The type must satisfy whatever serializer is used for the wire, which by default is a `ProtobufSerializer` that builds only the frame type (so `[ProtoContract]`/`[ProtoMember]` attributes). `Serializer<TSerializer>` on the frame builder replaces it with a type implementing `IFrameSerializer` (derive from `FrameSerializer<TFrame, TPacket>` to work with the frame and packet types rather than `object`), instantiated through the running engine's dependency injection container, as long as every node this instance talks to (including its own interface connections) uses a matching one: Comlink never negotiates or advertises which format a payload used, so a mismatch deserializes garbage or throws rather than failing cleanly. A serializer can write into a `PooledBufferWriter` (an `IBufferWriter<byte>`, which a `Utf8JsonWriter` accepts) and return its `ToOwner()` to keep the buffers it hands back pooled rather than allocated per frame; `JsonSerializer` does this. A frame or packet type may implement `IDisposable` when it holds values that need disposal: the engine disposes the instances it creates or deserializes and never exposes to the host, namely a heartbeat, the packets it makes to carry a frame (once serialized), the packets that carried a received frame (once the frame has been deserialized and handed on) and frames it drops. Any frame or packet instance the host sees, such as the `Frame` of a handler's `OnReceived`, an item given to an initial handler or anything the host creates and sends itself, is never disposed by the engine and is the host's to dispose. `IFrameSerializer.Deserialize` is given the bytes and the first packet that carried the frame across (`null` when packetization is disabled, or the frame arrived over an interface connection), so a serializer can read what the host's own packet fields say about the frame; a custom serializer must make its format self-describing enough to rebuild the right type itself (the default wraps every payload in an outer envelope naming the type). Both serializers' `Deserialize` return a value or throw `InvalidDataException` for bytes they cannot or will not build (such as a type the sender names that is not this engine's own); the engine treats a throw as a rejected frame or packet. `Create` replaces `new TFrame()` for building an empty frame.

Packetization is off unless `Packets<THandler>(maxPayloadSize)` is called, which needs a packet type stated in `Types` (calling it with `NoPacket` throws). With it, payloads are broken into prioritized packets of the host's packet type and reassembled on the other side. The host only states how its packet carries a piece of a frame, with a packet handler (the type argument of `Packets<THandler>`, instantiated through dependency injection like the other handlers, implementing `IPacketHandler<TFrame, TPacket>`): `CreateFramePacket` takes a `FramePacketCreateContext<TFrame>` (the frame the packet is being created from, always set, the packet index, packet count, frame length and the payload slice, which is only valid during the call so a packet that stores it must copy it) and returns a packet; `IsFramePacket` says whether a given packet carries a piece of a frame, as opposed to one that carries no frame, such as a packet a handshake handler sends, and the engine refuses to reassemble a packet that does not; and getters read the same aspects back. The frame id (`GetFrameId`, a string) is the handler's to generate: every packet of one frame must report the same one and two frames in flight over a connection must not share one, so a handler typically derives it from the frame it is given or remembers it while a frame's packets are created; the engine rejects a frame whose packets disagree. The handler is required; all splitting, reassembly and priority scheduling is the engine's, so a host gets its own packet format and serialization without writing any packetization logic. The packet serializer, a type implementing `IPacketSerializer` (derive from `PacketSerializer<TFrame, TPacket>`) stated with `Serializer<TSerializer>` on the packet builder and instantiated the same way, defaults to a `ProtobufSerializer` that wraps every packet in an envelope naming its type, a fixed overhead per packet that a leaner custom serializer avoids. `IFrameSerializer.ConfigurePacket(frame, packet)` is called on every outgoing frame packet, once per packet in order and before the packet is serialized, so the frame serializer can set the host's own packet properties from the frame being packetized. `IPacketSerializer.Serialize` is also given the packet and the original frame being packetized (`null` for a packet that carries no frame, such as one an initial packet handler sends), so a packet's encoding can depend on its frame. The maximum payload size (the argument of `Packets`, at least 1) is the largest slice of a serialized frame a packet carries, in bytes. It limits the payload only: the packet's own fields come on top of it. `Window` (on the packet configuration, default 1) is how many packets may be in flight over one connection at once, and must be at least 1. The engine applies no other limit: a packet larger than a connection can carry (the HDLC `MaxInfoField`, say) fails to send, with the reason logged, so on a serial network the payload size must leave room within `MaxInfoField` for a packet's own fields. Every node must be configured alike, since neither side can tell whether the other packetizes. Interface connections are never packetized.

Internally the configuration becomes a `FrameMap` (type, serializer, create, heartbeat) and a `PacketMap`, whose accessors and handlers take the frame or packet as an `object`, since that is the boundary every other layer (LiteDB storage, MSMT wire serialization) operates at. A packet member of an engine that never called `Packets` throws `NotSupportedException`, because nothing calls them.

**Default:** none for the frame, since the engine has no frame DTO of its own; no packetization.

**Network file:** none; the file has no field for any frame or packet member.

**Sample:** `EngineConfiguration` states `Frame`, a DTO with its own field names (`Id`, `Sender`, `Title`, `Text`, `Recipients`, ...) that its `FrameHandler` converts to and from `Message`, and turns packetization on with `Packet`, with a maximum payload size of 512 bytes so a packet stays within its HDLC `MaxInfoField` of 1024, and the default window.

---

### App Settings

This app's own identity and top-level presentation are the display handler's `AppName`, `Version`, `IsKiosk` and `Icon` (see [Display Names](#display-names)): the version is shown in the title bar and the info popup; kiosk mode hides the minimize and maximize buttons and has the close button ask "restart" instead of "exit"; the icon is an `avares://` URI of an Avalonia asset, or else the path of an image file.

**Default:** the name comes from the entry assembly name; the version is the entry assembly's `major.minor.build` version (`1.0.0` if it has none); kiosk mode is off; the icon is the operating system's.

The data folder is not configurable: a user's persistent state (LiteDB database, logs) is always written to `%APPDATA%\{AppName}\{USERNAME}` (`IEngineController.AppDataPath`), so users sharing a machine never share data. The one thing outside it is the file remembering which user is installed, `%APPDATA%\{AppName}\User.json` (`IEngineController.UserFilePath`), which sits beside the user folders since it is what says whose folder to use; logged lines from before a user is installed or named go to `%APPDATA%\{AppName}\Logs`.

**Network file:** none; the file has no field for any of these, and none for the data folder.

**Sample:** `EngineConfiguration` states nothing here beyond what its display handler states; everything else uses the default, including the data folder, which is always the user's own.

---

### User Identity

Nothing about this instance's own identity is stated in code. The user installs by name on the install screen (`UserService.Install`): the name must be a user of the network file (case-insensitive), and the user's certificate must be in order, that is the file `{USERNAME}.pfx` in the network's `CertificateStore` exists, its certificate has the user name as its common name, and it is signed by the `AuthorityCertificate`, the only authority trusted. A name that is not a user installs nothing, and a certificate that is not in order fails the install with the reason; either way nothing is persisted. The installed user is remembered in `User.json` in the app data folder, which holds nothing else, so deleting it only uninstalls the user. On every startup `UserService.Load` makes the same checks for the remembered user, and when they fail it logs why, deletes `User.json` and installs nobody, so the install screen is shown. Everything else about the user comes from [User Info](#user-info).

**Network file:** the `--user` argument, when the host allows command-line overrides, bypasses the install screen and `User.json` and runs as that user, after the same checks as an install (the name is a user of the network and the certificate is in order); when they fail nobody is installed and the install screen is shown, and `User.json` is left alone. See [Config.md](Config.md).

---

### User Directory

The addressable users and groups, stated only in the network file: the names used for the destination auto-complete in the draft editor and for [connection identification](Identification.md), and group membership for address expansion (members may be user names or other group names, enabling nested hierarchies). A name is only a name here; what is known about each user, including how a node run by that user connects, is stated through [User Info](#user-info). By convention user names are all uppercase.

When a message is sent to a group, the Engine records which addressed groups each user was reached through. The sent message view shows this context, e.g. `USER-A (OPS)`, so the operator can see which group membership drove delivery.

**Default:** no known users or groups.

**Network file:** the file's users and `UserGroups` are the directory (group names are listed among the users too). A user's info lists the groups it is a member of.

---

### User Info

```json
"Users": {
  "SERVER1": { "Role": "Server", "IpHost": "10.0.0.1", "Msmt": { "Port": 50221 }, "InterfacePort": 50220,
    "Parent": "SERVER2", "Children": [ "CLIENT1" ],
    "MessageLevel": "RESTRICTED" }
}
```

Everything about one user is stated on that user's entry in the [network configuration file](#network-configuration-file), which the engine turns into a `UserInfo` and hands out by name (`IEngineController.GetUserInfo`); a user the file does not list is just a name. There is no per-aspect engine method for a user's role, ports, connections, message level and so on, and no user info in code. The fields:

| Field | Meaning | Default |
|-------|---------|---------|
| `Role` | The [networking role](Peer.md#user-roles) of a node this user runs: `Client` or `Server` | `Client` |
| `IpHost` | The IP address or host name other nodes use to reach the node over IP | none |
| `Msmt` | MSMT options by name, and `Port`, the port the node listens on | the MSMT defaults, port `50021` |
| `Hdlc` | HDLC options by name, `Address`, the station address, and `Ports`, the MicroGate ports to open (or `*` for all) | the HDLC defaults, address `1`, no ports |
| `InterfacePort` | Loopback TCP port of the local interface listener, always active in every role (see [Interface.md](Interface.md)) | `50020` |
| `Parent` | The user above this one in a hierarchy, by name or as an object forcing the connection mode (`MsmtListen`, `MsmtConnect` or `Hdlc`); by default the node dials it | none |
| `Children` | The users below this one, for a `Server`, each by name or as an object forcing the connection mode; by default the node listens for them | none |
| `MessageLevel` | The name of the level the user runs at (see [Message Levels](#message-levels)) | the lowest configured level |
| `Data` | App-specific string keys and values; the engine does not interpret them, they travel with the user's `UserIdentity` | none |

The current user's info is what decides how this node behaves, so it is read once a user is installed (or named by `--user`), not when the engine starts: until then the node is a `Client` with the default ports and nothing connected, showing only the install screen. The topology a `Server` routes with, every server in the cluster and the children each owns, is every user in the [directory](#user-directory) whose role is `Server`, with its `Children`. Every node on a network uses the same file, since a node learns about other users, such as which servers store messages, from it.

**Default:** a user with nothing stated is a `Client` on the default ports that connects nowhere.

**Network file:** this is the file; each field above has the same name in a user's entry (see [Config.md](Config.md)), and the entry also carries the settings of the node that user runs.

**Sample:** each scenario under `Scripts/Scenarios/` (`ClientServer`, `ClientServerSerial`, `ServerCluster`) has its own `Config.json` describing its whole network: role, IP host, parent and children, message level, storage, and certificate file for each of its users. Each scenario script passes it with `--config` and names its user with `--user`.

---

### Connection Identification

```csharp
engine
    .Frames<MyFrameHandler>().Handshake<MyFrameHandshake>()
    .Packets<MyPacketHandler>(16 * 1024).Handshake<MyPacketHandshake>();
```

Who is on the other end of a connection, decided as the connection forms. All traffic between nodes is a serialized instance of the configured frame type, or of the packet type when packets are configured, and nothing else, so the introduction is too: a handshake handler gets `OnConnected` on both nodes when a connection forms, `OnReceived` on either node for each item the other sent, every time with a controller that can send an item, mark the connection fully connected as a user name, or disconnect it. Two kinds exist and either, both or neither may be stated: an `IPacketHandshakeHandler<TPacket>` (stated with `Handshake` on the packet configuration), whose items are packets sent as they are beneath the packetizer, and an `IFrameHandshakeHandler<TFrame>` (stated with `Handshake` on the frame configuration), whose items are frames, split into packets like any frame, above the packetizer and after the packet handshake. The name the handler marks the connection connected as wins; otherwise the engine names the user from what it knows of the connection (an `IIpConnectionInfo` for IP: the remote host, port and certificate names; an `ISerialConnectionInfo` for serial: the port and addresses; and this node's own `LocalUser`, which a handler can read). The handshake, where it sits in the transport stack, and what the engine does by default are described in [Identification.md](Identification.md). Every node on a network must be configured alike, as with packetization. Handlers are stated by type and instantiated through the running engine's dependency injection container (the instance the host registered for the type, or else one constructed from the host's services), once and on first use, so a handler's constructor can take services.

**Default:** the engine identifies an IP connection by its certificate name and a serial connection by its port name, and no handshake handler is stated, so no handshake takes place.

**Network file:** none, because these are behavior, not settings.

**Sample:** `EngineConfiguration` states a `HandshakeHandler`: the handler decides that the node that dialed (or, on a serial cable, the one at the higher station address) sends a `Packet` whose chunk is its user name (`IConnectionInfo.LocalUser`), the other node answers with one carrying its own, and each marks the connection connected as the name it received, so its connections, IP and serial, are identified by the packet instead of by certificate name or port.

---

### Display Names

```csharp
engine.Display<MyDisplayHandler>();

public sealed class MyDisplayHandler : IDisplayHandler
{
    public string? AppName => "MyApp";
    public string? HomeText => "Select a folder and entry to get started.";
    public string? TagLabel => "Category";
    public string? InboxLabel => "Received";
}
```

The names and words the app shows its users, stated by a handler implementing `IDisplayHandler` (instantiated through dependency injection like the other handlers, but from the bootstrap container, which exists before the engine and holds the logging and the services the host registered, so the handler can log and can use the host's services but nothing the engine itself registers; creating it there is why the log location and the controller never wait on each other). Every member has a default of `null`, which keeps the engine's own text, so a handler overrides only what it wants: `AppName` (the title bar and log headers), `DataFolderName` (the folder under the application data root holding the app's data and logs: a member of its own so renaming the app can never move, and so appear to lose, its data; changing it after data has been stored starts from an empty folder), `Version` (shown in the title bar and the info popup), `MessageAspectLabel` and `MessageAspectPluralLabel` (`Message Aspect`/`Message Aspects`; what the draft view's picker and the interface's own text call a message aspect), `UserLabel` and `UserPluralLabel` (`User`/`Users`; the word for a user of the network wherever the interface says it, such as the install screen and the help), `NetworkOnlineLabel`/`NetworkOfflineLabel` (`ONLINE`/`OFFLINE`) and `NetworkOnlineColor`/`NetworkOfflineColor` (hex colors, green `#2E7D32`/orange `#D35400`), how the network indicator in the top bar of a client looks in each state, `SeparateAlerts` (`false` by default; when `true` the client has an alert inbox and a normal inbox, and an alert outbox and a normal outbox, and no alert filter in either list; when `false` alerts are mixed in and inbox and outbox lists can be filtered to alerts only; a draft list never has an alert filter), `IsKiosk` (`false` by default; kiosk mode hides the minimize and maximize buttons and has the close button restart), `Icon` (the window icon: an `avares://` URI of an Avalonia asset, or else the path of an image file), `HomeText` (the content area when no entry is selected), `AlertLabel` (the title bar's alarm box and the draft editor's alert checkbox, so both surfaces always show the same word for "alert"), `TagLabel`, `PriorityLabel` and `MessageLevelLabel` (the concepts of tags, priorities and message levels wherever the user interface names them), each with a plural member (`TagPluralLabel` and so on) for text that names several, which defaults to the singular with an `s` added when only the singular is stated, and one member for each of the engine's other names for concepts: `InboxLabel`, `OutboxLabel`, `DraftsLabel`, `DraftLabel`, `NotesLabel`, `NoteLabel`, `ActivityLabel`, `MessagesLabel` and `MessageLabel`. These reach the whole user interface: every fixed piece of text in the GUI (the title bar, folders, editors, filters, dialogs, status messages and the help) is written with the engine's own names, and what is shown is that text with each concept name replaced by what the host calls it, in the case style of what it replaces (`NEW DRAFT` becomes `NEW MEMO`, `Drafts` becomes `Memos`). The alert, tag, priority and message level names are replaced by their own labels in the same way, so an `Alert only` filter reads `Flag only` once the alert label is `Flag`. XAML does this with the `{vm:Display '...'}` markup extension and view models with `Display(text)`. Text the host supplies itself, such as the home text, is shown as it is. An empty string counts as `null`. Everything here is presentation: a rename changes what users read, never how anything is stored or routed.

**Default:** `AppName` and `DataFolderName` are the entry assembly's name, `"HOME"`, `"ALERT"`, `"Tag"`, `"Priority"`, `"Message Level"`, and no renames.

**Network file:** none for the labels. The file names message levels by their label, unlike drafts, which store the enum value: the file describes a network and is edited and redistributed quickly, while stored data must stay readable for as long as it is kept.

**Sample:** `DisplayHandler` states the app name (`Sample`), the version (`0.1.0`), the window icon and the home text, and overrides no label. The data folder keeps its own default, the assembly name, so the app's data does not move.

---

### Drafts

```csharp
engine.Drafts<MyDraftHandler>();

public sealed class MyDraftHandler : IDraftHandler<MyPriority, MyMessageLevel, MyMessageAspect>
{
    public int? DefaultLineWidth => 60;
    public int MinLineWidth => 30;
    public int? MaxLineWidth => 80;
    public string? GetHeader(DraftState<MyPriority, MyMessageLevel> state) => state.Tag.Length > 0 ? $"CATEGORY: {state.Tag}" : null;
}
```

How drafts are composed, stated by a handler implementing `IDraftHandler<TPriority, TLevel, TAspect>` (instantiated through dependency injection like the other handlers). Every member is optional.

**Line width.** The draft editor shows the body in a monospace font. With `DefaultLineWidth` or `MaxLineWidth` stated, the draft view offers a width control, the user sets how many characters wide a line is shown, between `MinLineWidth` (1 by default, and never below the header) and `MaxLineWidth` (no maximum by default). **The width is only how the draft is shown:** the editor wraps the text, and the header above it, at that many characters, at the last space that fits or in the middle of a word that is wider than a line by itself, and no line break is ever added to the text, so changing the width never changes the draft or the message that is sent. The cut-off is shown: a dashed ruler runs down the body at the last column of a line, and the header sits in a box as wide as a line. **The width can never be less than the longest line of the header**, even when `MinLineWidth` is smaller: the minimum of the width control is raised to fit it and a narrower width is raised to match, and a header wider than `MaxLineWidth` wins over the maximum. A new draft starts at `DefaultLineWidth`, or at `MaxLineWidth` when there is no default (no limit would exceed it); without a maximum the user may also clear the width to have no limit. The width is saved with the draft (`DraftEntity.LineWidth`).

**Tags.** `EnableTags` (`true` by default) says whether messages carry a tag at all; when `false` no tag is shown in the draft editor, the message view or the entry list. The handler also says what a message tag may be, wherever one is entered: `TagCase` (`Mixed`, the default, leaves it as written; `Lower` and `Upper` force it as it is typed or pasted), `IsTagRequired` (`false` by default; when `true` a draft without a tag is not sent), `MinTagLength` (0 by default; the fewest characters a tag that is given may have), `MaxTagLength` (none by default; the tag box in the draft view is sized to fit exactly that many monospace characters and does not let more be typed), and `AllowTagSymbols`, `AllowTagNumbers` and `AllowTagSpaces` (all `true` by default; letters are always allowed). What is typed is filtered to what the rules allow, so a tag in the draft view is always a valid one except for being too short, which stops the send with a message. A tag passed to `IServiceConnection<,,>.SendMessage` that breaks the rules throws.

**Defaults.** `DefaultTag`, `DefaultPriority` and `DefaultMessageLevel` say what a new draft starts with (none by default: no tag, the lowest priority the user may choose, and the highest message level the user may use). The tag is made to fit the tag rules, and a level the user may not choose, or a message level above their own, is brought to what they may.

**Alert and identifier.** `IsAlert(state)` says whether the draft's message is an alert (default: no), from its tag, body or other aspects: there is no alert flag for the user to set. `NextId(previous)` generates the message identifier (see [Message Identifiers](#message-identifiers)).

**Header.** `GetHeader` is given the draft's current state (`DraftState`: tag, priority, message level, message aspect, body, recipients and line width) and returns the header every message sent from the draft must start with, or `null` for none. The engine asks again whenever one of those aspects changes, so the header follows the draft. A header is shown in the draft view as an uneditable segment above the editable body (wrapped for viewing like the body), is not part of the saved draft, and is put in front of the body followed by a line break when the draft is sent, so the sent message, and the copy in the Outbox, start with it.

**Default:** without a handler, no width control, the editor wraps at the window, and there is no header.

**Network file:** none.

**Sample:** `DraftHandler` starts lines at 60 characters (10 to 90) and gives each kind of message its own header: `ALERT - ACTION REQUIRED` for an alert, `URGENT - PLEASE REPLY` or `REPORT - FOR YOUR REVIEW` for drafts tagged `URGENT` or `REPORT`, and a second line, `RESTRICTED - DO NOT FORWARD`, at the restricted message level. A tag is required, starts as `NOTICE` (a new draft also starts at medium importance and the internal message level), is forced to uppercase, at most 12 characters, and holds letters and numbers only. Every other message, a plain one or one tagged `NOTICE`, has no header, so the header appears and disappears as the user changes the tag, the alert flag or the message level.

---

### Alert Settings

```csharp
engine.Alarms<MyAlarmHandler>();
```

Configuration for the alert-message feature in Client mode: how long the alarm sound plays (`IAlarmHandler.AlertDuration`) before automatically stopping (resetting whenever a new alert arrives while already alarming, and stopping early once every alert that set off the current alarm has been read). While any alert is unread an indicator shows in the top bar. Clicking it, or pressing `Space` or `Enter` (fixed; pressed while focus is not in a text input), opens the oldest unread alert, which reads it, so repeating works through them oldest first. There is no setting for composing alerts: the user does not choose whether a message is an alert. The draft handler decides from the draft (`IDraftHandler.IsAlert`, see [Drafts](#drafts)), and the draft view shows the alert mark when its draft is one. See `Docs/Components/ViewModels.md`. Actually playing the alarm sound is real platform behavior, not configuration, see [`IAlertSoundPlayer`](#ialertsoundplayer-not-configurable) below.

**Disconnects:** the alarm handler's `DisconnectDuration` (30 seconds by default) is how long the alarm sound plays after a connection drops, in every role. It is separate from the alert alarm and has its own sound. Another connection dropping while it sounds starts the time again, and it stops early once every connection that dropped during the current alarm is back up (`DisconnectAlarmService`).

**Default:** `"ALERT"` / 30 seconds; no message is an alert (`IDraftHandler.IsAlert` is `false` unless the handler says otherwise). A message the handler receives is an alert when its `Message.IsAlert` says so.

**Sample:** `DraftHandler.IsAlert` makes a message an alert when its tag is `ALERT`, so typing that tag in a draft is all it takes: the draft shows the alert mark and the draft handler's `ALERT - ACTION REQUIRED` header.

**Network file:** none; the alert label and the alarm durations are stated in code.

**Sample:** none; the default plus the network file already cover every genuinely useful case.

---

### Routing

The engine does not route. Where a frame goes is decided by the frame handler, which reads whatever its frame says and sends it with `Send(user, priority, frame)` (see [Frame Handler](#frame-handler)). The peer layer only sends to a directly connected user. **Network file:** none. **Sample:** `FrameHandler` expands group names, drops destinations whose message level is too low, and sends each frame once per hop (a client to its server, a server to the owning child or sibling server). A message addressed to the sender goes to the server like any other and comes back the same way.

---

### Message Identifiers

A message has an identifier, which the draft handler states: `NextId(previous)` generates the one for each message the user sends. `NextId` is given the identifier it generated last, which the engine keeps in the user's database between restarts (`null` the first time ever), so a sequence can continue. The identifier is how receipts, retrievals and stored copies refer to a message, so it must be unique across the whole network; a sequence therefore has to include something particular to this node. **Default (`NextId` not overridden):** a random GUID in 32 hexadecimal characters. **Network file:** none. **Sample:** `DraftHandler.NextId` makes an identifier from a random 16 character token chosen for each run and a counter that continues from the previous identifier's.

---

### Message Composition

```csharp
engine
    .Priority(MessagePriority.Receipt).Mode(PriorityMode.System)
    .Drafts<MyDraftHandler>();
```

How messages are composed and displayed: the priority levels, members of the enum `TPriority` stated in `Types`, each stated with the top-level `Priority(level)` call, lowest first: the order stated is the send priority, never the order of the enum, so later levels are sent first on every connection (see [Peer.md](Peer.md)), and a member not stated is not a level. `Priority(level)` states a level and selects it to give it a `Label` (its name, the member name in uppercase by default) and a `Mode` (the GUI offers `User` priorities to users composing a message and never offers `System` ones, which restricts only the GUI, not code). The draft handler's `IsAllowed(context, priority, level, aspect, tag)` returns (`context` is an `IEngineContext` snapshot of the engine, so a rule can depend on the current user or who is connected) whether a draft may have that combination (`level` and `aspect` are `null` when none, `tag` is empty for none). In code a level is the enum member (`Message.Priority` and the priority given to `Send` are members of the host's enum); in long-term storage (drafts and exports) it is the enum member's integer value, so reordering or relabelling the levels does not change what stored data means, and a value that is not a stated level is the lowest level. **A member's value must therefore never change or be reused, even for a level that is no longer stated**, or data stored earlier would read as another level. A message carries the priority its user chose, and the handler gives each frame it sends a priority, so receipts and requests can be ordered against messages. Whether message tags are shown anywhere in the UI and what the tag input's watermark says.

`DraftViewModel` asks the draft handler proactively rather than only at send time: the priority, message level and message aspect pickers offer only choices some allowed combination with the entered tag uses, choosing one that the other current choices block moves them to the allowed combination that keeps the most of what was chosen, and setting `Tag` to a value that blocks every combination is rejected outright (the value reverts), so a blocked combination can never actually be entered in the draft editor. `SendCommand` also re-checks before sending, as a defense-in-depth safety net. See `Docs/Components/ViewModels.md`.

**Default:** the levels must be stated (the engine refuses to start otherwise), each a `User` level named by its member name in uppercase. A priority or message level is never used unless it is stated: a message with one that is not stated is refused when the handler calls `ReceiveMessage` or the GUI sends it, and an imported one is dropped with an error logged; tags on (the draft handler's `EnableTags`) with label `"Tag"`; no draft handler, so every combination is allowed. Stating a level again selects it without changing its place. With `NoPriority` the only level is `"NORMAL"` and everything is sent at priority 0. Nothing is ever sent with a priority outside the configured levels: a priority given to `Send` is brought within them, heartbeats go at the lowest level, and the handshake that identifies a connection at the highest.

**Network file:** none; tags, priorities and blocked combinations are all stated in code.

**Sample:** `EngineConfiguration` states its `Priority` enum: three user levels (`Low`, `Medium`, `High`) and two system ones (`Retrieval`, `Receipt`, used by its handler) instead of the default's one, and a `DraftHandler` whose `IsAllowed` blocks the `"SPAM"` tag regardless of the rest, and `High` priority regardless of the rest. Unlike Sample's other settings, the blocking deliberately changes default behavior from the engine's permissive "no blocks" default, since that is the only way to usefully demonstrate that part of the configuration.

---

### Address Type Labels

```csharp
engine.AddressType(AddressType.External).Label("OUTSIDE");
```

The `AddressType(type)` call selects an address type and its `Label(...)` overrides the display label shown for it, everywhere it appears in the UI: the address type picker in the draft editor, the per-address badge next to each recipient, and the message view's section headers. Each label applies to exactly the type selected; every other type keeps its own current label (its own override, if stated, or the default). The underlying `AddressType` value itself never changes - overriding a label only changes what the user reads, not how an address is stored, routed, or read by the message handler's `GetAddresses`.

**Default:** the enum name itself (`"To"`, `"Cc"`, `"External"`).

**Network file:** none; address type labels have no field in the file.

**Sample:** `EngineConfiguration` renames `External` to `"OUTSIDE"`, matching the `Kind` vocabulary `Recipient` already uses for it (see [Frame Format](#frame-format)).

---

### Message Levels

```csharp
engine
    .Level(MessageLevel.Public).Color("#2E7D32")
        .Level(MessageLevel.Internal).Color("#1565C0")
        .Level(MessageLevel.Restricted).Color("#C62828");
```

The ordered set of message levels a message may be sent at (a member of the `TLevel` stated in `Types`, carried as `Message.MessageLevel`): members of the enum `TLevel`, each stated with `Level(level)` and named by its member name in uppercase unless `.Label(...)` says otherwise, with an optional hex color from `Color(...)` shown for it in the title bar's banner (neutral gray by default) (`MessageLevelBanner`, replacing the fixed orange "DEBUG" banner every user used to see). Order is the order stated, never the order of the enum: each level ranks higher than the one stated before it, so the last is the most senior, and a member not stated is not a level. A level is stored in drafts as the enum member's integer value, so, as for priorities, **a member's value must never change or be reused, even for a level that is no longer stated**. A user's level is the `MessageLevel` on their [user info](#user-info).

The engine does not enforce a destination's level on its own: whether a user may receive a message is the handler's rule, which it applies by getting its destinations with `GetDestinations`, which leaves out users ranked below the message's level. The draft editor's message level picker only ever offers the sending user's own level and lower, so a message can be deliberately declassified but never sent above the sender's own clearance. Turning the feature off entirely is stating `NoMessageLevel` as the level type: every message maps to an empty message level and the picker is hidden.

**Default:** none stated, which turns the feature off (as does `NoMessageLevel`); stated levels are neutral gray unless colored.

**Network file:** the level each user runs at is that user's `MessageLevel` in the file; the set of levels itself has no field.

**Sample:** `EngineConfiguration` defines three placeholder levels (`PUBLIC`, `INTERNAL`, `RESTRICTED`) and states each site's level on its user info: `CLIENT1`/`CLIENT2` run at `INTERNAL`, and the server sites (`SERVER`, `SERVER1`, `SERVER2`) at `RESTRICTED`.

---

### Message Aspects

```csharp
engine
    .Aspect(MessageAspect.Encrypted)
        .Aspect(MessageAspect.Signed).Label("SIGNED BY SENDER");
```

Additional security information a message can carry beside its message level, such as being encrypted or signed: members of the enum `TAspect` stated in `Types` (or `NoMessageAspect`, which turns the feature off), each stated with `Aspect`. A message has one aspect or none. It is carried as `Message.MessageAspect` (`null` for none), like message levels. The draft handler can state `DefaultMessageAspect` (none by default) and is told the draft's aspect in its `DraftState`. Only the members stated are aspects, offered in the order stated, each named by its member name in uppercase unless `Label` gives another name, which is how the draft view, the message view and frames refer to it. Their integer values are how aspects are stored in drafts, so a member's value must never change or be reused. A name used twice throws.

The draft view shows a picker with a blank entry for none first and then each aspect, beside the message level picker, and what is chosen is stored in the draft and sent (`IServiceConnection.SendMessage(..., messageAspect)`, which must be a configured aspect or the call throws). A message shows the aspect it carries as a badge beside its message level. What the interface calls the concept is the display handler's `MessageAspectLabel` and `MessageAspectPluralLabel`. A received message's aspect is not checked against the configured ones, and one that is not configured is shown as none. Aspects take no part in routing: no destination is blocked for lacking one.

**Default:** none stated, which turns the feature off: no picker, no badge.

**Sample:** `EngineConfiguration` states `Encrypted` and `Signed` (labelled `SIGNED BY SENDER`), which the sample frame stores in its `Protection` field.

### Log Layout

```csharp
engine.Logs<MyLogHandler>();
```

The layout of the lines of the log file. An `ILogHandler` gives the fixed width of any of the three fields of a line, category (`CategoryWidth`), user (`UserWidth`) and event ID (`IdWidth`), each an `int?` where `null` leaves the field at its natural length. Fixed text is padded on the right with hyphens and cut when longer, and a fixed ID is padded with hyphens and never cut. The rules, the line format and every event are in the logging component. Which log categories are written is not a handler setting: see the logging component. Instantiated through dependency injection like the other handlers.

**Default:** no handler, so no field is fixed and lines do not align.

**Network file:** none; the layout is stated in code.

**Sample:** `LogHandler` fixes the level to 4, the category to 8, the user to 8 and the event ID to 2, so the Sample's lines align.

---

### Print Policy

```csharp
engine.Prints<MyPrintHandler>();  // an IPrintHandler<MyPriority, MyMessageLevel, MyMessageAspect>
```

The print manager's automatic "print received" behavior: whether its toggle starts enabled (`IPrintHandler<,,>.PrintReceivedByDefault`), automatically adding every received message to the print queue from the moment the app starts (the user can still toggle it at any time), and how many times each received message is added to the print queue while it is (`0` to not print it, `1` once, `2` for two copies, and so on). Consulted once per received message via `IEntryService.MessageInserted`. The count is `IPrintHandler<,,>.GetPrintCount(Message<,,>)` (default `1`), since only received messages are printed.

**Default:** off, one copy of each.

**Network file:** none; whether "print received" starts on and the print count are stated in code.

**Sample:** `PrintHandler` states a print count that prints an alert message twice and every other received message once, demonstrating a rule that inspects the message itself; "print received" uses the default.

---

### Deletion Policy

```csharp
engine.Deletes<MyDeleteHandler>();

public sealed class MyDeleteHandler : IDeleteHandler
{
    public bool CanDelete(DeleteContext context) => context.Folder is FolderType.Drafts or FolderType.Notes;
}
```

Whether the user can delete entries and subfolders, decided per root folder, never per entry, by the `IDeleteHandler` stated with `Deletes<THandler>()` (instantiated through dependency injection like the other handlers). Its `CanDelete` is given a `DeleteContext` holding the root folder type (`FolderType.Inbox`/`Outbox`/`Drafts`/`Notes`/`Activity`) that the entry or subfolder being deleted is under. Consulted by `EntryBarViewModel.DeleteEntry` before deleting; the active folder's `RootType` is passed straight through, and when the handler returns `false`, `DeleteEntry` is a silent no-op (the entry stays in the data store and in the list) rather than throwing. `LoadFolder` also caches the result on `EntryBarViewModel.CanDeleteEntries`, which drives whether the entry list's right-click "Delete" context menu item is shown at all. Draft and note editors also show a DELETE button, gated on the same rule for `FolderType.Drafts`/`FolderType.Notes` when the editor is created. See `Docs/Components/ViewModels.md`.

**Default:** allowed for every folder type; deletion is unrestricted unless a host locks down specific folders.

**Network file:** none; a fixed, code-level rule, not a per-deployment setting.

**Sample:** `EngineConfiguration` allows deletion only in `FolderType.Drafts` and `FolderType.Notes`, protecting Inbox, Outbox, and Activity entries.

---

### Export Formats

```csharp
engine.Export<MyCsvExportFormat>();

public sealed class MyCsvExportFormat : IExportFormat
{
    public string Name => "CSV";

    public bool Accepts(FolderType type) => type is FolderType.Inbox or FolderType.Outbox;

    public async Task Export(object entry, Stream stream, CancellationToken cancellation)
    {
        if (entry is MessageExportData message)
        {
            await using StreamWriter writer = new(stream, leaveOpen: true);
            await writer.WriteLineAsync($"{message.SentAt:O},{message.FromUser},{message.Body.Split('\n')[0]}");
        }
    }
}
```

`Export<T>()` adds a custom export format, a type implementing `IExportFormat` that is instantiated through the running engine's dependency injection container (the instance the host registered for it, or else one constructed from the host's services), shown as an option in the export screen's format picker alongside the built-in JSON
format (see `Docs/Components/ViewModels.md`, `IExportViewModel`). `Export` is handed one entry - a
`MessageExportData`, `DraftExportData`, `NoteExportData`, or `ActivityLogExportData` depending on which root
folder type it came from, the exact same public DTOs the engine's own built-in JSON export writes - and a stream
to write it to; a host that only handles some entry types checks the runtime type (as above) or narrows what it
ever receives at all by overriding `Accepts`. `Accepts` also determines which entries `ExportService.Export`
leaves out of the archive entirely for this format, so an excluded entry's data is never touched, not merely
unwritten. Each entry's file inside the export zip gets an extension derived from the format's own name (lowercased,
stripped to letters and digits - `"CSV"` above becomes `.csv`), so files stay recognizable to whatever tool a host
exports for. Stating a format whose `Name` matches an earlier one (case-insensitive) replaces it; a new name adds another.

A package written with a custom format is one-way: only a package written with the built-in JSON format can be
read back in by the import screen (see `Docs/Components/Services.md`, `ImportService`) - a custom format is for
producing something a tool outside Comlink consumes, not for round-tripping through this app.

**Default:** no custom formats; the export screen offers only the built-in JSON format.

**Network file:** none; formats are behavior, not settings.

**Sample:** a `"Text"` format writing each message, draft, or note as readable plain text, restricted (via
`Accepts`) to Inbox, Outbox, Drafts, and Notes - Activity's structured entries are left to the built-in JSON
format instead.

---

### Import Formats

```csharp
engine.Import<MyCsvImportFormat>();

public sealed class MyCsvImportFormat : IImportFormat<MyPriority, MyMessageLevel>
{
    public string Name => "CSV";

    public TimeSpan? StagedSendDelay => TimeSpan.FromSeconds(1);

    public async Task Import(Stream stream, IImportFormatContext<MyPriority, MyMessageLevel> context, CancellationToken cancellation)
    {
        using StreamReader reader = new(stream, leaveOpen: true);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellation)) is not null)
        {
            string[] parts = line.Split(',', 3);
            if (parts.Length < 3) { continue; }
            context.AddStagedSend(new StagedSendData<MyPriority, MyMessageLevel> { Body = parts[1], Addresses = [new AddressRequest { UserName = parts[0] }] });
        }
    }
}
```

`Import<T>()` adds a custom import format, a type implementing `IImportFormat<TPriority, TLevel>` that is instantiated through the running engine's dependency injection container, shown as an option in the import screen's format picker alongside the built-in
package format (see `Docs/Components/ViewModels.md`, `IImportViewModel`). Selecting it changes which files the
screen finds on the source drive - not `IExportService.PackageExtension` packages, but files whose extension
matches this format's own name-derived extension (the same derivation an `ExportFormat` entry's file extension
uses - `"CSV"` above becomes `.csv`). Choosing one of those files and importing it opens it as a plain stream and
hands `Import` the stream plus an `IImportFormatContext<TPriority, TLevel>`, unlike the built-in format's zip archive of typed entries.

The context turns whatever the reader finds into real changes:

- `AddMessage(MessageExportData)`, `AddDraft(DraftExportData)`, `AddNote(NoteExportData)` - insert a new entry
  using the exact same public DTOs a custom export format's serializer receives (see above), applying the same
  rules the built-in package format already applies to its own entries: a message matching an existing one (same
  ID, direction, and date) is skipped, and a draft/note matching an existing entry's name prompts the user through
  the same Keep Existing / Overwrite / Overwrite All dialog - a reader only builds the DTO, never reimplements
  matching or conflict prompting.
- `AddStagedSend(StagedSendData<TPriority, TLevel>)` - adds a prepared message (`Body`, `Addresses`, and the same
  `Priority`/`Tag`/`MessageLevel` fields a send normally carries; whether it is an alert is decided by the draft handler) to the staged send screen instead of
  writing anything to the database directly; nothing is sent until the user reviews the batch there and presses
  its own send button.

`StagedSendMode` and `StagedSendDelay` (members of the format, defaulting to sequential with no pause) state how that later send-all processes everything this format ever adds
through `AddStagedSend`: `StagedSendMode.Sequential` (the default) sends one at a time, in the order added,
pausing `StagedSendDelay` between each when it is stated; `StagedSendMode.Simultaneous` sends every one at once.
Stating a format whose `Name` matches an earlier one (case-insensitive) replaces that format; a new name adds another.

**Default:** no custom formats; the import screen offers only the built-in package format.

**Network file:** none; formats are behavior, not settings.

**Sample:** a `"CSV"` format reading `User,Body` lines and staging one send per line, sent one at a time
a second apart (`StagedSendMode.Sequential`, a one second `StagedSendDelay`).

---

### Auto Forwarders

```csharp
engine.AutoForwarder("Escalation");
```

Adds an auto forwarder with a name, shown as an option in the auto forward screen to every user whose `AutoForwarders` in the network file lists that name. Each of them can open it there and maintain their own locally-saved target list, added to and removed from freely, persisted between restarts (see `Docs/Components/ViewModels.md`, `IAutoForwardViewModel`). An auto forwarder is only a name and a target list: the engine never forwards anything itself. The frame handler reads the list with `GetAutoForwardTargets(name)` and decides which received messages to forward and how (the current user is never in the list it returns, so a forward cannot loop back to them). Stating a name twice adds it once.

**Default:** no auto forwarders; the auto forward screen's title bar button is hidden entirely for every user, since no one has access to anything.

**Network file:** the names a user has access to are their `AutoForwarders`; the auto forwarders themselves are stated in code. A target list, once a user sets it up, is per-installation local data (see `Docs/Components/Data.md`, `AutoForwardTargetsEntity`), not config file state.

**Sample:** an `"Escalation"` auto forwarder, listed in the `AutoForwarders` of the clients of every scenario. `FrameHandler` forwards any received alert or `URGENT`-tagged message on as the original frame, with the forwarding user as its sender, addressed to all of its targets at once.

---

### Server Storage

The engine keeps a store of messages for the frame handler (`StoreMessage` and `FindStoredMessages` on its context, in the local `stored_messages` collection, one copy per message ID) and a RETRIEVE screen (shown in the title bar only for a `UserRole.Client` on a network that has a server; see `Docs/Components/ViewModels.md`, `IRetrieveViewModel`). Everything else is the handler's: which messages a server stores, who may retrieve them and what a request looks like. A retrieval submitted on the RETRIEVE screen becomes `OnRetrieval(context)` with the chosen storage server (`Server`, one of `IEngineController.StorageServers`, which is every server user) and a `RetrievalCriteria` (`From`/`To` UTC instants, and `Authors`, `Destinations` and `Ids` lists). A stored message fits when it satisfies every criterion that is set, each list matching any of its entries, exactly and case-insensitively; an unset criterion matches anything (`FindStoredMessages` applies this). The handler sends the request in its own frame, and the server's handler answers with the messages it finds, which arrive as ordinary frames and are received with `ReceiveMessage`, oldest first; a client skips any whose ID its Inbox already holds. Storage needs the server's database, which the Client/Server UI opens; a server in Headless mode has none and stores nothing.

**Default:** no handler, so nothing is stored or answered; the RETRIEVE button is never shown on a network without a server.

**Network file:** none; storage is behavior, not a setting.

**Sample:** a server's `FrameHandler` stores each message that does not come from another server and answers a retrieval request with the matching copies, addressed to the requester alone and marked not an alert so an old alert does not alarm again.

---

### HDLC Options

```csharp
engine.Hdlc().MaxInfoField(1024).Crc(HdlcCrc.Crc32Ccitt);
```

The options every serial connection starts its HDLC peer with: line encoding, CRC and clocking (`Link`, which must match the station at the far end of the cable), frame size, transmit window and retransmission timing. `Hdlc()` is its own top-level configuration with one call per option (`Encoding`, `Crc`, the clock sources, `ClockSpeed`, `MaxInfoField`, `TransmitWindow`, `RetransmitInterval`, `MaxRetransmissions` and the rest of the MicroGate package's `HdlcPeerOptions`), each defaulting to the package's own value. The HDLC address is not an option; each serial `ConnectionPoint` supplies it, used as both this station's and the remote station's address.

**Default:** the MicroGate package's HDLC peer defaults.

**Sample:** caps frames at 1024 bytes and the transmit window at 4 (a smaller frame is always safe with any remote station); see the MSMT section for its timeout adjustment.

---

### MSMT Certificates

MSMT peer authentication is mandatory - there is no unauthenticated mode. The certificates come only from the files the network configuration file designates: its `CertificateStore` is a folder holding one `{USERNAME}.pfx` identity per user, of which a node loads the one for the current user, and its `AuthorityCertificate` is the one authority every identity certificate must chain to and the only one a node trusts. A user's certificate carries the user's name as its common name, which is the name [connection identification](Identification.md) matches against. Nothing is looked up in the system certificate store.

The MSMT options (identity certificate plus trusted authority) used for both inbound and outbound session peer connections are built from those files by `ConfiguredEngineController`, for the current user (via `ICurrentUserProvider`). If either key is missing, there is no current user yet, or either file is missing, building them throws `InvalidOperationException`; callers (`PeerService`, `ClientPeerService`, `ServerRoutingService`, `InterfaceService`) catch this at startup, log it, and simply don't start their listener, retried the next time the host restarts once a user and the files are in place. A host never touches this security-sensitive credential logic.

`Msmt()` is its own top-level configuration that adjusts the other MSMT settings (handshake, stall and response timeouts, TCP keep-alive, session lifetimes and keep-alive intervals) for every IP connection, inbound and outbound, including the interface listener. It has one call per setting (`HandshakeTimeout`, `StallTimeout`, `ResponseTimeout`, `TcpKeepAliveTime`, `MaximumSessionLifetime`, `SessionLifetime`, `KeepAliveMinInterval`, `KeepAliveMaxInterval`, each defaulting to the MSMT package's own value) instead of the library's session options, since those require credentials the engine supplies. Its values are laid over the options built above, and the credentials and hostname rule stay the engine's:

```csharp
engine.Msmt().HandshakeTimeout(TimeSpan.FromSeconds(20));
```

**Default:** the MSMT options are left at the package defaults. Sample states a 15 second handshake timeout and a 60 second response timeout through `Msmt()`.

**Network file:** `CertificateStore` and `AuthorityCertificate` are required for connections and are the only source of certificates; see [Config.md](Config.md).

**Sample:** Sample states only the `Msmt()` timeouts and provisions no certificates of its own; `Scripts/Scenarios/` demonstrates the `CertificateStore`/`AuthorityCertificate` keys with `{USERNAME}.pfx` files checked in alongside each scenario's config.

---

### Network Topology

This instance's place in the peer/client/server networking topology, see [Peer.md](Peer.md#user-roles), is not stated on its own: it comes from the current user's [user info](#user-info) (`Role`, `IpHost`, `Parent`, and for a `Server` its `Children`, with the topology of every server in the cluster built from every `Server` user's info). A node is configured only with where it connects and listens, never which users it expects there; who is on the other end of a connection is worked out when it forms (see [Identification.md](Identification.md)).

The role selects the `IPeerService` implementation (`ClientPeerService`/`ServerRoutingService`). `RolePeerService` is the one service the engine depends on; it creates that implementation when networking starts, after a user is installed, and forwards its events. `Restart()` (used when the network file is reloaded, see [Services.md](Services.md#networkreloadservice)) cancels and disposes the running implementation and creates another from the role as it is then, so a changed role takes effect without restarting the application; a changed port or set of outgoing points is applied in place by `Reconfigure()`, which leaves connections to unchanged points alone.

**Default:** `UserRole.Client`, no links, no server users.

**Network file:** none of its own; the role, IP host, links and topology come from the users' entries. See [User Info](#user-info).

**Sample:** the roles, ports and points are on each site's user info; see [User Info](#user-info).

---

### Command-Line Overrides

```csharp
engine.CommandLineOverrides(true);  // honor --config and --user
engine.CommandLineOverrides(false); // ignore them (the default)
```

Determines whether command-line arguments may override where the [network configuration file](#network-configuration-file) and the running user come from: `--config <path>` names the file instead of `Config.json` in the working directory, and `--user <name>` names the user the process runs as, checked like an installed user, and `--log <categories>` turns on log categories that are off by default. `Config.json` in the working directory is always read; only the arguments are affected. Resolved once, before anything else, from the recorded configuration (see [Bootstrap ordering](#network-configuration-file) above). Because of this ordering, a configuration must never depend on `NetworkConfig`.

**Default:** disallowed.

**Network file:** none possible; there is no field for whether the arguments are honored (that would be circular).

**Sample:** `EngineConfiguration` allows them, so its scenario scripts can pass `--config` and `--user`. A host that wants them ignored (e.g. to lock down a deployment) simply does not call it, or passes `false`.

---

### External Systems

```csharp
engine.ExternalSystem(new MyExternalSystem());
```

`ExternalSystem` adds an external system: a conduit to a system outside Comlink (a socket, a message queue, an HTTP long-poll, etc.) this instance communicates with, resolved once at startup by `ExternalSystemsService`. See `Docs/Components/ExternalSystems.md` for the full contract and behavior; the shape here is deliberately terse since that doc covers it in depth. `IExternalSystem` is already non-generic, so no cast is involved. `ExternalSystemBase<TFrame>` is available as an optional convenience base class for implementing `IExternalSystem` with less boilerplate (the connect/poll/disconnect lifecycle, filtering, etc.) but is never required; any `IExternalSystem` implementation works.

Each external system is constructed directly by the configuration, not resolved through DI, so a logger it is given by the configuration comes from the bootstrap container and writes to none of the engine's logs (the engine's logging providers, e.g. `DailyFileLoggerProvider`, need the configuration's output for their log file location). `ExternalSystemsService` instead calls `IExternalSystem.AttachLogger` on each system, using its own `ILoggerFactory` from the running container, before starting it, see `Docs/Components/ExternalSystems.md`.

**Default:** no external systems.

**Network file:** none; a system-specific connection endpoint, credential, etc. belongs to each `IExternalSystem` implementation's own constructor, not a generic config schema.

**Sample:** none; `EngineConfiguration` states no external system.

---

### Frame Handler

```csharp
engine.Frames<MyFrameHandler>();

public sealed class MyFrameHandler : IFrameHandler<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect>
{
    public Task OnConnected(INetworkConnectedContext<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect> context) { ... }
    public Task OnDisconnected(INetworkDisconnectedContext<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect> context) { ... }
    public Task OnReceived(INetworkReceivedContext<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect> context) { ... }
    public Task OnSent(INetworkSentContext<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect> context) { ... }
    public Task OnRead(INetworkReadContext<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect> context) { ... }
    public Task OnRetrieval(INetworkRetrievalContext<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect> context) { ... }
}
```

The handler is the whole protocol: the engine is only transport, GUI and storage, and does nothing automatically. Receiving, routing, receipting, retrieval, forwarding and the network indicator all happen here. Every method has a default that does nothing, returns a `Task`, runs in the background (not awaited by the engine), and an exception it throws is logged and never stops a later event from being handled.

Events:
- `OnConnected` / `OnDisconnected` fire once each time a user goes from unreachable to reachable over at least one live peer connection, or the other way around (`TargetUser` is that user's `UserInfo`).
- `OnReceived` fires for every frame this instance receives, handed the `Frame` typed as the host's own frame type, the `SourceUser` (a `UserInfo`) it came from and its `Origin` (`FrameOrigin.Peer`, `Interface` or `ExternalSystem`).
- `OnSent` fires when the user sends a message with the GUI (or `IServiceConnection<,,>.SendMessage`), handed the `Message<,,>` and its `Destinations` (a set of the users it is for, built from its addresses with groups expanded; a user it cannot be sent to, such as one below the message's level, is marked `Failed` by the engine first and is not in the set). The message is already in the Outbox with every recipient `Sending`. `OnSent` is not called when no destination is left.
- `OnRead` fires when the user opens an unread Inbox message, handed that `Message<,,>`.
- `OnRetrieval` fires when the user submits a retrieval, handed the `Server` (a `UserInfo`) and the `Criteria`.

Every context extends the common `IEngineContext` (`CurrentUser`, `Users` and `ConnectedUsers`, both read-only dictionaries of `UserInfo` by name where a user is connected when its name is a key of `ConnectedUsers`, and `GetGroupMembers(groupName)`) and `INetworkContext<TFrame>`, which adds:
- `Send(user, priority, frame)`: sends the frame, exactly as given, to `user`, who must be directly connected to the current user (a client's only such user is its server; a server's are its children and sibling servers), and returns whether they accepted it, or `false` when they are not connected. There is no routing or hopping: reaching anyone further away is the handler's job, by sending to the node that is directly connected and putting the real recipients in the frame. The priority is a required `TPriority`.
- `ReceiveMessage(message)`: records a `Message<TPriority, TLevel, TAspect>` as received, so it is stored in the Inbox and shown (and alarms, when it is an alert). A message with a priority, level or aspect that is not configured throws.
- `SetSentStatus(messageId, user, status)` (also taking several users at once) and `SetReceivedStatus(messageId, status)`: change the delivery status of a stored sent or received message.
- `SetNetworkIndicator(isOnline)`: the only thing that sets the network indicator.
- `GetDestinations(minimumLevel, out excluded, targets)` / `GetDestinations(message, out excluded)`: the set of users that some user and group names, or all the non-external addresses of a message, stand for, with groups expanded to their members recursively. Users whose own message level ranks below the minimum level (the message's own level for the message overload; `null` for no minimum) are left out of the result and returned in `excluded`, for the handler to report as failed.
- `SendInterface(priority, frame)`, which sends a frame to every interface connected to the local interface listener, `SendToExternalSystems(frame)`, `StoreMessage(message)`, `FindStoredMessages(criteria)` and `GetAutoForwardTargets(controllerName)`.

The handler is stated by type and instantiated through the running engine's dependency injection container, so its constructor can take services.

**Network indicator.** A client shows a network indicator in the top bar (to the right of the alert indicator in a client), always present, online or offline, in the label and color the display handler states for each. Nothing sets it but the handler (`SetNetworkIndicator`, typically from `OnConnected` and `OnDisconnected` for its parent). It starts offline. A server has no indicator, and it is hidden on the install screen.

**Default:** no frame handler; `EngineHooksService` (which runs it) does nothing when none is configured, so no message is received, routed, receipted or retrieved and the indicator stays offline.

**Network file:** none; a handler is behavior, not a setting.

**Sample:** `FrameHandler` implements the protocol for its two roles from `context.CurrentUser`: a client expands groups, enforces message levels, sends one frame to its server, sends receive and read receipts, and forwards escalations; a server stores, routes once per hop or owning server and answers retrievals. A client keeps the network indicator in step with the connection to its server.

---

### `IExternalDriveProvider` (not configurable)

```csharp
IReadOnlyList<ExternalDriveInfo> GetDrives();
```

Enumerates the external (removable/optical) drives currently available as a destination for the export feature or a source for the import feature (see `Docs/Components/ViewModels.md`, `IExportViewModel`/`IImportViewModel`) - both share this same member and drive list. Each `ExternalDriveInfo` carries a `RootPath` (to write to or read from) and a `DisplayName` (volume label + drive name, for the drive picker); both the record and the interface live in `Core/src/Internal/Devices/ExternalDriveProvider.cs`.

Unlike the settings above, this is real OS-level behavior, not configuration or rules, so it is not part of the engine configuration and a host cannot replace it: Engine always provides real behavior for it directly, the same way it always provides real behavior for alarm sound playback (see `IAlertSoundPlayer`, above) and printer discovery/driving (see `IPrintDriver`, below).

**Engine implementation:** `ExternalDriveProvider` (`Core/src/Internal/Devices/ExternalDriveProvider.cs`) - `DriveInfo.GetDrives()` filtered to ready `Removable`/`CDRom` drives that pass a live write probe (a small temp file is written and deleted at the drive root). Not unit tested directly - inherently environment-dependent, so a unit test could only meaningfully assert against whatever removable drives happen to be connected to the machine running the test.

---

### `IPrintDriver` (not configurable)

```csharp
IReadOnlyList<string> GetAvailablePrinters();
string? GetDefaultPrinter();
Task PrintLine(string printerName, string line, CancellationToken cancellation = default);
Task PageFeed(string printerName, CancellationToken cancellation = default);
```

`GetAvailablePrinters`/`GetDefaultPrinter` enumerate the printers available on this computer for the print manager to target (see `Docs/Components/ViewModels.md`, `IPrintManagerViewModel`): `GetAvailablePrinters` populates the printer picker, `GetDefaultPrinter` selects the initial `SelectedPrinter` automatically. `PrintLine`/`PageFeed` drive the selected printer for the print queue: prints one line at a time, and the returned task from `PrintLine` completing is treated as confirmation that the line finished printing - the queue will not print the next line, or check whether a higher-priority job should interrupt the current one, until it completes. `PageFeed` is called after the last line of an entry and also when a job is interrupted partway through.

Unlike the settings above, this is real OS-level behavior, not configuration or rules, so it is not part of the engine configuration and a host cannot replace it: it lives in `Core/src/Internal/Devices/`, and Engine always provides real behavior for it directly, the same way it always provides real behavior for alarm sound playback (see `IAlertSoundPlayer`, above) rather than leaving either to a host. Printer discovery is a genuine operating-system resource (like external drives, above), not app-specific configuration, and driving a printer line-by-line with real completion confirmation only makes sense against the operating system's own print spooler, not a bundled library. None of the four members has a the network file field.

**Engine implementation:** `PrintDriver` (`Core/src/Internal/Devices/PrintDriver.cs`), backed by a `file`-scoped `PrintOperations` helper class (marked `[ExcludeFromCodeCoverage]`), OS-branched via `OperatingSystem.IsWindows()`/`IsLinux()`:
- **Windows:** printer discovery shells out to PowerShell, querying WMI's `Win32_Printer` class (`Get-CimInstance -ClassName Win32_Printer`) for the printer list and the entry with `Default = true` for the default printer - no extra module dependency (unlike `Get-Printer`, which requires the PrintManagement module). Line printing uses the Windows Print Spooler (WinSpool) directly via P/Invoke (`OpenPrinter`/`StartDocPrinter`/`StartPagePrinter`/`WritePrinter`/`EndPagePrinter`/`EndDocPrinter`): each line (and each page feed, sent as a form-feed byte `\f`) is submitted as its own raw print job, and `PrintLine`/`PageFeed` don't return until polling `GetJob` reports the job has reached a terminal status (`JOB_STATUS_PRINTED`, `JOB_STATUS_COMPLETE`, `JOB_STATUS_DELETED`, or `JOB_STATUS_ERROR`) - a genuine OS-confirmed completion, not just "the app handed the bytes off."
- **Linux:** printer discovery shells out to `lpstat -p`/`lpstat -d` (CUPS). Line printing submits each line (and each page feed, as `\f`) as its own raw job via `lp -d {printer} -o raw` (parsing the returned job ID from `lp`'s "request id is …" output), then polls `lpstat -W not-completed -o {printer}` until that specific job ID no longer appears among the printer's pending jobs - the CUPS-level equivalent of the same "wait for OS-confirmed completion" contract.
- **Other platforms:** printer discovery returns an empty list/no default; line printing is a no-op.
- Both platforms poll every 150ms with a 30-second-per-line safety timeout, so a stuck or offline printer cannot hang the print queue forever; discovery and printing are both best-effort - any failure (missing tooling, no printers configured, permission error) degrades gracefully (empty list / no default / a line that times out and moves on) rather than throwing.

Not unit tested directly, since it is inherently environment- and OS-dependent, so a unit test could only meaningfully assert against whatever printers happen to be installed (and reachable) on the machine running the test - `Docs/Components/ViewModels.md`'s `PrintManagerViewModelTests` instead test the print queue's own logic (ordering, interruption, restart) against a mocked `IPrintDriver`/`IEngineController`.

---

## Client API

---

#### `IServiceConnection<TPriority, TLevel, TAspect>`

```csharp
event Func<Message<TPriority, TLevel, TAspect>, Task>? MessageReceived;
event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;
Task Connect(CancellationToken cancellation = default);
Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default);
Task<List<string>> GetUserNames(CancellationToken cancellation = default);
Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default);
Task<UserInfo?> InstallUser(string userName, CancellationToken cancellation = default);
Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, TPriority? priority = null, string tag = "", TLevel? messageLevel = null, TAspect? messageAspect = null, CancellationToken cancellation = default);
Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
```

High-level API for host code to interact with the running Engine. Engine registers `DirectServiceConnection`, which calls Engine services in-process, in both Client and Headless mode - Headless mode acts as a normal peer client, just without a GUI. External programs instead plug into the message stream over the local interface listener (see [Interface.md](Interface.md)), which is unrelated to this interface.

Host applications resolve `IServiceConnection<TPriority, TLevel, TAspect>`, typed by the enums stated in `Types`, from the container to send messages, install the user, and subscribe to inbound delivery events. `GetConnectedUsers` reports who is currently reachable over a live peer connection (`IPeerService.GetConnectedUsers`), unlike `GetUserNames`'s fixed configured directory.

**Default:** `DirectServiceConnection`, registered in both Client and Headless mode.

**Sample:** none, deliberately - this is not a piece of *external configuration* a host states (the "Concept" section's definition); it is the client-facing API surface a host *consumes* to drive the running Engine, backed by `DirectServiceConnection`'s substantial in-process orchestration of Engine services. Sample resolves `IServiceConnection` directly from the container instead of replacing it. This is why it is documented in its own "Client API" section rather than among the settings.

---

## Supporting Type

#### `ICurrentUserProvider` / `CurrentUserProvider`

```csharp
string? UserName { get; set; }
```

Exposes the mutable user name of the currently running instance, once installed (`null` beforehand). `UserService` writes to it; the engine's MSMT options (via `EngineController`'s constructor dependency) read from it to decide which certificate to request. Registered as a singleton via convention scanning (`ICurrentUserProvider → CurrentUserProvider`), like any other `IThing`/`Thing` pair, but is not itself a control interface - it holds mutable runtime state, not configuration, and is consumed as an ordinary constructor dependency by many unrelated parts of the app (logging, peer services, `UserService`), not just by the engine controller.

Host code should not write to `CurrentUserProvider` directly; let `UserService` manage it.
