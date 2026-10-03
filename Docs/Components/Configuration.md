# Engine Configuration

A host tells the engine how to run by implementing `IEngineConfiguration` and naming it to `Engine.Start<T>`, which constructs it through dependency injection. The engine calls `Configure` once, before anything else starts, handing it an `IEngineBuilder`; every call on the builder is optional except `Frames<TFrame>`, which states the frame type and everything typed with it (through `IFrameBuilder<TFrame>`), and each returns the builder so a configuration reads as one fluent expression. `Packets<TPacket>` likewise states the packet type and what is typed with it (through `IPacketBuilder<TPacket>`). The builder is the only public way to change what the engine does; everything it collects is read internally through `IEngineController`, which is not part of the public surface. See each area below for what it covers.

## Concept

Engine never reads environment variables, hardcodes paths, or calls host-specific APIs directly. Instead, every piece of external configuration and rule-based behaviour is either a call on `IEngineBuilder` or part of the network configuration file, which describes the users of the network. A host states only what differs from the engine's defaults:

```csharp
public sealed class MyEngineConfiguration : IEngineConfiguration
{
    public IEngineBuilder Configure(IEngineBuilder engine) => engine
        .Frames<MyFrame>(frame => frame
            .Id(m => m.Id)
            // ...every other logical field...
            )
        .HomeText("Select a folder and entry to get started.")
        .Users("ALICE", "BOB");
}

await Engine.Start<MyEngineConfiguration>(args);
```

`EngineBuilder` (internal) implements `IEngineBuilder` by recording what it is told; nothing is interpreted while configuring. `EngineController` (internal) reads the recorded state through `IEngineController` and supplies the default for every setting the host left alone, which is what every service, ViewModel and repository in the engine depends on. Keeping the recording separate from the reading means a configuration can be checked as a whole (`Engine.Start` throws an `InvalidOperationException` naming a missing frame type or unmapped field before any service starts), and that tests can replace a single behavior of the controller.

