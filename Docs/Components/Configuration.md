# Engine Configuration

A host tells the engine how to run by implementing `IEngineConfiguration` and naming it to `Engine.Start<T>`, which constructs it through dependency injection. The engine calls `Configure` once, before anything else starts, handing it an `IEngineBuilder`; every call on the builder is optional except `Message<TMessage>`, and each returns the builder so a configuration reads as one fluent expression. The builder is the only public way to change what the engine does; everything it collects is read internally through `IEngineController`, which is not part of the public surface. See each area below for what it covers.

## Concept

Engine never reads environment variables, hardcodes paths, or calls host-specific APIs directly. Instead, every piece of external configuration and rule-based behaviour is either a call on `IEngineBuilder` or a field in the `--config` file. A host states only what differs from the engine's defaults:

```csharp
public sealed class MyEngineConfiguration : IEngineConfiguration
{
    public IEngineBuilder Configure(IEngineBuilder engine) => engine
        .Message<MyMessage>(message => message
            .Id(m => m.Id)
            // ...every other logical field...
            )
        .HomeText("Select a folder and entry to get started.")
        .Users("ALICE", "BOB");
}

await Engine.Start<MyEngineConfiguration>(args);
```

`EngineBuilder` (internal) implements `IEngineBuilder` by recording what it is told; nothing is interpreted while configuring. `EngineController` (internal) reads the recorded state through `IEngineController` and supplies the default for every setting the host left alone, which is what every service, ViewModel and repository in the engine depends on. Keeping the recording separate from the reading means a configuration can be checked as a whole (`Engine.Start` throws an `InvalidOperationException` naming a missing message type or unmapped field before any service starts), and that tests can replace a single behavior of the controller.

