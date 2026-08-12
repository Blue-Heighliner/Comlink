# Architecture Overview

Comlink is a peer-to-peer messaging system. The solution has three projects:

| Project | Description |
|---------|-------------|
| **Engine** | The whole engine, in one library — networking, data, services, ViewModels, and the Avalonia UI layer (Views, Themes, converters). The only project that depends on Avalonia. |
| **Sample** | Host application. References Engine. |
| **Tests** | xUnit tests. References Engine. |

```mermaid
graph LR
    Engine["Engine\n(core library + UI layer)"]
    Sample["Sample\n(host app)"]
    Tests["Tests\n(xUnit)"]
    Engine --> Sample
    Engine --> Tests
```

## Modes

The engine runs in one of two modes selected at startup via `EngineMode`:

| Mode | Description |
|------|-------------|
| `Client` | Desktop UI via Engine's Avalonia layer. Includes LiteDB persistence, all ViewModels, and a peer listener for receiving messages. |
| `Headless` | Runs as a normal peer client — same LiteDB persistence, same `IServiceConnection` — but with no UI. |

Both modes run `PeerService` to accept and send peer-to-peer messages over [OFT](Oft.md), and both always run `InterfaceService`, hosting the local interface listener for external programs — see [Interface.md](Interface.md). The interface listener is not tied to Headless mode; it is active regardless of which mode the engine runs in.

Headless mode does not remove the Avalonia dependency — Engine is a single assembly, so Avalonia and its packages are always loaded regardless of mode. `HeadlessMode` only controls whether `EngineApplication` shows a window (`AppBuilder...StartWithClassicDesktopLifetime`) or runs the `IHost` directly with no UI; it is not a build-time or package-level option.

## Component Map

```
Engine/src/
├── Control/       DI interfaces — all external configuration points
├── Data/          LiteDB persistence (Client and Headless modes)
│   ├── Entities/  LiteDB document models
│   └── Repositories/
├── Interface/      Local interface listener (always active) — see Interface.md
├── Logging/        Daily file logger + activity log writer
├── Models/         Shared DTOs (UserInfo, UserEndpoint, UserState, Folder, etc.)
├── Peer/           P2P networking over OFT — send and receive messages between nodes
├── Services/       Business logic
├── ViewModels/     MVVM layer — mostly Avalonia-agnostic (primitive types, custom interfaces),
│                   except the Avalonia-specific converters and TextDocumentBodyDocument(Factory)
├── Themes/         Avalonia dark theme resources
└── Views/          Avalonia XAML + code-behind (Client mode only; [ExcludeFromCodeCoverage])
```

## Key Flows

### Sending a message (Client mode)

```mermaid
sequenceDiagram
    participant DVM as DraftViewModel
    participant SC as IServiceConnection
    participant MRS as MessageRoutingService
    participant SL as IUserLocator
    participant PS as PeerService
    participant RP as Remote IOftPeer
    DVM->>SC: SendMessage
    SC->>MRS: Route
    MRS->>SL: GetEndpoint (per recipient)
    MRS->>PS: Send (IMessageFormat.MessageType instance, tagged for delivery status)
    PS->>RP: OFT send
    RP-->>PS: OFT Acknowledged
    PS-->>MRS: DeliveryStatusChanged (Confirmed)
    MRS-->>SC: DeliveryStatusChanged event
    SC-->>DVM: DeliveryStatusChanged event
```

### Receiving a message (Client mode)

```mermaid
sequenceDiagram
    participant RN as Remote Node
    participant PS as PeerService
    participant DSC as DirectServiceConnection
    participant MVM as MainViewModel
    participant ES as EntryService
    RN->>PS: OFT send (IMessageFormat.MessageType instance)
    PS-->>DSC: MessageDelivered event
    DSC-->>MVM: MessageReceived event
    MVM->>ES: StoreIncomingMessage (ReadStatus=Received)
    MVM->>MVM: Prepend to EntryBar if Inbox active
```