**A configuration describes non-config-file behavior only. It must never read `NetworkConfig` itself, and it must never read an environment variable.** Everything about the network's users, and the settings of the node a user runs, comes from the network configuration file, applied by the engine itself: the users' info is read by `EngineController`, and the node settings (identity certificate file, alert, tag and print settings) are applied as a decorator layered on top of it (see [Network Configuration File](#network-configuration-file) below). This split keeps "what does this app do out of the box" (the configuration) and "who is on this network and how do they run" (the file) as two independent, separately testable concerns, and means a host is never tempted to reimplement file parsing just to add one small piece of non-file behavior.

**Dependency injection:** `Engine.Start<T>` builds a bootstrap container holding logging (`ILoggerFactory`, `ILogger<T>`) plus whatever the host's `configureServices` argument registers, and constructs `T` from it, so a configuration's constructor can take services. The bootstrap container is deliberately separate from the running engine's: the engine's own logging providers need the engine's configuration (for the log file location), so a configuration built from the running container could not take a logger without a cycle, and it has to be built before the container exists anyway, because it decides whether `--config` is read and so what the container is built from. The same `configureServices` registrations are applied again to the running engine's container, which is also where a host registers anything else it wants running alongside the engine (for example a hosted service that uses `IServiceConnection`); a service registered there therefore exists once in each container. The bootstrap container lives until the engine exits, since a configuration may have handed the builder functions that use what was injected.

## Network Configuration File

The engine defines the schema of one network configuration file, shared by every node of a network (see [Config.md](Config.md) for every field). It holds the info for all users of the network, who is in which group, and the trusted certificate authority, so no user, port or connection is stated in code. It is read from the path given by the `--config` command-line argument, otherwise from `Config.json` in the current working directory; a missing default file is an empty network, while a `--config` path that does not exist is an error. The file can be read again while the application runs (right-click the user name in the title bar and choose "Refresh"): connections no longer defined are brought down and newly defined ones opened while unchanged connections are left alone, and the rest is applied as it is next read. The `--user` argument, or else a `User.json` in the working directory, names the user the process runs as, which lets a node skip the install screen.

`EngineController` reads the file for everything about users (`GetUserInfo`, `Users`, `UserGroups`, the trusted authority name, and so on, see [User Info](#user-info)). `EngineExtensions.UseEngine` registers `IEngineController` as a `ConfiguredEngineController` wrapping it, which takes the loaded `NetworkConfig` and `ICurrentUserProvider` and applies the node settings of the current user's entry: member by member, the entry's value when it is set and the wrapped controller's value otherwise; every other member delegates straight to the wrapped controller. It is registered explicitly, never by convention scanning. The current user is the one named by `--user`, or else the installed user, except for the headless choice, which only the `--user` user decides since an installed user is not known until networking starts. The user also decides the data folder (`%APPDATA%/{AppName}/{USERNAME}`), which the decorator points at the `--user` user even before the install state has been read.

**Bootstrap ordering:** whether the command-line arguments may override the file and the user is itself a setting (`CommandLineOverrides`), so `Engine.Start` constructs the configuration and builds the `EngineBuilder` first, reads its `AreCommandLineOverridesAllowed`, and only then loads `NetworkConfig`, passing the arguments only if allowed. There is no field in the file for it (that would be circular), and a configuration cannot depend on the file for the same reason. When overrides are not allowed, `--config` and `--user` are ignored entirely, as if the arguments had never been passed, and only `Config.json` and `User.json` in the working directory are read.

## Settings

### Frame Format

```csharp
engine
    .Frames<MyFrame>(frame => frame
        .Id(m => m.Id)
        .Sender(...).Addresses(...)
        .Message<MyMessageHandler>()
        .Retrieval<MyRetrievalHandler>()
        .ReadReceipt<MyReadReceiptHandler>()
        .ReceiveReceipt<MyReceiveReceiptHandler>()
        .AutoForward<MyEscalationController>())
    .Packets<MyPacket>(packet => packet
        .Frame<MyFramePacketHandler>()
        .Size(16 * 1024).Window(1));
```

A **frame** is the data format of all network traffic other than packets: every heartbeat, receive receipt, read receipt, retrieval request and user message, and any other frame the host's own processors exchange, is an instance of the host's one frame type. A **message** is a kind of frame, the kind the user sees: it is shown in the UI, stored in the Inbox when received and in the Outbox when sent, and is what auto forward, printing, server storage and external systems act on. Each kind of frame is stated with a handler type (`Message<THandler>`, `Retrieval<THandler>`, `ReadReceipt<THandler>`, `ReceiveReceipt<THandler>`), instantiated through dependency injection like serializers and processors, and implementing the matching interface (`IMessageHandler<TFrame>`, `IRetrievalHandler<TFrame>`, `IReadReceiptHandler<TFrame>`, `IReceiveReceiptHandler<TFrame>`). A handler has three jobs: `Create` takes the inputs relevant to its kind (a `MessageCreateContext`, `RetrievalCreateContext` or `ReceiptCreateContext`) and returns a new frame that is of that kind; `IsValid` says whether a given frame is of that kind; and getters read the kind's logical fields from a frame (sent time, body, alert, priority, tag and security level for a message; the date range, authors, destinations and ids for a retrieval request; the message id for a receipt). Every handler must be stated. The aspects every frame needs to be routed (id, sender, addresses) stay on the frame builder as getter and setter mappings, and the engine stamps them onto every frame a handler creates. A frame the message handler does not recognize is still routed and handed to the network processor but is never shown or stored; a processor that sends a frame the recipient should see makes one the message handler recognizes. Frames are classified by asking handlers, never by a stored enum, so a host decides how a frame says what it is (a flag, a field that is empty or not, a derived rule; the tests recognize a message as a frame whose `IsHidden` flag is not set, so a default frame is a message).

`Frames` supplies the concrete frame type used throughout the engine, on the wire (peer and interface connections) and in the database, and maps the engine's logical fields onto that type's real fields. Each mapping is a getter and a setter, so the engine reads and builds the host's frame without ever assuming a field name or shape. Where the host's field has the type the engine wants, naming the property (`.Id(m => m.Id)`) is enough: the builder reads the member access from the expression and compiles a getter and setter from it once, when the configuration runs, so using it costs no more than writing them out (an init-only property works; a member that cannot be assigned, or an expression that is not a plain member access such as `m => m.Id.ToUpper()`, is refused at once with an error naming it). Where the types differ - the addresses, which the host stores in its own recipient shape and converts to and from `(string Name, AddressType Type, string Information)` tuples (the getter returns any sequence of them, the setter receives a list; `Information` is optional custom per-address instructions, e.g. `Deliver to Eastside Office` - a `(string Name, AddressType Type)` overload with no `Information` is also available for a host with no use for it), and a packet's data, which is a `ReadOnlyMemory<byte>` - the getter and setter are given explicitly. Every field and every handler must be stated. The type must be LiteDB-serializable for storage, and must additionally satisfy whatever serializer is used for the wire, which by default is a `ProtobufSerializer` that builds only the frame type (so `[ProtoContract]`/`[ProtoMember]` attributes). `Serializer<TSerializer>` on the frame builder replaces it with a type implementing `IFrameSerializer` (derive from `FrameSerializer<TFrame, TPacket>` to work with the frame and packet types rather than `object`), instantiated through the running engine's dependency injection container, as long as every node this instance talks to (including its own interface connections) uses a matching one: Comlink never negotiates or advertises which format a payload used, so a mismatch deserializes garbage or throws rather than failing cleanly. A serializer can write into a `PooledBufferWriter` (an `IBufferWriter<byte>`, which a `Utf8JsonWriter` accepts) and return its `ToOwner()` to keep the buffers it hands back pooled rather than allocated per frame; `SampleJsonSerializer` does this. `IFrameSerializer.Deserialize` is given the bytes and the first packet that carried the frame across (`null` when packetization is disabled, or the frame arrived over an interface connection), so a serializer can read what the host's own packet fields say about the frame; it so a custom serializer must make its format self-describing enough to rebuild the right type itself (the default wraps every payload in an outer envelope naming the type). Both serializers' `Deserialize` return a value or throw `InvalidDataException` for bytes they cannot or will not build (such as a type the sender names that is not this engine's own); the engine treats a throw as a rejected frame or packet. `Create` replaces `new TFrame()` for building an empty message. The retrieval fields back [Server Storage](#server-storage); the receipt handlers and alert flag back the receive and read receipts and alert-message features (see [Peer.md](Peer.md#receipts) and [Peer.md](Peer.md#alert-messages)); the priority backs [Message Composition](#message-composition) and the MSMT send priority, and the tag backs [Message Composition](#message-composition) too.

Packetization is off unless `Packets<TPacket>` is called. With it, payloads are broken into prioritized packets of the host's packet type and reassembled on the other side. The host only states how its packet carries a piece of a frame, with a frame packet handler (`Frame<THandler>`, instantiated through dependency injection like the frame handlers, implementing `IFramePacketHandler<TPacket>`): `Create` takes a `FramePacketCreateContext` (payload id, packet index, packet count, payload length and the data slice, which is only valid during the call so a packet that stores it must copy it) and returns a packet; `IsValid` says whether a given packet is a frame packet, as opposed to one that carries no frame, such as an initial packet exchanged by a processor, and the engine refuses to reassemble a packet that is not one; and getters read the same five aspects back. The handler must be stated; all splitting, reassembly and priority scheduling is the engine's, so a host gets its own packet format and serialization without writing any packetization logic. The packet serializer, a type implementing `IPacketSerializer` (derive from `PacketSerializer<TFrame, TPacket>`) stated with `Serializer<TSerializer>` on the packet builder and instantiated the same way, defaults to a `ProtobufSerializer` that wraps every packet in an envelope naming its type, a fixed overhead per packet that a leaner custom serializer avoids. `IFrameSerializer.ConfigurePacket(frame, packet)` is called on every outgoing frame packet, once per packet in order and before the packet is serialized, so the frame serializer can set the host's own packet properties from the frame being packetized. `IPacketSerializer.Serialize` is also given the packet and the original frame being packetized (`null` for a packet that carries no frame, such as one an initial packet processor sends), so a packet's encoding can depend on its frame. `Size` (default 16 KiB) is the largest serialized packet in bytes: the engine measures what the serializer makes of a packet to see how much payload fits, and refuses to start with an error in the log if none does. `Window` (default 1) is how many packets may be in flight over one connection at once, and must be at least 1. Every node must be configured alike, since neither side can tell whether the other packetizes. Interface connections are never packetized.

Internally the configuration becomes a `FrameMap` and a `PacketMap`, whose accessors and handlers take the frame or packet as an `object`, since that is the boundary every other layer (LiteDB storage, MSMT wire serialization) operates at. A packet member of an engine that never called `Packets` throws `NotSupportedException`, because nothing calls them.

**Default:** none for the frame, since the engine has no frame DTO of its own; no packetization.

**Network file:** none; the file has no field for any frame or packet member, since the whole point is that the engine does not know the DTO's shape.

**Sample:** `SampleEngineConfiguration` maps every logical field onto `SampleFrame`, a DTO with deliberately differently-named fields (`Id`, `Sender`, `Title`, `Text`, `Recipients`, ...) to demonstrate that the mapping, not any assumed field name or shape, is what the engine relies on, and turns packetization on with `SamplePacket` and the default size and window.

---

### App Settings

```csharp
engine.AppName("MyApp").AppVersion("1.2.3").KioskMode().HomeText("Welcome").WindowIcon("avares://Host/icon.png");
```

This app's own identity and top-level presentation: the display name (also the name of the folder holding the install state), the version shown in the title bar and the info popup, whether the main window runs in kiosk mode (hides window chrome and restricts navigation), the placeholder text shown in the content area when no entry is selected, and the window icon (an `avares://` URI of an Avalonia asset, or else the path of an image file).

**Default:** the name comes from the entry assembly name; the version is the entry assembly's `major.minor.build` version (`1.0.0` if it has none); kiosk mode is off; the home text is `"HOME"`; the icon is the operating system's.

The data folder is not configurable: a user's persistent state (LiteDB database, logs) is always written to `%APPDATA%\{AppName}\{USERNAME}` (`IEngineController.AppDataPath`), so users sharing a machine never share data. The one thing outside it is the file remembering which user is installed, `%APPDATA%\{AppName}\State.json` (`IEngineController.StatePath`), which sits beside the user folders since it is what says whose folder to use; logged lines from before a user is installed or named go to `%APPDATA%\{AppName}\Logs`.

**Network file:** none; the file has no field for any of these, and none for the data folder.

**Sample:** `SampleEngineConfiguration` states the home text and the window icon; everything else uses the default, including the data folder, which is always the user's own.

---

### User Identity

```csharp
engine.DebugUser("TEST1").UserCodes(code => code == "CODE1" ? "TEST1" : null);
```

How this instance's own local user identity is established: a fixed debug override that bypasses the normal `State.json` lookup, and mapping a user activation code (entered during installation) to the name of the user it installs, nothing more: everything else about that user comes from [User Info](#user-info). See `Services.UserService`.

**Default:** no debug user; the code `"CODE"` resolves to the user `"TEST"`.

**Network file:** the `--user` argument overrides the debug user when given. See [Config.md](Config.md). Unless the host states its own code scheme, an install code is simply the name of a user of the network (case-insensitive), so `--user` and the install screen agree.

**Sample:** `SampleEngineConfiguration` states no code scheme and no debug user: an install code is the name of a user in the network file its scenario passes, and each scenario script names its user with `--user`.

---

### User Directory

```csharp
engine.Users("ALICE", "BOB").Group("OPS", "ALICE", "BOB");
```

The addressable users and groups, stated in code and in the network file: the names used for the destination auto-complete in the draft editor and for [connection identification](Identification.md), and group membership for address expansion (members may be user names or other group names, enabling nested hierarchies). A name is only a name here; what is known about each user, including how a node run by that user connects, is stated through [User Info](#user-info). By convention user names are all uppercase.

When a message is sent to a group, the Engine records which addressed groups each user was reached through. The sent message view shows this context, e.g. `USER-A (OPS)`, so the operator can see which group membership drove delivery.

**Default:** no known users or groups.

**Network file:** the file's `UserGroups` merge over the stated groups (a file entry replaces a same-named group; groups only stated in code still pass through), and its user and group names are added to the stated names, deduplicated. A user's info lists the groups it is a member of.

**Sample:** `SampleEngineConfiguration` states three built-in user names matching its codes; the file's names are still unioned in.

---

### User Info

```json
"Users": {
  "SERVER1": { "Role": "Server", "PeerPoint": { "Host": "10.0.0.1", "Port": 50221 }, "InterfacePort": 50220,
    "Parent": "SERVER2", "Children": [ "CLIENT1" ],
    "StoresMessages": true, "SecurityLevel": "RESTRICTED" }
}
```

Everything about one user is stated on that user's entry in the [network configuration file](#network-configuration-file), which the engine turns into a `UserInfo` and hands out by name (`IEngineController.GetUserInfo`); a user the file does not list is just a name. There is no per-aspect engine method for a user's role, ports, connections, security level and so on, and no user info in code. The fields:

| Field | Meaning | Default |
|-------|---------|---------|
| `Role` | The [networking role](Peer.md#user-roles) of a node this user runs: `Peer`, `Client`, `Server` or `Relay` | `Peer` |
| `PeerPoint` | `Host` and `Port` other nodes use to reach the node over IP, and the port it listens on | `127.0.0.1`, `50021` |
| `InterfacePort` | Loopback TCP port of the local interface listener, always active in every role (see [Interface.md](Interface.md)) | `50020` |
| `Parent` | The user above this one in a hierarchy, by name or as an object forcing the connection mode (`MsmtListen`, `MsmtConnect` or `SyncSerial`); by default the node dials it | none |
| `Children` | The users below this one, for a `Server` or `Relay`, each by name or as an object forcing the connection mode; by default the node listens for them | none |
| `StoresMessages` | For a `Server`, whether it stores the messages it routes and answers retrieval requests (see [Server Storage](#server-storage)) | `false` |
| `SecurityLevel` | The name of the level the user runs at (see [Security Levels](#security-levels)) | the lowest configured level |
| `CertificateName` | The certificate subject name of the user: the identity certificate to look up for the local user, and the name others' certificates must carry (see [MSMT Certificates](#msmt-certificates)) | the user name |
| `Data` | App-specific string keys and values; the engine does not interpret them, they travel with the user's `UserIdentity` | none |

The current user's info is what decides how this node behaves, so it is read once a user is installed (or named by `--user`), not when the engine starts: until then the node is a `Peer` with the default ports and nothing connected, showing only the install screen. The topology a `Server` routes with, every server in the cluster and the children each owns, is every user in the [directory](#user-directory) whose role is `Server`, with its `Children`. Every node on a network uses the same file, since a node learns about other users, such as which servers store messages, from it.

**Default:** a user with nothing stated is a `Peer` on the default ports that connects nowhere.

**Network file:** this is the file; each field above has the same name in a user's entry (see [Config.md](Config.md)), and the entry also carries the settings of the node that user runs.

**Sample:** each scenario under `Scripts/Scenarios/` (`Peer`, `ClientServer`, `ServerCluster`, `ClientRelayServer`) has its own `Config.json` describing its whole network: role, peer point, parent and children, security level, storage, and certificate file for each of its users. Each scenario script passes it with `--config` and names its user with `--user`.

---

### Connection Identification

```csharp
engine
    .Frames<MyFrame>(frame => frame.InitialProcessor<MyFrameIntroduction>())
    .Packets<MyPacket>(packet => packet.InitialProcessor<MyPacketIntroduction>())
    .Identify(connection => ...);
```

Who is on the other end of a connection, decided as the connection forms. All traffic between nodes is a serialized instance of the configured frame type, or of the packet type when packets are configured, and nothing else, so the introduction is too: an `IInitialPacketProcessor<TPacket>` (stated on the packet configuration) and an `IInitialFrameProcessor<TFrame>` (stated on the message configuration) each get `OnConnected` on both nodes when a connection forms, `OnInitial` on the accepting node for each item the opener sent and `OnReply` on the opener for each item the accepting node sent, every time with a controller that can send an item, mark the connection fully connected as a user name, or disconnect it. The packet exchange runs beneath the packetizer and first, the frame exchange above it, and the name a processor marks the connection connected as wins; otherwise `Identify` is handed an `IConnectionInfo` (an `IIpConnectionInfo` for IP: the remote host, port and certificate names; an `ISerialConnectionInfo` for serial: the port and addresses; and this node's own `LocalUser`) and returns the user name, or `null` to let the engine decide. The exchange, where it sits in the transport stack, and what the engine does by default are described in [Identification.md](Identification.md). Every node on a network must be configured alike, as with packetization. Processors are stated by type and instantiated through the running engine's dependency injection container (the instance the host registered for the type, or else one constructed from the host's services), once and on first use, so a processor's constructor can take services.

**Default:** the hook returns `null` (the engine identifies an IP connection by its certificate name and a serial connection by its port name), and no initial packet or message processor is stated, so no exchange takes place.

**Network file:** none, because these are behavior, not settings.

**Sample:** `SampleEngineConfiguration` states a `SampleIdentityProcessor`: the opener sends a `SamplePacket` whose chunk is its user name (`IConnectionInfo.LocalUser`), the accepting node answers with one carrying its own, and each marks the connection connected as the name it received, so its connections, IP and serial, are identified by the packet instead of by certificate name or port.

---

### Alert Settings

```csharp
engine.AlertLabel("ALERT").AlarmDuration(TimeSpan.FromSeconds(30)).QuickConfirmation().ComposeAlerts();
```

Configuration for the alert-message feature in Client mode: the title bar's alarm box text (also the draft editor's alert checkbox label, so both surfaces always show the same word for "alert"), how long the alarm sound plays before automatically stopping (resetting whenever a new alert arrives while already alarming), whether click/Space/Enter quick confirmation is enabled, and whether the draft editor shows its alert checkbox at all (disabling only affects local origination; the app can still receive and alarm on a peer-originated alert). See [Peer.md](Peer.md#alert-messages) and `Docs/Components/ViewModels.md`. Actually playing the alarm sound is real platform behavior, not configuration, see [`IAlertSoundPlayer`](#ialertsoundplayer-not-configurable) below.

**Default:** `"ALERT"` / 30 seconds / on / on.

**Network file:** the current user's entry may set `AlertText`, `AlarmSoundSeconds`, `QuickConfirmationEnabled` and `ComposeAlertsEnabled`, overriding what is stated, field by field. See [Config.md](Config.md).

**Sample:** none; the default plus the network file already cover every genuinely useful case.

---

### Message Composition

```csharp
engine
    .Priorities(("Low", 0), ("High", 2))
    .Tags(enabled: true, label: "Category")
    .BlockTag("SPAM", null).BlockTag(null, 2);
```

How messages are composed and displayed: the set of selectable priority levels (each a display name paired with the value stored in the message's priority field and used verbatim as the MSMT send priority, larger values sent first, see [Peer.md](Peer.md)); whether message tags are shown anywhere in the UI and what the tag input's watermark says; and which tag and priority combinations are blocked outright when composing a draft (each `BlockTag` pairs an optional case-insensitive tag with an optional priority; leaving either `null` matches any value for that field).

`DraftViewModel` enforces the blocked-combination rules proactively rather than only at send time: `AvailablePriorities` excludes any priority blocked for the currently-entered tag, and setting `Tag` to a value blocked for the currently-selected priority is rejected outright (the value reverts), so a blocked combination can never actually be entered in the draft editor. `SendCommand` also re-checks before sending, as a defense-in-depth safety net. See `Docs/Components/ViewModels.md`.

**Default:** a single `"Normal"` (value `0`) priority level; tags on with label `"Tag"`; no blocked combinations. Stating priorities twice replaces the earlier list.

**Network file:** the current user's entry may set `MessageTagsEnabled` and `MessageTagLabel`, overriding what is stated, field by field. See [Config.md](Config.md). Priorities and blocked combinations have no field in the file.

**Sample:** `SampleEngineConfiguration` states three priority levels (`"Low"`/`"Medium"`/`"High"`, values 0/1/2) instead of the default's one, and demonstrates both blocked-combination kinds: the `"SPAM"` tag is blocked regardless of priority, and `High` priority is blocked regardless of tag. Unlike Sample's other settings, the blocked combinations deliberately change default behavior from the engine's permissive "no blocks" default, since that is the only way to usefully demonstrate that part of the configuration.

---

### Address Type Labels

```csharp
engine.AddressTypeLabel(AddressType.External, "OUTSIDE");
```

Overrides the display label shown for one address type, everywhere it appears in the UI: the address type picker in the draft editor, the per-address badge next to each recipient, and the message view's section headers. Each call replaces the label for exactly the type given; every other type keeps its own current label (its own override, if stated, or the default). The underlying `AddressType` value itself never changes - overriding a label only changes what the user reads, not how an address is stored, routed, or mapped through `Frames<TFrame>.Addresses`.

**Default:** the enum name itself (`"To"`, `"Cc"`, `"External"`).

**Network file:** none; address type labels have no field in the file.

**Sample:** `SampleEngineConfiguration` renames `External` to `"OUTSIDE"`, matching the `Kind` vocabulary `SampleRecipient` already uses for it (see [Frame Format](#frame-format)).

---

### Security Levels

```csharp
engine
    .SecurityLevels(("PUBLIC", "#2E7D32"), ("INTERNAL", "#1565C0"), ("RESTRICTED", "#C62828"));
```

Defines the ordered set of security levels a message may be sent at (`IFrameBuilder<TFrame>.SecurityLevel`, see [Frame Format](#frame-format)): each a display name paired with the hex color shown for it in the title bar's banner (`SecurityLevelBanner`, replacing the fixed orange "DEBUG" banner every user used to see). Order matters: each level ranks higher than the one stated before it, so the last one given is the most senior. A user's level is the `SecurityLevel` on their [user info](#user-info).

A destination user may only receive a message whose security level their own assigned level ranks at or above: `MessageRoutingService.Route` drops any lower-ranked destination before sending, and the draft editor's security level picker only ever offers the sending user's own level and lower, so a message can be deliberately declassified but never sent above the sender's own clearance. Turning the feature off entirely is just leaving `SecurityLevels` empty (the default): every message maps to an empty security level, the picker is hidden, and no destination is ever blocked for lacking one.

**Default:** no security levels; the feature is off.

**Network file:** the level each user runs at is that user's `SecurityLevel` in the file; the set of levels itself has no field.

**Sample:** `SampleEngineConfiguration` defines three placeholder levels (`PUBLIC`, `INTERNAL`, `RESTRICTED`) and states each site's level on its user info: `PEER1`/`PEER2` run at `PUBLIC`, `CLIENT1`/`CLIENT2` at `INTERNAL`, and the server sites (`SERVER`, `SERVER1`, `SERVER2`) at `RESTRICTED`.

---

### Print Policy

```csharp
engine
    .PrintReceived()
    .Frames<MyFrame>(frame => frame.PrintCount(m => m.IsAlert ? 2 : 1));
```

The print manager's automatic "print received" behavior: whether its toggle starts enabled, automatically adding every received message to the print queue from the moment the app starts (the user can still toggle it at any time), and how many times each received message is added to the print queue while it is (`0` to not print it, `1` once, `2` for two copies, and so on). Consulted once per received message via `IEntryService.MessageInserted`. `PrintCount` is stated on the message configuration, so the rule receives the frame typed; the engine casts once on the host's behalf.

**Default:** off / `1` for every message.

**Network file:** the current user's entry may set `PrintReceivedEnabled`, overriding what is stated. See [Config.md](Config.md). The print count has no field in the file.

**Sample:** `SampleEngineConfiguration` states a print count that prints an alert message twice and every other received message once, demonstrating a rule that inspects the message itself; "print received" uses the default.

---

### Deletion Policy

```csharp
engine.CanDelete(folder => folder is FolderType.Drafts or FolderType.Notes);
```

Whether the user can delete entries in a given root folder type (`FolderType.Inbox`/`Outbox`/`Drafts`/`Notes`/`Activity`). Consulted by `EntryBarViewModel.DeleteEntry` before deleting; the active folder's `RootType` is passed straight through, and when the rule returns `false`, `DeleteEntry` is a silent no-op (the entry stays in the data store and in the list) rather than throwing. `LoadFolder` also caches the result on `EntryBarViewModel.CanDeleteEntries`, which drives whether the entry list's right-click "Delete" context menu item is shown at all. Draft and note editors also show a DELETE button, gated on the same rule for `FolderType.Drafts`/`FolderType.Notes` when the editor is created. See `Docs/Components/ViewModels.md`.

**Default:** allowed for every folder type; deletion is unrestricted unless a host locks down specific folders.

**Network file:** none; a fixed, code-level rule, not a per-deployment setting.

**Sample:** `SampleEngineConfiguration` allows deletion only in `FolderType.Drafts` and `FolderType.Notes`, protecting Inbox, Outbox, and Activity entries.

---

### Export Formats

```csharp
engine.ExportFormat<MyCsvExportFormat>();

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

Adds a custom export format, a type implementing `IExportFormat` that is instantiated through the running engine's dependency injection container (the instance the host registered for it, or else one constructed from the host's services), shown as an option in the export screen's format picker alongside the built-in JSON
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
engine.ImportFormat<MyCsvImportFormat>();

public sealed class MyCsvImportFormat : IImportFormat
{
    public string Name => "CSV";

    public TimeSpan? StagedSendDelay => TimeSpan.FromSeconds(1);

    public async Task Import(Stream stream, IImportFormatContext context, CancellationToken cancellation)
    {
        using StreamReader reader = new(stream, leaveOpen: true);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellation)) is not null)
        {
            string[] parts = line.Split(',', 3);
            if (parts.Length < 3) { continue; }
            context.AddStagedSend(new StagedSendData { Body = parts[1], Addresses = [new AddressRequest { UserName = parts[0] }] });
        }
    }
}
```

Adds a custom import format, a type implementing `IImportFormat` that is instantiated through the running engine's dependency injection container, shown as an option in the import screen's format picker alongside the built-in
package format (see `Docs/Components/ViewModels.md`, `IImportViewModel`). Selecting it changes which files the
screen finds on the source drive - not `IExportService.PackageExtension` packages, but files whose extension
matches this format's own name-derived extension (the same derivation an `ExportFormat` entry's file extension
uses - `"CSV"` above becomes `.csv`). Choosing one of those files and importing it opens it as a plain stream and
hands `Import` the stream plus an `IImportFormatContext`, unlike the built-in format's zip archive of typed entries.

The context turns whatever the reader finds into real changes:

- `AddMessage(MessageExportData)`, `AddDraft(DraftExportData)`, `AddNote(NoteExportData)` - insert a new entry
  using the exact same public DTOs a custom export format's serializer receives (see above), applying the same
  rules the built-in package format already applies to its own entries: a message matching an existing one (same
  ID, direction, and date) is skipped, and a draft/note matching an existing entry's name prompts the user through
  the same Keep Existing / Overwrite / Overwrite All dialog - a reader only builds the DTO, never reimplements
  matching or conflict prompting.
- `AddStagedSend(StagedSendData)` - adds a prepared message (`Body`, `Addresses`, and the same
  `IsAlert`/`Priority`/`Tag`/`SecurityLevel` fields a send normally carries) to the staged send screen instead of
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

### Auto Forward Controllers

```csharp
engine.Frames<MyFrame>(frame => frame
    .AutoForward<MyEscalationController>());

public sealed class MyEscalationController : IAutoForwardController<MyFrame>
{
    public string Name { get; } = "Escalation";
    public IReadOnlyList<string> Users { get; } = ["Alice", "Bob"];
    public bool Accepts(MyFrame frame) => frame.Priority >= 2;
}
```

Adds a custom auto forward controller, a type implementing `IAutoForwardController<TFrame>` instantiated through dependency injection like export and import formats, shown as an option in the auto forward screen to every user named in
its `Users` - each of them can open it there and maintain their own locally-saved target list, added to and removed
from freely, persisted between restarts (see `Docs/Components/ViewModels.md`, `IAutoForwardViewModel`). Whenever
this instance receives a message its `Accepts` accepts, it is forwarded automatically, unchanged in body,
to every user currently on that target list - no action needed from the user beyond having set the target list up
once. `Accepts` receives the frame typed, since the controller is stated on the frame configuration like `PrintCount` (only frames the message handler recognizes are auto forwarded); it is never consulted for a user with no access to the controller, or whose target list is
currently empty, so an inaccessible or unconfigured controller costs nothing per received message beyond that one
check. The controller's own name is never sent as one of the forwarded message's own addresses, even if a user
adds themselves to their own target list, avoiding a self-forward loop. Adding another controller with the same `Name`
(case-insensitive) replaces the earlier controller of that name in place; a new name adds another alongside it.

**Default:** no controllers; the auto forward screen's title bar button is hidden entirely for every user, since
no one has access to anything.

**Network file:** none; controllers are behavior, not settings. A target list, once a user sets it up, is
per-installation local data (see `Docs/Components/Data.md`, `AutoForwardTargetsEntity`), not config file state.

**Sample:** an `"Escalation"` controller, open to every Peer/Client scenario site, that matches any received alert
or `URGENT`-tagged message.

---

### Server Storage

```csharp
new UserInfo { Name = "SERVER1", Role = UserRole.Server, Children = ["CLIENT1", "CLIENT2"], StoresMessages = true };
```

A server user whose [user info](#user-info) sets `StoresMessages` stores messages: each keeps a copy of every message it routes (a message from one of its
children, or one forwarded to it by another server for its own children - never a receipt or a retrieval
request), once per message ID, in its own local database. It also answers a client's *retrieval request*, sent from
the client's RETRIEVE screen (shown in the title bar only for a `UserRole.Client` on a network where some server
stores; see `Docs/Components/ViewModels.md`, `IRetrieveViewModel`), by sending back a copy of each stored message
that fits. Every node states the same user info, since a client learns which servers store from it, and only a user whose
role is `Server` can store.

A retrieval request is not a special wire format. Like a receipt, it is a message of the configured message
type, recognized by the required `Retrieval` handler, whose `Create` builds the request from ordinary typed
inputs (a nullable `From` and `To`, and `Authors`, `Destinations` and `Ids` lists) and whose `IsValid` is false on every ordinary frame. A read receipt (sent when the recipient opens a message) is likewise handled by `ReadReceipt`, and a receive receipt (sent automatically when the recipient's node receives a message) by `ReceiveReceipt`; each handler carries the identifier of the message it is for. Nothing is packed into a string; the host's frame type carries each
criterion in its own field. The request is addressed to the storage server it is for and routed by a
server like any message (a server hands it on to the addressed server if that is not itself); a node that is not a
storage server, or a client or peer that receives one, never treats it as a received message. A stored message fits
when it satisfies every criterion that is set - its original sent time within the range (inclusive, as UTC
instants), its sender one of the authors, any of its addresses (as written, groups unexpanded) one of the
destinations, its ID one of the IDs - each list matching any of its entries, exactly and case-insensitively; an
unset criterion matches anything. Nothing restricts a request to the requester's own traffic: any user can retrieve
any stored message, whoever sent or received it.

Each found copy keeps the original's ID, sender, sent time, body, priority, tag and security level, but is
addressed to the requester alone and is never an alert: servers route purely by address list, so the copy has to
name the requester, and an old alert must not alarm again. It arrives as an ordinary received message, oldest
first; a client skips any whose ID its Inbox already holds, so retrieving what it already has does nothing. A
retrieved copy still counts as a received message for anything keyed on receipt (for example, "print received"
prints it). Storage needs the server's database, which the Client/Server UI opens; a server in Headless mode has none
and stores nothing.

**Default:** no server stores messages; the RETRIEVE button is never shown.

**Network file:** none; storage is behavior, not a setting.

**Sample:** `Server` (ClientServer scenario) and `Server1` (ServerCluster scenario) store; `Server2` does not.

---

### MicroGate Options

```csharp
engine.MicroGateOptions(new MicroGatePeerOptions { MaxInfoField = 1024, Link = new MicroGatePeerOptions().Link with { Crc = MicroGateCrc.Crc32Ccitt } });
```

The options every serial connection starts its MicroGate peer with: line encoding, CRC and clocking (`Link`, which must match the station at the far end of the cable), frame size, transmit window and retransmission timing. The options object is the MicroGate package's `MicroGatePeerOptions`, defaulting to the package defaults. The HDLC address is not an option; each serial `ConnectionPoint` supplies it, used as both this station's and the remote station's address.

**Default:** the MicroGate package defaults.

**Sample:** caps frames at 1024 bytes and the transmit window at 4 (a smaller frame is always safe with any remote station); see the MSMT section for its timeout adjustment.

---

### MSMT Certificates

```csharp
engine.TrustedAuthority("COMLINK-ROOT").ConnectionOptions(() => options);
```

MSMT peer authentication is mandatory - there is no unauthenticated mode. A user's `CertificateName` (on their [user info](#user-info)) is their certificate's subject name (CN): for the current user, the identity certificate to present; for any other user, the name a Server expects that user's certificate to carry (and the name [connection identification](Identification.md) matches against); `TrustedAuthority` names the certificate authority every peer's identity certificate must chain to. Both are looked up in the system certificate store (`CurrentUser` then `LocalMachine`, `StoreName.My`).

The MSMT options (identity certificate plus trusted authorities) used for both inbound and outbound session peer connections are built from those two by default, against the current user name (via `ICurrentUserProvider`). If no current user is registered yet, or either certificate can't be found in the store, building them throws `InvalidOperationException`; callers (`PeerService`, `ClientPeerService`, `ServerRoutingService`, `InterfaceService`) catch this at startup, log it, and simply don't start their listener, retried the next time the host restarts once a user and certificates are in place. `ConnectionOptions` replaces the whole policy, but for most customization needs stating `CertificateName`/`TrustedAuthority` instead is sufficient and does not require touching this security-sensitive logic at all. State `ConnectionOptions` only when you need custom certificate pinning, a non-store certificate source, or a different validation policy.

`MsmtOptions` adjusts the other MSMT settings (handshake, stall and response timeouts, TCP keep-alive, session lifetimes and keep-alive intervals) for every IP connection, inbound and outbound, including the interface listener. It takes a `MsmtConnectionOptions` object (handshake, stall and response timeouts, TCP keep-alive time, session lifetimes and keep-alive intervals, each defaulting to the MSMT package's own value) instead of the library's session options, since those require credentials the engine supplies. Its values are laid over the options built above, after `ConnectionOptions` and after the config file's certificate file override, and the credentials and hostname rule stay the engine's:

```csharp
engine.MsmtOptions(new MsmtConnectionOptions { HandshakeTimeout = TimeSpan.FromSeconds(20) });
```

**Default:** the certificate name is the user name unchanged; the trusted authority is `"COMLINK-ROOT"`; the MSMT options are otherwise left at the package defaults. Sample states a 15 second handshake timeout and a 60 second response timeout through `MsmtOptions`.

**Network file:** each user's entry may set `CertificateName` (the name of that user's certificate, defaulting to the user name); the file's `TrustedAuthorityCertificateName` names the trusted authority. The file's `CertificateStore` (a folder of `{USERNAME}.pfx` identity files) and `AuthorityCertificate` bypass the system store entirely, loading the running user's identity and the authority certificate directly from disk instead, set together or not at all; see [Config.md](Config.md). The options are built by `ConfiguredEngineController` for the current user, so the files are used even though they are not part of the wrapped controller.

**Sample:** none, deliberately; this is the one area Sample does not state. Replacing `ConnectionOptions` would duplicate ~60 lines of security-sensitive X.509 store-lookup logic, and stating the certificate names instead is sufficient for the vast majority of customization needs. Sample itself provisions no certificates of its own; `Scripts/Scenarios/` demonstrates the `CertificateStore`/`AuthorityCertificate` keys with `{USERNAME}.pfx` files checked in alongside each scenario's config.

---

### Network Topology

This instance's place in the peer/client/server networking topology, see [Peer.md](Peer.md#user-roles), is not stated on its own: it comes from the current user's [user info](#user-info) (`Role`, `PeerPoint`, `Parent`, and for a `Server` or `Relay` its `Children`, with the topology of every server in the cluster built from every `Server` user's info). A node is configured only with where it connects and listens, never which users it expects there; who is on the other end of a connection is worked out when it forms (see [Identification.md](Identification.md)).

The role selects the `IPeerService` implementation (`PeerService`/`ClientPeerService`/`ServerRoutingService`/`RelayPeerService`). `RolePeerService` is the one service the engine depends on; it creates that implementation when networking starts, after a user is installed, and forwards its events. `Restart()` (used when the network file is reloaded, see [Services.md](Services.md#networkreloadservice)) cancels and disposes the running implementation and creates another from the role as it is then, so a changed role takes effect without restarting the application; a changed port or set of outgoing points is applied in place by `Reconfigure()`, which leaves connections to unchanged points alone.

**Default:** `UserRole.Peer`, no links, no server users.

**Network file:** none of its own; the role, peer point, links and topology come from the users' entries. See [User Info](#user-info).

**Sample:** the roles, ports and points are on each site's user info; see [User Info](#user-info).

---

### Command-Line Overrides

```csharp
engine.CommandLineOverrides(true);  // honor --config and --user
engine.CommandLineOverrides(false); // ignore them (the default)
```

Determines whether command-line arguments may override where the [network configuration file](#network-configuration-file) and the running user come from: `--config <path>` names the file instead of `Config.json` in the working directory, and `--user <name>` names the user instead of `User.json` in the working directory. The files in the working directory are always read; only the arguments are affected. Resolved once, before anything else, from the recorded configuration (see [Bootstrap ordering](#network-configuration-file) above). Because of this ordering, a configuration must never depend on `NetworkConfig`.

**Default:** disallowed.

**Network file:** none possible; there is no field for whether the arguments are honored (that would be circular).

**Sample:** `SampleEngineConfiguration` allows them, so its scenario scripts can pass `--config` and `--user`. A host that wants them ignored (e.g. to lock down a deployment) simply does not call it, or passes `false`.

---

### External Systems

```csharp
engine.ExternalSystem(new MyExternalSystem()).ExternalServer(hub);
```

`ExternalSystem` adds an external system: a conduit to a system outside Comlink (a socket, a message queue, an HTTP long-poll, etc.) this instance communicates with, resolved once at startup by `ExternalSystemsService`. `ExternalServer` designates one of them as the exclusive upstream hub every outbound message is routed through instead of the normal peer network and every other external system (and adds it if it was not already added). See `Docs/Components/ExternalSystems.md` for the full contract and behavior; the shape here is deliberately terse since that doc covers it in depth. `IExternalSystem` is already non-generic, so no cast is involved. `ExternalSystemBase<TFrame>` is available as an optional convenience base class for implementing `IExternalSystem` with less boilerplate (the connect/poll/disconnect lifecycle, filtering, etc.) but is never required; any `IExternalSystem` implementation works.

Each external system is constructed directly by the configuration, not resolved through DI, so a logger it is given by the configuration comes from the bootstrap container and writes to none of the engine's logs (the engine's logging providers, e.g. `DailyFileLoggerProvider`, need the configuration's output for their log file location). `ExternalSystemsService` instead calls `IExternalSystem.AttachLogger` on each system, using its own `ILoggerFactory` from the running container, before starting it, see `Docs/Components/ExternalSystems.md`.

**Default:** no external systems; no gateway behavior.

**Network file:** none; a system-specific connection endpoint, credential, etc. belongs to each `IExternalSystem` implementation's own constructor, not a generic config schema, and which one is the exclusive upstream hub is likewise a host-code decision, not something a deployment config toggles.

**Sample:** none; `SampleEngineConfiguration` states no external system.

---

### Network Processor

```csharp
engine.Frames<MyFrame>(frame => frame.Processor<MyNetworkProcessor>());

public sealed class MyNetworkProcessor : INetworkProcessor<MyFrame>
{
    public void OnConnected(INetworkConnectedContext<MyFrame> context) { ... }
    public void OnDisconnected(INetworkDisconnectedContext<MyFrame> context) { ... }
    public void OnReceived(INetworkReceivedContext<MyFrame> context) { ... }
}
```

Runs host code in reaction to peer activity, independent of any UI: each method is synchronous (`void`) and runs on the thread that raised the event, so it should return quickly (`Send` itself is fire-and-forget); an exception it throws is logged and does not stop later events. `OnConnected`/`OnDisconnected` fire once
each time a user goes from unreachable to reachable over at least one live peer connection, or the other way
around (see [Peer.md](Peer.md#network-processor) for exactly what counts as "a live connection" for each
`UserRole`), handed an `INetworkConnectedContext<TFrame>` or `INetworkDisconnectedContext<TFrame>` whose `TargetUser` names that user; `OnReceived` fires
for every new (non-receipt) frame this instance receives, whether or not it is a message, handed an `INetworkReceivedContext<TFrame>` whose
`Frame` is that frame, typed as the host's own frame type - a processor never sees an internal representation of it. The processor is
stated on the frame configuration (`Frames`), so every context is generic over that type. Every processor context, network and initial exchange alike, extends the common `IEngineContext`; the network ones add `Send` through `INetworkContext<TFrame>`: `CurrentUser` (this instance's own installed user), `Users`/`ConnectedUsers`
(every known user, and the subset of them currently reachable, each as a `UserInfo` carrying its directly-assigned
group memberships but no real installation code), `IsConnected(userName)`, and `Send(TFrame frame)`, which originates a new
outbound frame: its frame ID and sender are overwritten before it is routed (mirroring
`IServiceConnection.SendMessage`'s own field handling), so a processor only needs to set the content fields, and make it a frame the message handler recognizes when the recipient should see it as a message. It is
fire-and-forget: a processor does not track or await the send, so it returns nothing, and a failed send is logged rather than
thrown back. Each processor method runs in the background and is not awaited by the engine;
an exception it throws is logged and never stops a later event from being handled. Each event gets one freshly-built context, so the
processor sees a consistent snapshot.

The processor is stated by type and instantiated through the running engine's dependency injection container, so its constructor can take services.

**Default:** no network processor; `EngineHooksService` (which runs it) does nothing when none is configured.

**Network file:** none; a processor is behavior, not a setting.

**Sample:** `SampleNetworkProcessor` sends a newly connected user a welcome message naming who else is currently online (`ConnectedUsers`), tells everyone still online when someone disconnects, and auto-replies `PONG` to any received message tagged `PING` - all via `Send` with the frame made a message, and none of it on a relay (`context.CurrentUser.Role`), which composes nothing, nor for a relay user, which nothing can be addressed to, so every reaction shows up as an ordinary message in the recipient's Inbox rather than a log line only visible from the host process's own console.

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

Not unit tested directly, for the same reason `ConnectionOptions`'s certificate store lookup below isn't: both are inherently environment- and OS-dependent, so a unit test could only meaningfully assert against whatever printers happen to be installed (and reachable) on the machine running the test - `Docs/Components/ViewModels.md`'s `PrintManagerViewModelTests` instead test the print queue's own logic (ordering, interruption, restart) against a mocked `IPrintDriver`/`IEngineController`.

---

## Client API

---

#### `IServiceConnection`

```csharp
event Func<MessageReceivedEvent, Task>? MessageReceived;
event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;
Task Connect(CancellationToken cancellation = default);
Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default);
Task<List<string>> GetUserNames(CancellationToken cancellation = default);
Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default);
Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default);
Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, bool isAlert = false, int priority = 0, string tag = "", CancellationToken cancellation = default);
Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
```

High-level API for host code to interact with the running Engine. Engine registers `DirectServiceConnection`, which calls Engine services in-process, in both Client and Headless mode - Headless mode acts as a normal peer client, just without a GUI. External programs instead plug into the message stream over the local interface listener (see [Interface.md](Interface.md)), which is unrelated to this interface.

Host applications resolve `IServiceConnection` from the container to send messages, install the user, and subscribe to inbound delivery events. `GetConnectedUsers` reports who is currently reachable over a live peer connection (`IPeerService.GetConnectedUsers`), unlike `GetUserNames`'s fixed configured directory - the same distinction [Connection & Message Hooks](#connection--message-hooks)'s `IEngineHookContext.Users`/`ConnectedUsers` expose to a hook, through its own simpler, synchronous surface rather than this interface directly.

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