**A configuration describes non-config-file behavior only. It must never read `EngineConfigFile` itself, and it must never read an environment variable.** Where a setting has a corresponding `config.json` field, that override is applied separately, at the engine level, as a decorator layered on top of the controller built from the configuration (see [Config File Overlay](#config-file-overlay) below). This split keeps "what does this app do out of the box" (the configuration) and "what does `config.json` change about that" (`ConfiguredEngineController`, a decorator the engine owns) as two independent, separately testable concerns, and means a host is never tempted to reimplement `config.json` parsing just to add one small piece of non-config behavior.

**Dependency injection:** `Engine.Start<T>` builds a bootstrap container holding logging (`ILoggerFactory`, `ILogger<T>`) plus whatever the host's `configureServices` argument registers, and constructs `T` from it, so a configuration's constructor can take services. The bootstrap container is deliberately separate from the running engine's: the engine's own logging providers need the engine's configuration (for the log file location), so a configuration built from the running container could not take a logger without a cycle, and it has to be built before the container exists anyway, because it decides whether `--config` is read and so what the container is built from. The same `configureServices` registrations are applied again to the running engine's container, which is also where a host registers anything else it wants running alongside the engine (for example a hosted service that uses `IServiceConnection`); a service registered there therefore exists once in each container. The bootstrap container lives until the engine exits, since a configuration may have handed the builder functions that use what was injected.

## Config File Overlay

`EngineExtensions.UseEngine` registers `IEngineController` as a `ConfiguredEngineController` wrapping the `EngineController` built from the host's configuration. `ConfiguredEngineController` takes that controller, the loaded `EngineConfigFile`, and `ICurrentUserProvider`, and, member by member, returns the file's value when it is set and the wrapped controller's value otherwise; a member with no corresponding `config.json` field always delegates straight to the wrapped controller. It is registered explicitly, never by convention scanning.

**Bootstrap ordering:** whether the file is read at all is itself a setting (`ConfigFile`), so `Engine.Start` constructs the configuration and builds the `EngineBuilder` first, reads its `IsConfigFileEnabled`, and only then loads `EngineConfigFile` if allowed. There is no `config.json` field for it (that would be circular), and a configuration cannot depend on the file for the same reason. When the file is not allowed, `--config` is ignored entirely and every setting uses what the host stated or its default, as if the argument had never been passed.

## Settings

### Message Format

```csharp
engine
    .Message<MyMessage>(message => message
        .Id(m => m.Id)
        .Sender(...).Subject(...).Body(...).Addresses(...).SentAt(...)
        .ConfirmationId(...).IsAlert(...).Priority(...).Tag(...))
    .Packets<MyPacket>(packet => packet
        .PayloadId(...).Index(...).Count(...).PayloadLength(...).Data(...)
        .Size(16 * 1024).Window(1));
```

`Message` supplies the concrete message type used throughout the engine, on the wire (peer and interface connections) and in the database, and maps the engine's logical fields onto that type's real fields. Each mapping is a getter and a setter, so the engine reads and builds the host's message without ever assuming a field name or shape. Where the host's field has the type the engine wants, naming the property (`.Id(m => m.Id)`) is enough: the builder reads the member access from the expression and compiles a getter and setter from it once, when the configuration runs, so using it costs no more than writing them out (an init-only property works; a member that cannot be assigned, or an expression that is not a plain member access such as `m => m.Id.ToUpper()`, is refused at once with an error naming it). Where the types differ - the addresses, which the host stores in its own recipient shape and converts to and from `(string Name, AddressType Type, string Information)` tuples (the getter returns any sequence of them, the setter receives a list; `Information` is optional custom per-address instructions, e.g. `Deliver to Eastside Office` - a `(string Name, AddressType Type)` overload with no `Information` is also available for a host with no use for it), and a packet's data, which is a `ReadOnlyMemory<byte>` - the getter and setter are given explicitly. Every field must be mapped. The type must be LiteDB-serializable for storage, and must additionally satisfy whatever serializer is used for the wire, which by default is a `ProtobufNetworkSerializer` that builds only the message type (so `[ProtoContract]`/`[ProtoMember]` attributes). `Serializer` on the message builder replaces it, as long as every node this instance talks to (including its own interface connections) uses a matching one: Comlink never negotiates or advertises which format a payload used, so a mismatch deserializes garbage or throws rather than failing cleanly. `INetworkSerializer.Deserialize` is given only the bytes, so a custom serializer must make its format self-describing enough to rebuild the right type itself (the default wraps every payload in an outer envelope naming the type). `Create` replaces `new TMessage()` for building an empty message. The confirmation id and alert flag back the user-read confirmation and alert-message features (see [Peer.md](Peer.md#read-confirmation) and [Peer.md](Peer.md#alert-messages)); the priority backs [Message Composition](#message-composition) and the MSMT send priority, and the tag backs [Message Composition](#message-composition) too.

Packetization is off unless `Packets` is called. With it, payloads are broken into prioritized packets of the host's packet type and reassembled on the other side. The host only says how the five fields the engine needs are stored in its packet (payload id, packet index, packet count, payload length, data); all splitting, reassembly and priority scheduling is the engine's, so a host gets its own packet format and serialization without writing any packetization logic. The packet serializer defaults to a `ProtobufNetworkSerializer` that wraps every packet in an envelope naming its type, a fixed overhead per packet that a leaner custom serializer avoids. `Size` (default 16 KiB) is the largest serialized packet in bytes: the engine measures what the serializer makes of a packet to see how much payload fits, and refuses to start with an error in the log if none does. `Window` (default 1) is how many packets may be in flight over one connection at once, and must be at least 1. Every node must be configured alike, since neither side can tell whether the other packetizes. Interface connections are never packetized.

Internally the mappings become a `MessageMap` and a `PacketMap`, whose accessors take the message or packet as an `object`, since that is the boundary every other layer (LiteDB storage, MSMT wire serialization) operates at. A packet member of an engine that never called `Packets` throws `NotSupportedException`, because nothing calls them.

**Default:** none for the message, since the engine has no message DTO of its own; no packetization.

**Config file:** none; there is no `config.json` field for any message or packet member, since the whole point is that the engine does not know the DTO's shape.

**Sample:** `SampleEngineConfiguration` maps every logical field onto `SampleMessage`, a DTO with deliberately differently-named fields (`Id`, `Sender`, `Title`, `Text`, `Recipients`, ...) to demonstrate that the mapping, not any assumed field name or shape, is what the engine relies on, and turns packetization on with `SamplePacket` and the default size and window.

---

### App Settings

```csharp
engine.AppName("MyApp").AppVersion("1.2.3").DataPath("/data/app").KioskMode().HomeText("Welcome").WindowIcon(new Uri("avares://Host/icon.png"));
```

This app's own identity and top-level presentation: the display and data-folder name, the version shown in the title bar and the info popup, the root directory persistent state (LiteDB, user state, logs) is written under, whether the main window runs in kiosk mode (hides window chrome and restricts navigation), the placeholder text shown in the content area when no entry is selected, and the window icon.

**Default:** the name comes from the entry assembly name; the version is the entry assembly's `major.minor.build` version (`1.0.0` if it has none); the data path is `%APPDATA%\{AppName}`, computed from the name so a host stating only `AppName` gets a matching data folder; kiosk mode is off; the home text is `"HOME"`; the icon is the operating system's.

**Config file:** `DataFolder` overrides the data path when set: `null` uses it unchanged; an absolute path is used verbatim; an `@`-prefixed path is relative to it (see [Config.md](Config.md)), which is what lets a host state both `AppName` and a `DataFolder` at once and have them compose correctly. The other settings have no `config.json` field.

**Sample:** `SampleEngineConfiguration` states the home text and the window icon; everything else uses the default. Changing the app data path's default runtime behavior has caused real data loss in this project before, so Sample deliberately never states `DataPath` or `AppName`.

---

### User Identity

```csharp
engine.DebugUser("TEST1").UserCodes(code => code == "CODE1" ? new UserInfo { ... } : null);
```

How this instance's own local user identity is established: a fixed debug override that bypasses the normal `State.json` lookup, and resolving a user activation code (entered during installation) to a `UserInfo`. See `Services.UserService`.

**Default:** no debug user; the code `"CODE"` resolves to the user `"TEST"`.

**Config file:** `UserName` overrides the debug user when set. See [Config.md](Config.md). Code resolution has no `config.json` field.

**Sample:** `SampleEngineConfiguration` states one hard-coded install code per site used across `Scripts/Scenarios/` (`CLIENT1`, `CLIENT2`, `SERVER`, `SERVER1`, `SERVER2`, `PEER1`, `PEER2`, each resolving to the like-named user) instead of the default's one; it states no debug user, so `config.json`'s `UserName` applies on its own.

---

### User Directory

```csharp
engine.Users("ALICE", "BOB").Group("OPS", "ALICE", "BOB").UserData("ALICE", new Dictionary<string, string> { ["role"] = "clerk" });
```

Everything the engine knows about addressable users and groups: the names used for the destination auto-complete in the draft editor and for [connection identification](Identification.md), group membership for address expansion (members may be user names or other group names, enabling nested hierarchies), and the app-specific data attached to a user. A user is only a name here: nothing says where a user is reached, since that is worked out when a connection forms. `UserData` attaches whatever extra information a host wants to a user (a role, a station, a display name) as string keys and values, either by name (merging with anything already stated for that user) or as a lookup function for every user, with the by-name data winning; the engine does not interpret it, it travels with the user's `UserIdentity` to the host's own hooks.

When a message is sent to a group, the Engine records which addressed groups each user was reached through. The sent message view shows this context, e.g. `USER-A (OPS)`, so the operator can see which group membership drove delivery.

**Default:** no known users, groups, or data.

**Config file:** merges `config.json`'s `UserGroups` over the stated groups (a file entry replaces a same-named group; groups only stated in code still pass through); unions the stated names with the file's `Users` and `UserGroups` keys, deduplicated and sorted; and merges the `Data` of the matching `Users` entry over the stated data (the file winning on a key conflict). See [Config.md](Config.md).

**Sample:** `SampleEngineConfiguration` states three built-in user names matching its codes; the file's names are still unioned in.

---

### Connection Identification

```csharp
engine
    .Identify(connection => new UserIdentity { Name = ... })
    .ConnectionMessage<MyHello>(connection => new MyHello { ... })
    .ConnectionResponse<MyWelcome>(connection => new MyWelcome { ... })
    .ConnectionSerializer(serializer);
```

Who is on the other end of a connection, decided as the connection forms. `Identify` is handed a `ConnectionInfo` (for IP the remote host, port and certificate names, for serial the port and address, and the connection message and response when those are configured) and returns a `UserIdentity`, or `null` to let the engine decide. `ConnectionMessage` (with `ConnectionResponse` for an optional reply) makes the node that opens a connection send a message first, and the receiver answer with a response; identification then sees both, so a host can carry a user name, a station or any other detail across the wire. The serializer defaults to one that builds only the two types. The exchange, the framing it puts on the wire, and what the engine does by default are described in [Identification.md](Identification.md). Every node on a network must be configured alike, as with packetization.

**Default:** the hook returns `null` (the engine identifies an IP connection by its certificate name and a serial connection by its port name), and no connection message or response is stated, so no exchange takes place.

**Config file:** none, because these are behavior, not settings.

**Sample:** none.

---

### Ports

```csharp
engine.PeerPort(50021).InterfacePort(50020);
```

TCP port numbers for the peer listener (the one place a node accepts IP connections) and the local interface listener (always active, in every mode; see [Interface.md](Interface.md)).

| Port | Default |
|------|---------|
| `PeerPort` | `50021` |
| `InterfacePort` | `50020` |

**Config file:** `PeerPort` and `InterfacePort` override what is stated, field by field, when set. See [Config.md](Config.md).

**Sample:** none; the default plus the automatic config file overlay already cover every genuinely useful case.

---

### Alert Settings

```csharp
engine.AlertLabel("ALERT").AlarmDuration(TimeSpan.FromSeconds(30)).QuickConfirmation().ComposeAlerts();
```

Configuration for the alert-message feature in Client mode: the title bar's alarm box text (also the draft editor's alert checkbox label, so both surfaces always show the same word for "alert"), how long the alarm sound plays before automatically stopping (resetting whenever a new alert arrives while already alarming), whether click/Space/Enter quick confirmation is enabled, and whether the draft editor shows its alert checkbox at all (disabling only affects local origination; the app can still receive and alarm on a peer-originated alert). See [Peer.md](Peer.md#alert-messages) and `Docs/Components/ViewModels.md`. Actually playing the alarm sound is real platform behavior, not configuration, see [`IAlertSoundPlayer`](#ialertsoundplayer-not-configurable) below.

**Default:** `"ALERT"` / 30 seconds / on / on.

**Config file:** `AlertText`, `AlarmSoundSeconds`, `QuickConfirmationEnabled` and `ComposeAlertsEnabled` override what is stated, field by field, when set. See [Config.md](Config.md).

**Sample:** none; the default plus the automatic config file overlay already cover every genuinely useful case.

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

**Config file:** `MessageTagsEnabled` and `MessageTagLabel` override what is stated, field by field, when set. See [Config.md](Config.md). Priorities and blocked combinations have no `config.json` field.

**Sample:** `SampleEngineConfiguration` states three priority levels (`"Low"`/`"Medium"`/`"High"`, values 0/1/2) instead of the default's one, and demonstrates both blocked-combination kinds: the `"SPAM"` tag is blocked regardless of priority, and `High` priority is blocked regardless of tag. Unlike Sample's other settings, the blocked combinations deliberately change default behavior from the engine's permissive "no blocks" default, since that is the only way to usefully demonstrate that part of the configuration.

---

### Address Type Labels

```csharp
engine.AddressTypeLabel(AddressType.External, "OUTSIDE");
```

Overrides the display label shown for one address type, everywhere it appears in the UI: the address type picker in the draft editor, the per-address badge next to each recipient, and the message view's section headers. Each call replaces the label for exactly the type given; every other type keeps its own current label (its own override, if stated, or the default). The underlying `AddressType` value itself never changes - overriding a label only changes what the user reads, not how an address is stored, routed, or mapped through `Message<TMessage>.Addresses`.

**Default:** the enum name itself (`"To"`, `"Cc"`, `"External"`).

**Config file:** none; address type labels have no `config.json` field.

**Sample:** `SampleEngineConfiguration` renames `External` to `"OUTSIDE"`, matching the `Kind` vocabulary `SampleRecipient` already uses for it (see [Message Format](#message-format)).

---

### Security Levels

```csharp
engine
    .SecurityLevels(("PUBLIC", "#2E7D32"), ("INTERNAL", "#1565C0"), ("RESTRICTED", "#C62828"))
    .UserSecurityLevel("ALICE", "INTERNAL")
    .UserSecurityLevel(userName => directory.LevelFor(userName));
```

Defines the ordered set of security levels a message may be sent at (`Message<TMessage>.SecurityLevel`, see [Message Format](#message-format)): each a display name paired with the hex color shown for it in the title bar's banner (`SecurityLevelBanner`, replacing the fixed orange "DEBUG" banner every user used to see). Order matters: each level ranks higher than the one stated before it, so the last one given is the most senior. `UserSecurityLevel` assigns individual users to a level by name, or (the `Func<string, string>` overload) replaces the per-user lookup wholesale, the same additive-versus-wholesale pattern as `UserData`; a user with no assignment runs at the lowest configured level.

A destination user may only receive a message whose security level their own assigned level ranks at or above: `MessageRoutingService.Route` drops any lower-ranked destination before sending, and the draft editor's security level picker only ever offers the sending user's own level and lower, so a message can be deliberately declassified but never sent above the sender's own clearance. Turning the feature off entirely is just leaving `SecurityLevels` empty (the default): every message maps to an empty security level, the picker is hidden, and no destination is ever blocked for lacking one.

**Default:** no security levels; the feature is off.

**Config file:** none; security levels have no `config.json` field.

**Sample:** `SampleEngineConfiguration` defines three placeholder levels (`PUBLIC`, `INTERNAL`, `RESTRICTED`) and assigns them by site: `Peer1`/`Peer2` run at `PUBLIC`, `Client1`/`Client2` at `INTERNAL`, and the server sites (`Server`, `Server1`, `Server2`) at `RESTRICTED`.

---

### Print Policy

```csharp
engine.PrintReceived().PrintCount<MyMessage>(message => message.IsAlert ? 2 : 1);
```

The print manager's automatic "print received" behavior: whether its toggle starts enabled, automatically adding every received message to the print queue from the moment the app starts (the user can still toggle it at any time), and how many times each received message is added to the print queue while it is (`0` to not print it, `1` once, `2` for two copies, and so on). Consulted once per received message via `IEntryService.MessageInserted`. `PrintCount` is generic over the host's message type so the rule receives the message typed; the engine casts once on the host's behalf.

**Default:** off / `1` for every message.

**Config file:** `PrintReceivedEnabled` overrides what is stated when set. See [Config.md](Config.md). The print count has no `config.json` field.

**Sample:** `SampleEngineConfiguration` states a print count that prints an alert message twice and every other received message once, demonstrating a rule that inspects the message itself; "print received" uses the default.

---

### Deletion Policy

```csharp
engine.CanDelete(folder => folder is FolderType.Drafts or FolderType.Notes);
```

Whether the user can delete entries in a given root folder type (`FolderType.Inbox`/`Outbox`/`Drafts`/`Notes`/`Activity`). Consulted by `EntryBarViewModel.DeleteEntry` before deleting; the active folder's `RootType` is passed straight through, and when the rule returns `false`, `DeleteEntry` is a silent no-op (the entry stays in the data store and in the list) rather than throwing. `LoadFolder` also caches the result on `EntryBarViewModel.CanDeleteEntries`, which drives whether the entry list's right-click "Delete" context menu item is shown at all. Draft and note editors also show a DELETE button, gated on the same rule for `FolderType.Drafts`/`FolderType.Notes` when the editor is created. See `Docs/Components/ViewModels.md`.

**Default:** allowed for every folder type; deletion is unrestricted unless a host locks down specific folders.

**Config file:** none; a fixed, code-level rule, not a per-deployment setting.

**Sample:** `SampleEngineConfiguration` allows deletion only in `FolderType.Drafts` and `FolderType.Notes`, protecting Inbox, Outbox, and Activity entries.

---

### Export Formats

```csharp
engine.ExportFormat(
    "CSV",
    async (entry, stream, cancellation) =>
    {
        if (entry is MessageExportData message)
        {
            await using StreamWriter writer = new(stream, leaveOpen: true);
            await writer.WriteLineAsync($"{message.SentAt:O},{message.FromUser},{message.Subject}");
        }
    },
    entryTypes: type => type is FolderType.Inbox or FolderType.Outbox);
```

Adds a custom export format, shown as an option in the export screen's format picker alongside the built-in JSON
format (see `Docs/Components/ViewModels.md`, `IExportViewModel`). The serializer is handed one entry - a
`MessageExportData`, `DraftExportData`, `NoteExportData`, or `ActivityLogExportData` depending on which root
folder type it came from, the exact same public DTOs the engine's own built-in JSON export writes - and a stream
to write it to; a host that only handles some entry types checks the runtime type (as above) or narrows what it
ever receives at all with `entryTypes`. `entryTypes`, when stated, also determines which entries `ExportService.Export`
leaves out of the archive entirely for this format, so an excluded entry's data is never touched, not merely
unwritten. Each entry's file inside the export zip gets an extension derived from the format's own name (lowercased,
stripped to letters and digits - `"CSV"` above becomes `.csv`), so files stay recognizable to whatever tool a host
exports for. Calling this again with the same name (case-insensitive) replaces that format; a new name adds another.

A package written with a custom format is one-way: only a package written with the built-in JSON format can be
read back in by the import screen (see `Docs/Components/Services.md`, `ImportService`) - a custom format is for
producing something a tool outside Comlink consumes, not for round-tripping through this app.

**Default:** no custom formats; the export screen offers only the built-in JSON format.

**Config file:** none; formats are behavior, not settings.

**Sample:** a `"Text"` format writing each message, draft, or note as readable plain text, restricted (via
`entryTypes`) to Inbox, Outbox, Drafts, and Notes - Activity's structured entries are left to the built-in JSON
format instead.

---

### Import Formats

```csharp
engine.ImportFormat(
    "CSV",
    async (stream, context, cancellation) =>
    {
        using StreamReader reader = new(stream, leaveOpen: true);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellation)) is not null)
        {
            string[] parts = line.Split(',', 3);
            if (parts.Length < 3) { continue; }
            context.AddStagedSend(new StagedSendData { Subject = parts[0], Body = parts[2], Addresses = [new AddressRequest { UserName = parts[1] }] });
        }
    },
    stagedSendMode: StagedSendMode.Sequential,
    stagedSendDelay: TimeSpan.FromSeconds(1));
```

Adds a custom import format, shown as an option in the import screen's format picker alongside the built-in
package format (see `Docs/Components/ViewModels.md`, `IImportViewModel`). Selecting it changes which files the
screen finds on the source drive - not `IExportService.PackageExtension` packages, but files whose extension
matches this format's own name-derived extension (the same derivation an `ExportFormat` entry's file extension
uses - `"CSV"` above becomes `.csv`). Choosing one of those files and importing it opens it as a plain stream and
hands `read` the stream plus an `IImportFormatContext`, unlike the built-in format's zip archive of typed entries.

The context turns whatever the reader finds into real changes:

- `AddMessage(MessageExportData)`, `AddDraft(DraftExportData)`, `AddNote(NoteExportData)` - insert a new entry
  using the exact same public DTOs a custom export format's serializer receives (see above), applying the same
  rules the built-in package format already applies to its own entries: a message matching an existing one (same
  ID, direction, and date) is skipped, and a draft/note matching an existing entry's name prompts the user through
  the same Keep Existing / Overwrite / Overwrite All dialog - a reader only builds the DTO, never reimplements
  matching or conflict prompting.
- `AddStagedSend(StagedSendData)` - adds a prepared message (`Subject`, `Body`, `Addresses`, and the same
  `IsAlert`/`Priority`/`Tag`/`SecurityLevel` fields a send normally carries) to the staged send screen instead of
  writing anything to the database directly; nothing is sent until the user reviews the batch there and presses
  its own send button.

`stagedSendMode` and `stagedSendDelay` state how that later send-all processes everything this format ever adds
through `AddStagedSend`: `StagedSendMode.Sequential` (the default) sends one at a time, in the order added,
pausing `stagedSendDelay` between each when it is stated; `StagedSendMode.Simultaneous` sends every one at once.
Calling `ImportFormat` again with the same name (case-insensitive) replaces that format; a new name adds another.

**Default:** no custom formats; the import screen offers only the built-in package format.

**Config file:** none; formats are behavior, not settings.

**Sample:** a `"CSV"` format reading `Subject,User,Body` lines and staging one send per line, sent one at a time
a second apart (`StagedSendMode.Sequential`, `stagedSendDelay: TimeSpan.FromSeconds(1)`).

---

### Auto Forward Controllers

```csharp
engine.AutoForwardController<MyMessage>(
    "Escalation",
    users: ["Alice", "Bob"],
    filter: message => message.Priority >= 2);
```

Adds a custom auto forward controller, shown as an option in the auto forward screen to every user named in
`users` - each of them can open it there and maintain their own locally-saved target list, added to and removed
from freely, persisted between restarts (see `Docs/Components/ViewModels.md`, `IAutoForwardViewModel`). Whenever
this instance receives a message `filter` accepts, it is forwarded automatically, unchanged in subject and body,
to every user currently on that target list - no action needed from the user beyond having set the target list up
once. `filter` receives the message as an instance of the configured message type, the same as
`PrintCount<TMessage>`; it is never consulted for a user with no access to the controller, or whose target list is
currently empty, so an inaccessible or unconfigured controller costs nothing per received message beyond that one
check. The controller's own name is never sent as one of the forwarded message's own addresses, even if a user
adds themselves to their own target list, avoiding a self-forward loop. Calling this again with the same name
(case-insensitive) replaces the earlier controller of that name in place; a new name adds another alongside it.

**Default:** no controllers; the auto forward screen's title bar button is hidden entirely for every user, since
no one has access to anything.

**Config file:** none; controllers are behavior, not settings. A target list, once a user sets it up, is
per-installation local data (see `Docs/Components/Data.md`, `AutoForwardTargetsEntity`), not config file state.

**Sample:** an `"Escalation"` controller, open to every Peer/Client scenario site, that matches any received alert
or `URGENT`-tagged message.

---

### MSMT Certificates

```csharp
engine.CertificateName(user => $"COMLINK-{user}").TrustedAuthority("COMLINK-ROOT").ConnectionOptions(() => options);
```

MSMT peer authentication is mandatory - there is no unauthenticated mode. `CertificateName` maps a user name to its certificate's subject name (CN): for the current user, the identity certificate to present; for any other user, the name a Server expects that user's certificate to carry (and the name [connection identification](Identification.md) matches against); `TrustedAuthority` names the certificate authority every peer's identity certificate must chain to. Both are looked up in the system certificate store (`CurrentUser` then `LocalMachine`, `StoreName.My`).

The MSMT options (identity certificate plus trusted authorities) used for both inbound and outbound session peer connections are built from those two by default, against the current user name (via `ICurrentUserProvider`). If no current user is registered yet, or either certificate can't be found in the store, building them throws `InvalidOperationException`; callers (`PeerService`, `ClientPeerService`, `ServerRoutingService`, `InterfaceService`) catch this at startup, log it, and simply don't start their listener, retried the next time the host restarts once a user and certificates are in place. `ConnectionOptions` replaces the whole policy, but for most customization needs stating `CertificateName`/`TrustedAuthority` instead is sufficient and does not require touching this security-sensitive logic at all. State `ConnectionOptions` only when you need custom certificate pinning, a non-store certificate source, or a different validation policy.

**Default:** the certificate name is the user name unchanged; the trusted authority is `"COMLINK-ROOT"`.

**Config file:** `PeerCertificateName` overrides the certificate name for the current user only (`null` falls back to what is stated; an explicit name is used as-is). It names this node's own certificate, so every other user still resolves through what is stated, since applying it to them too would make a Server expect every connecting user to present this node's certificate name; `TrustedAuthorityCertificateName` overrides the trusted authority the same way. See [Config.md](Config.md). The options are built by `ConfiguredEngineController` from its own overridden names rather than delegated, so they reflect both overrides even though they have no `config.json` field of their own. Separately, `PeerCertificateFile` and `TrustedAuthorityCertificateFile` bypass the system store entirely, loading the identity and authority certificates directly from disk instead, set together or not at all; see [Config.md](Config.md).

**Sample:** none, deliberately; this is the one area Sample does not state. Replacing `ConnectionOptions` would duplicate ~60 lines of security-sensitive X.509 store-lookup logic, and stating the certificate names instead is sufficient for the vast majority of customization needs. Sample itself provisions no certificates of its own; `Scripts/Scenarios/` demonstrates the `PeerCertificateFile`/`TrustedAuthorityCertificateFile` config fields with certificate files checked in alongside each scenario's config.

---

### Network Topology

```csharp
engine.Role(NodeRole.Server).OutgoingPoint(new ConnectionPoint { IpAddress = "10.0.0.2", Port = 50021 }).Server("SERVER-A", "CLIENT-A1", "CLIENT-A2");
```

This instance's place in the peer/client/server networking topology, see [Peer.md](Peer.md#node-roles). `Role` selects one of `NodeRole.Peer`/`Client`/`Server`; `OutgoingPoint` adds an IP host and port this node dials or a serial port it opens, each kept connected by a heartbeat (a `Client` connects to the first only), while the port it listens on is `PeerPort`; `Server` adds a server to the topology a `Server` routes with, and the child clients it owns, every server in the cluster and not just the local one (unused outside `Server`). A node is configured only with where it connects and listens, never with which users it expects there: the topology names who belongs where but not how to reach them, and a connection is matched to a user by [identification](Identification.md).

The role is what selects the `IPeerService` implementation (`PeerService`/`ClientPeerService`/`ServerRoutingService`), when the service is first resolved.

**Default:** `NodeRole.Peer`, no outgoing points, no server users.

**Config file:** `NodeRole` overrides the role when set and recognized (`"Peer"`/`"Client"`/`"Server"`, case-insensitive; an unrecognized value falls back to what is stated rather than forcing `Peer`); `OutgoingPoints` replaces the stated points when it lists any; and `ServerUsers` merges over the stated servers (a file entry replaces a same-named server; servers only stated in code still pass through). See [Config.md](Config.md).

**Sample:** none; the default plus the automatic config file overlay already cover every genuinely useful case.

---

### Config File

```csharp
engine.ConfigFile();
```

Determines whether the `--config` command-line argument is honored at all. Resolved once, before anything else, from the recorded configuration (see [Bootstrap ordering](#config-file-overlay) above). Because of this ordering, a configuration must never depend on `EngineConfigFile`. When it is off, `--config` is ignored entirely and every setting uses what the host stated or its default, as if the argument had never been passed.

**Default:** off.

**Config file:** none possible; there is no `config.json` field for whether `config.json` is read (that would be circular).

**Sample:** `SampleEngineConfiguration` turns it on, so Sample honors a `--config` argument. A host that wants `--config` ignored (e.g. to lock down a deployment) simply does not call it.

---

### External Systems

```csharp
engine.ExternalSystem(new MyExternalSystem()).ExternalServer(hub);
```

`ExternalSystem` adds an external system: a conduit to a system outside Comlink (a socket, a message queue, an HTTP long-poll, etc.) this instance communicates with, resolved once at startup by `ExternalSystemsService`. `ExternalServer` designates one of them as the exclusive upstream hub every outbound message is routed through instead of the normal peer network and every other external system (and adds it if it was not already added). See `Docs/Components/ExternalSystems.md` for the full contract and behavior; the shape here is deliberately terse since that doc covers it in depth. `IExternalSystem` is already non-generic, so no cast is involved. `ExternalSystemBase<TMessage>` is available as an optional convenience base class for implementing `IExternalSystem` with less boilerplate (the connect/poll/disconnect lifecycle, filtering, etc.) but is never required; any `IExternalSystem` implementation works.

Each external system is constructed directly by the configuration, not resolved through DI, so a logger it is given by the configuration comes from the bootstrap container and writes to none of the engine's logs (the engine's logging providers, e.g. `DailyFileLoggerProvider`, need the configuration's output for their log file location). `ExternalSystemsService` instead calls `IExternalSystem.AttachLogger` on each system, using its own `ILoggerFactory` from the running container, before starting it, see `Docs/Components/ExternalSystems.md`.

**Default:** no external systems; no gateway behavior.

**Config file:** none; a system-specific connection endpoint, credential, etc. belongs to each `IExternalSystem` implementation's own constructor, not a generic config schema, and which one is the exclusive upstream hub is likewise a host-code decision, not something a deployment config toggles.

**Sample:** `SampleEngineConfiguration` adds a single `SampleExternalSystem`, demonstrating the conduit pattern with a self-contained simulated connection (see `Docs/Components/ExternalSystems.md`); it does not designate an `ExternalServer`, since a single-external-system setup has nothing else to designate it as exclusive relative to.

---

### Connection & Message Hooks

```csharp
engine
    .OnUserConnected(context => context.SendMessage(new MyMessage { ... }))
    .OnUserDisconnected(context =>
    {
        foreach (UserInfo user in context.ConnectedUsers) { context.SendMessage(new MyMessage { ... }); }
    })
    .OnMessageReceived(context =>
    {
        MyMessage message = (MyMessage)context.Message;
        if (message.Body.Contains("ping", StringComparison.OrdinalIgnoreCase))
        {
            context.SendMessage(new MyMessage { ... });
        }
    });
```

Runs host code in reaction to peer activity, independent of any UI: `OnUserConnected`/`OnUserDisconnected` fire once
each time a user goes from unreachable to reachable over at least one live peer connection, or the other way
around (see [Peer.md](Peer.md#connection--message-hooks) for exactly what counts as "a live connection" for each
`NodeRole`), handed an `IUserConnectionHookContext` whose `TargetUser` names that user; `OnMessageReceived` fires
for every new (non-confirmation) message this instance receives, handed an `IMessageReceivedHookContext` whose
`Message` is that message, as an instance of the configured message type - the same as anywhere else a host's own
message type crosses the engine boundary, a hook never sees an internal representation of it. Both context types
extend the common `IEngineHookContext`: `CurrentUser` (this instance's own installed user), `Users`/`ConnectedUsers`
(every known user, and the subset of them currently reachable, each as a `UserInfo` carrying its directly-assigned
group memberships but no real installation code), `IsConnected(userName)`, and two ways to originate new outbound
traffic:

- `SendMessage(object message)` - `message` must be an instance of the configured message type; its message ID,
  sender, and sent time are overwritten before it is routed (mirroring `IServiceConnection.SendMessage`'s own
  field handling), so a hook only needs to set the content fields.
- `SendPacket(object packet, params IEnumerable<string> userNames)` - sends a raw, already-built packet (an
  instance of the configured packet type; throws if none is configured, or if the packet type doesn't match)
  directly to each named user, bypassing the normal packetization/reassembly a full message goes through and
  routing's address expansion entirely, since a packet carries no address list of its own.

Both are fire-and-forget: a hook does not track or await the send it makes, so neither returns anything, and a
failed send is logged rather than thrown back into the hook. Calling a builder method more than once adds another
hook rather than replacing the last one: every hook added for an event runs, in the order added, each time it
fires, all handed the same context instance so they see a consistent snapshot. A hook that throws is logged and
never stops the rest, of that firing or a later one, from running.

**Default:** no hooks of any kind; `EngineHooksService` (which runs them) does nothing when none are configured.

**Config file:** none; hooks are behavior, not settings.

**Sample:** `SampleEngineConfiguration` sends a newly connected user a welcome message naming who else is currently online (`ConnectedUsers`), tells everyone still online when someone disconnects, and auto-replies `PONG` to any received message tagged `PING` - all via `SendMessage`, so every hook's effect shows up as an ordinary message in the recipient's Inbox rather than a log line only visible from the host process's own console.

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

Unlike the settings above, this is real OS-level behavior, not configuration or rules, so it is not part of the engine configuration and a host cannot replace it: it lives in `Core/src/Internal/Devices/`, and Engine always provides real behavior for it directly, the same way it always provides real behavior for alarm sound playback (see `IAlertSoundPlayer`, above) rather than leaving either to a host. Printer discovery is a genuine operating-system resource (like external drives, above), not app-specific configuration, and driving a printer line-by-line with real completion confirmation only makes sense against the operating system's own print spooler, not a bundled library. None of the four members has a `config.json` field.

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
Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, bool isAlert = false, int priority = 0, string tag = "", CancellationToken cancellation = default);
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