When the user opens that Inbox message, `ContentAreaViewModel` calls `IServiceConnection.MarkMessageRead`, which transitions `ReadStatus` to `Read` and sends a user-read confirmation back to the sender — see [Peer.md](Peer.md#read-confirmation). If the message is an alert (`IMessageFormat.GetIsAlert`), `AlertViewModel` also alarms (title bar box + sound) until it — and every other pending alert — is read; see `Docs/ViewModels.md`.

### Receiving a message (via an interface connection)
1. Remote node sends to this user's `PeerService` over OFT.
2. `InterfaceService` (subscribed to `PeerService.MessageDelivered`) mirrors the message, unmodified, to every currently connected interface connection.
3. Any connected external program receives it directly over its own OFT connection — see [Interface.md](Interface.md). This happens in both Client and Headless mode.

### Sending a message from an interface
1. An external program sends an instance of `IMessageFormat.MessageType` on its interface connection.
2. `InterfaceService` reads `Subject`/`Body`/`Addresses` from it via `IMessageFormat` and calls `MessageRoutingService.Route` with this user's own installed name as `fromUser` — exactly as if the user itself had composed the message. This happens in both Client and Headless mode.

### Exporting and importing entries (Client mode)

The title bar's EXPORT/IMPORT buttons back up and restore messages, drafts, notes, and activity logs to/from an external drive (USB, etc.), independent of the peer network. See `Docs/ViewModels.md` (`IExportViewModel`/`IImportViewModel`) and `Docs/Services.md` (`ExportService`/`ImportService`) for the full behavior — conflict resolution, activity log merging, the `.export.zip` package format, and how the export/import screens keep their own state (including an in-progress operation) while the user navigates the rest of the app.

```mermaid
sequenceDiagram
    participant EXV as ExportViewModel
    participant EXS as ExportService
    participant Drive as External Drive
    participant IMV as ImportViewModel
    participant IMS as ImportService
    EXV->>EXS: GetAllEntryRefs / SelectedEntries
    EXS->>Drive: write {name}.export.zip (one JSON file per entry)
    IMV->>IMS: GetPackages(drive)
    IMS->>Drive: list *.export.zip
    IMV->>IMS: Import(package, resolveConflict)
    IMS->>Drive: read entries
    IMS-->>IMV: ImportConflict (per draft/note name clash)
    IMV-->>IMS: DraftNoteConflictResolution
```

## Startup Sequence

`EngineExtensions.UseEngine()` registers the core services. For Client mode, `EngineUiExtensions.UseEngineUi()` additionally registers `MainWindow` and overrides `IBodyDocumentFactory`. `EngineHost` (an `IHostedService`) runs at startup:

```mermaid
sequenceDiagram
    participant H as Host
    participant EE as EngineExtensions
    participant EA as EngineUiExtensions
    participant EH as EngineHost
    participant SS as UserService
    participant PS as PeerService
    participant IS as InterfaceService
    H->>EE: UseEngine(Client)
    H->>EA: UseEngineUi() [Client only]
    H->>EH: StartAsync
    EH->>SS: Load()
    EH->>PS: Start()
    EH->>IS: Start()
```

1. `UserService.Load` — restores installed user from `State.json` (or applies `IDebugUserOverride`)
2. `PeerService.Start` — begins accepting peer connections
3. `InterfaceService.Start` — begins accepting interface connections (always, regardless of mode)

## Data Storage

All persistent data lives under `IAppDataPathProvider.AppDataPath` (default: `%APPDATA%/{AppName}`):

```
{AppDataPath}/
├── Data.db      LiteDB file (messages, drafts, notes, folders, activity)
├── State.json   Installed user state (name, code, environment)
└── Logs/
    └── yyyy-MM-dd.log
```

Export packages (`{name}.export.zip`, one JSON file per entry) are written to and read from an external drive selected by the user, not `AppDataPath` — see "Exporting and importing entries" above.

## Dependency Injection

All external configuration is expressed as interfaces in `Engine/src/Control/`. `EngineExtensions.UseEngine` registers defaults with `TryAddSingleton`, so a host can override any of them by registering its own implementation before or after calling `UseEngine` (later registrations win with `AddSingleton`).

One control interface, `IMessageFormat`, is not a settings provider but a message-shape provider: it supplies the concrete DTO type used to represent a message everywhere in the engine — on the wire (peer and interface connections) and in `MessageEntity.Message` in the database — and maps the engine's logical fields (subject, body, addresses, etc.) onto that type's real fields. Unlike the other control interfaces, it has no Engine default at all. Hosts using the `EngineApplication` entry point supply it as a required generic type parameter to `EngineApplication.Start<TMessageFormat>`, which registers it before running; omitting it is a compile error. See `Sample/src/SampleMessageFormat.cs` for a working example and [Control.md](Control.md#required-no-default).

`EngineUiExtensions.UseEngineUi` overrides `IBodyDocumentFactory` with `TextDocumentBodyDocumentFactory` so that drafts created in Client mode use a live AvaloniaEdit `TextDocument`. Without this call (e.g., in tests or Headless mode), the `BodyDocumentFactory` default creates `StringBodyDocument` instances.

See `Docs/Configuration.md` for all configuration interfaces.
