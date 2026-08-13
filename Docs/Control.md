# Control Interfaces

Control interfaces are the extension points through which a host application customises Engine behaviour without modifying Engine code. They live in `Engine/src/Control/` and follow a consistent DI-first pattern.

## Concept

Engine never reads environment variables, hardcodes paths, or calls host-specific APIs directly. Instead, every piece of external configuration is expressed as a small `public interface`. Each interface has a single co-located default implementation driven by `EngineConfig`. The convention scanner in `AddConventionSingletons` automatically registers every `IThing → Thing` pair it finds in the Engine assembly with `TryAddSingleton`, so hosts can override any interface simply by registering their own implementation before calling `UseEngine`.

```csharp
// Host registers its implementation first
services.AddSingleton<IAppNameProvider, MyAppNameProvider>();

// UseEngine sees an existing registration and skips the Engine default
builder.UseEngine(EngineMode.Client);
```

Config-based settings flow automatically through `EngineConfig`. Call `UseEngineConfig` before `UseEngine` so the config is present when the convention-registered implementations resolve it:

```csharp
var config = EngineConfig.Load(args);
builder.UseEngineConfig(config).UseEngine(EngineMode.Client);
```

## Interface Reference

### Optional (Engine provides a default)

These interfaces have a built-in default driven by `EngineConfig`. Override them by registering a replacement before calling `UseEngine`.

---

#### `IHomeContentProvider`

```csharp
string GetHomeText();
```

Returns the placeholder text displayed in the content area when no entry is selected.

**Engine default:** `"HOME"` (via `HomeContentProvider`)

**Sample override:** `SampleHomeContentProvider` — returns a product-appropriate instruction string.

---

#### `IUserCodeResolver`

```csharp
Task<UserInfo?> Resolve(string userCode, CancellationToken cancellation = default);
```

Converts a user activation code (entered by the user during installation) into a `UserInfo` record, or returns `null` if the code is unrecognised. The result sets the user name, display environment title, and environment accent color.

**Engine default:** accepts code `"CODE"` → user `"TEST"` (via `UserCodeResolver`)

**Sample override:** `SampleUserCodeResolver` — checks hard-coded codes (`CODE1`/`CODE2`/`CODE3`) and falls back to `USER_{CODE}_NAME` environment variables.

---

#### `IUserLocator`

```csharp
Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default);
```

Resolves a user name to its TCP peer endpoint (`IpAddress` + `Port`) for outbound P2P delivery. Returns `null` when the user is unknown and the message cannot be delivered.

**Engine default:** resolves users defined in `config.json` `Users`; returns `null` for unknown names (via `UserLocator`)

**Sample override:** `SampleUserLocator` — checks the `Users` map from `config.json` first, then falls back to `PEER_{USERNAME}=ip:port` environment variables.

---

#### `IUserNameDirectory`

```csharp
Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default);
```

Returns the names of every known user **and group** in the messaging system. Used to populate the destination auto-complete in the draft editor.

**Engine default:** returns user names from `config.json` `Users` and group names from `UserGroups` (via `UserNameDirectory`)

**Sample override:** `SampleUserNameDirectory` — unions config users/groups with users inferred from `PEER_*` environment variables.

---

#### `IUserGroupProvider`

```csharp
Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default);
```

Returns all defined user groups as a map of group name → member names. Members may be user names or other group names, enabling nested group hierarchies. Engine uses this to expand group addresses to their constituent users before delivery, deduplicating across overlapping groups and direct addresses.

When a message is sent to a group, the Engine records which addressed groups each user was reached through. The sent message view shows this context — e.g. `USER-A (OPS)` — so the operator can see which group membership drove delivery.

**Engine default:** reads `UserGroups` from `config.json`; returns empty map when no groups are defined (via `UserGroupProvider`)

**Sample override:** `SampleUserGroupProvider` — unions config groups with groups defined via `GROUP_{NAME}` environment variables (comma-separated member list).

---

#### `IAppNameProvider`

```csharp
string AppName { get; }
```

The application name used as the default data folder name and in log headers.

**Engine default:** derives from entry assembly name (via `AppNameProvider`)

**Sample override:** `SampleAppNameProvider` — reproduces the Engine default exactly (entry assembly name) unless the `APP_NAME` environment variable is set, so the data folder location does not change unless an operator opts in.

---

#### `IAppDataPathProvider`

```csharp
string AppDataPath { get; }
```

Absolute path to the root data directory. All persistent state (LiteDB, user state, logs) is written under this path.

**Engine default:** `%APPDATA%\{AppName}` when `DataFolder` is null; supports absolute paths and the `@`-prefix shorthand (see [Config.md](Config.md)) via `AppDataPathProvider`

**Sample override:** `SampleAppDataPathProvider` — reproduces the Engine default resolution exactly, plus a `DATA_FOLDER` environment variable fallback (same absolute-path/`@`-prefix rules) used only when `config.json` does not set `DataFolder`. Byte-identical to the Engine default when neither is set.

---

#### `IPortConfiguration`

```csharp
int PeerPort { get; }
int InterfacePort { get; }
```

TCP port numbers for the peer listener and the local interface listener (always active, in every mode; see [Interface.md](Interface.md)).

| Port | Default |
|------|---------|
| `PeerPort` | `50021` |
| `InterfacePort` | `50020` |

**Engine default:** uses `PeerPort`/`InterfacePort` from `config.json`, falling back to 50021/50020 (via `PortConfiguration`)

**Sample override:** `SamplePortConfiguration` — same `config.json` values, falling back to `PEER_LISTEN_PORT`/`INTERFACE_LISTEN_PORT` environment variables, then the same 50021/50020 defaults.

---

#### `IKioskModeProvider`

```csharp
bool IsKioskMode { get; }
```

When `true`, the main window hides its chrome (title bar, resize handles) and restricts navigation to prevent the user from leaving the application. Intended for locked-down deployments.

**Engine default:** `false` (via `KioskModeProvider`)

**Sample override:** `SampleKioskModeProvider` — `true` when the `KIOSK_MODE` environment variable is `"1"` or `"true"`; `false` otherwise, matching the Engine default.

---

#### `IAlertConfiguration`

```csharp
string AlertText { get; }
TimeSpan AlarmSoundDuration { get; }
bool QuickConfirmationEnabled { get; }
```

Configures the alarm triggered by alert messages (`IMessageFormat.GetIsAlert`) in Client mode: the text shown in the title bar's alert box, how long the alarm sound plays before automatically stopping (resetting whenever a new alert arrives), and whether click/Space/Enter quick confirmation is enabled. `AlertText` is also the shared source for the draft editor's alert checkbox label (`IDraftViewModel.AlertLabel`) — both surfaces always show the same word for "alert". See [Peer.md](Peer.md#alert-messages) and `Docs/ViewModels.md`.

**Engine default:** reads `AlertText`/`AlarmSoundSeconds`/`QuickConfirmationEnabled` from `config.json`, falling back to `"ALERT"` / 30 seconds / `true` (via `AlertConfiguration`). See [Config.md](Config.md).

**Sample override:** `SampleAlertConfiguration` — hardcodes `AlertText` to `"!ALERT!"`, so both the title bar's alert box and the draft editor's alert checkbox read `"!ALERT!"`; delegates `AlarmSoundDuration`/`QuickConfirmationEnabled` to the same `config.json` fields as the Engine default.

---

#### `IAlertComposeConfiguration`

```csharp
bool ComposeAlertsEnabled { get; }
```

Controls whether the draft editor shows its alert checkbox (labeled via `IAlertConfiguration.AlertText`), letting the user mark and send a draft as an alert. Disabling this only affects local origination — the app can still receive and alarm on an alert sent by a peer regardless of this setting.

**Engine default:** `true`, or the `ComposeAlertsEnabled` value from `config.json` when set (via `AlertComposeConfiguration`). See [Config.md](Config.md).

**Sample override:** `SampleAlertComposeConfiguration` — same `config.json` value, falling back to the `COMPOSE_ALERTS_ENABLED` environment variable (`"0"`/`"false"` to disable), then `true`.

---

#### `IAlertSoundPlayer`

```csharp
void Play();
void Stop();
```

Starts and stops the looping alarm sound triggered by alert messages. Actual audio playback is platform-specific, so the engine ships no built-in implementation.

**Engine default:** silent no-op (via `AlertSoundPlayer`).

**Sample override:** `SampleAlertSoundPlayer` — loops a synthesized beep tone through `paplay` (PulseAudio); any failure (missing binary, no audio device) is swallowed so the alert box and quick confirmation still work with no sound.

---

#### `IMessagePriorityProvider`

```csharp
IReadOnlyList<MessagePriorityOption> GetPriorities();
```

Returns the set of selectable message priority levels — each a `MessagePriorityOption` pairing a display `Name` with the `Value` stored via `IMessageFormat.SetPriority` and used verbatim as the OFT send priority (larger values are sent first — see [Peer.md](Peer.md)). The draft editor's priority picker (`IDraftViewModel.AvailablePriorities`) is populated from this list; see `Docs/ViewModels.md`.

**Engine default:** a single `"Normal"` (value `0`) level (via `MessagePriorityProvider`)

**Sample override:** `SampleMessagePriorityProvider` — three levels: `"Low"` (0), `"Medium"` (1), `"High"` (2).

---

#### `IMessageTagConfiguration`

```csharp
bool TagsEnabled { get; }
string TagLabel { get; }
```

Controls whether message tags (`IMessageFormat.GetTag`/`SetTag` — a short, user-inputted string identifying the type of message) are shown anywhere in the UI: the draft editor's tag input, and each Inbox/Outbox entry's tag label next to its priority in the entry listing (`EntryItemViewModel.TagText`). When `TagsEnabled` is `false`, tags are hidden everywhere — existing stored tag values are left untouched, just not surfaced. `TagLabel` is the watermark text shown in the draft editor's tag input (`IDraftViewModel.TagLabel`), letting a host call the concept something other than "Tag" (e.g. "Category", "Type") without changing engine behavior.

**Engine default:** `TagsEnabled` is `true`, or the `MessageTagsEnabled` value from `config.json` when set; `TagLabel` is `"Tag"`, or the `MessageTagLabel` value from `config.json` when set (via `MessageTagConfiguration`). See [Config.md](Config.md).

**Sample override:** `SampleMessageTagConfiguration` — same `config.json` value for `TagsEnabled`, falling back to the `TAGS_ENABLED` environment variable (`"0"`/`"false"` to disable), then `true`; `TagLabel` defaults to `"Category"` (or the `config.json` value when set).

---

#### `IMessageTagPriorityPolicy`

```csharp
IReadOnlyList<TagPriorityBlock> GetBlockedCombinations();
```

Returns the set of blocked message tag/priority combinations, enforced when composing a draft. Each `TagPriorityBlock` pairs an optional `Tag` (case-insensitive exact match) with an optional `Priority` value; leaving either `null` matches any value for that field, so a single rule can block a specific tag regardless of priority, a specific priority regardless of tag, or one specific tag/priority pair. The `TagPriorityBlockExtensions.IsBlocked(tag, priority)` extension method evaluates a rule set against a combination.

`DraftViewModel` enforces this proactively rather than only at send time: `AvailablePriorities` (see `IMessagePriorityProvider` above) excludes any priority blocked for the currently-entered tag, and setting `Tag` to a value blocked for the currently-selected priority is rejected outright (the value reverts) — so a blocked combination can never actually be entered in the draft editor. `SendCommand` also re-checks before sending, as a defense-in-depth safety net. See `Docs/ViewModels.md`.

**Engine default:** no blocked combinations (via `MessageTagPriorityPolicy`)

**Sample override:** `SampleMessageTagPriorityPolicy` — demonstrates both block kinds: the `"SPAM"` tag is blocked regardless of priority, and `High` priority (value 2) is blocked regardless of tag. Unlike Sample's other control interface overrides, this one deliberately changes default behavior from the Engine's permissive "no blocks" default, since that is the only way to usefully demonstrate the interface.

---

#### `IExternalDriveProvider`

```csharp
IReadOnlyList<ExternalDriveInfo> GetDrives();
```

Enumerates the external (removable/optical) drives currently available as a destination for the export feature or a source for the import feature (see `Docs/ViewModels.md`, `IExportViewModel`/`IImportViewModel`) — both share this same provider and drive list. Each `ExternalDriveInfo` carries a `RootPath` (to write to or read from) and a `DisplayName` (volume label + drive name, for the drive picker).

**Engine default:** `DriveInfo.GetDrives()` filtered to ready `Removable`/`CDRom` drives that pass a live write probe (a small temp file is written and deleted at the drive root) (via `ExternalDriveProvider`).

**Sample override:** `SampleExternalDriveProvider` — same `DriveInfo`-based enumeration, plus an extra pseudo-drive at the path named by the `EXPORT_DRIVE_PATH` environment variable (if set and the directory exists) — useful for exercising export/import without physical removable media.

---

#### `IPrinterProvider` / `ILinePrinter`

```csharp
// IPrinterProvider
IReadOnlyList<string> GetAvailablePrinters();
string? GetDefaultPrinter();

// ILinePrinter
Task PrintLine(string printerName, string line, CancellationToken cancellation = default);
Task PageFeed(string printerName, CancellationToken cancellation = default);
```

`IPrinterProvider` enumerates the printers available on this computer for the print manager to target (see `Docs/ViewModels.md`, `IPrintManagerViewModel`): `GetAvailablePrinters` populates the printer picker, `GetDefaultPrinter` selects the initial `SelectedPrinter` automatically. `ILinePrinter` drives the selected printer for the print queue: prints one line at a time, and the returned task from `PrintLine` completing is treated as confirmation that the line finished printing — the queue will not print the next line, or check whether a higher-priority job should interrupt the current one, until it completes. `PageFeed` is called after the last line of an entry and also when a job is interrupted partway through.

Unlike most other control interfaces, printing is one Engine handles directly rather than leaving to a host — printer discovery is a genuine operating-system resource (like `IExternalDriveProvider`'s drives), not app-specific configuration, and driving a printer line-by-line with real completion confirmation only makes sense against the operating system's own print spooler, not a bundled library. Both interfaces are implemented by the single internal `PrinterProvider` class (`Engine/src/Control/PrinterProvider.cs`) — it is the one case in this codebase where a class implements two control interfaces that don't share its name (`ILinePrinter` has no matching `LinePrinter` class), so `EngineExtensions.UseEngine` registers `ILinePrinter → PrinterProvider` explicitly alongside the usual convention-scanned `IPrinterProvider → PrinterProvider`.

**Engine default:** OS-branched via `OperatingSystem.IsWindows()`/`IsLinux()`:
- **Windows:** printer discovery shells out to PowerShell, querying WMI's `Win32_Printer` class (`Get-CimInstance -ClassName Win32_Printer`) for the printer list and the entry with `Default = true` for the default printer — no extra module dependency (unlike `Get-Printer`, which requires the PrintManagement module). Line printing uses the Windows Print Spooler (WinSpool) directly via P/Invoke (`OpenPrinter`/`StartDocPrinter`/`StartPagePrinter`/`WritePrinter`/`EndPagePrinter`/`EndDocPrinter`): each line (and each page feed, sent as a form-feed byte `\f`) is submitted as its own raw print job, and `PrintLine`/`PageFeed` don't return until polling `GetJob` reports the job has reached a terminal status (`JOB_STATUS_PRINTED`, `JOB_STATUS_COMPLETE`, `JOB_STATUS_DELETED`, or `JOB_STATUS_ERROR`) — a genuine OS-confirmed completion, not just "the app handed the bytes off."
- **Linux:** printer discovery shells out to `lpstat -p`/`lpstat -d` (CUPS). Line printing submits each line (and each page feed, as `\f`) as its own raw job via `lp -d {printer} -o raw` (parsing the returned job ID from `lp`'s "request id is …" output), then polls `lpstat -W not-completed -o {printer}` until that specific job ID no longer appears among the printer's pending jobs — the CUPS-level equivalent of the same "wait for OS-confirmed completion" contract.
- **Other platforms:** printer discovery returns an empty list/no default; line printing is a no-op.
- Both platforms poll every 150ms with a 30-second-per-line safety timeout, so a stuck or offline printer cannot hang the print queue forever; discovery and printing are both best-effort — any failure (missing tooling, no printers configured, permission error) degrades gracefully (empty list / no default / a line that times out and moves on) rather than throwing.

Not unit tested directly, for the same reason as `IOftCertificateProvider` below: both are inherently environment- and OS-dependent, so a unit test could only meaningfully assert against whatever printers happen to be installed (and reachable) on the machine running the test — `Docs/ViewModels.md`'s `PrintManagerViewModelTests` instead test the print queue's own logic (ordering, interruption, restart) against a fake `ILinePrinter`.

**Sample override:** none — Sample uses the Engine default for both interfaces.

---

#### `IPrintReceivedDefaultProvider`

```csharp
bool DefaultEnabled { get; }
```

Controls the starting state of the print manager's "print received" toggle (`IPrintManagerViewModel.PrintReceivedEnabled`) — whether every received message is automatically added to the print queue from the moment the app starts. The user can still toggle it at any time.

**Engine default:** `false`, or the `PrintReceivedEnabled` value from `config.json` when set (via `PrintReceivedDefaultProvider`). See [Config.md](Config.md).

**Sample override:** `SamplePrintReceivedDefaultProvider` — same `config.json` value, falling back to the `PRINT_RECEIVED_ENABLED` environment variable (`"1"`/`"true"` to enable), then `false`.

---

#### `IPrintReceivedRule`

```csharp
int GetPrintCount(object message);
```

Decides how many times each received message is automatically added to the print queue while the "print received" toggle is enabled — `0` to not print it, `1` to print it once, `2` for two copies, and so on. Consulted once per received message via `IEntryService.MessageInserted`.

**Engine default:** `1` for every message (via `PrintReceivedRule`).

**Sample override:** `SamplePrintReceivedRule` — prints an alert message (`IMessageFormat.GetIsAlert`) twice and every other received message once, demonstrating a rule that inspects the message itself.

---

#### `IOftPeerCertificateName`

```csharp
string? GetCertificateName(string userName);
```

Maps the local user name to a certificate subject name (CN) to look up in the system store for mutual TLS. Returning `null` disables peer authentication.

| Return value | Behaviour |
|---|---|
| `null` | Disable authentication (ephemeral cert, no client cert required) |
| Any string | Require the cert with that CN; startup throws if it is not found |

**Engine default:** `$"USER-{userName}"` when `PeerCertificateName` is null; `"disable"` → `null`; explicit string → use it as-is (via `OftPeerCertificateName`). See [Config.md](Config.md).

**Sample override:** `SampleOftPeerCertificateName` — honors an explicit `config.json` `PeerCertificateName` exactly like the Engine default; in auto mode (config value `null`), additionally checks a `CERT_NAME_{USERNAME}` environment variable before falling back to the same `USER-{userName}` default.

---

#### `IOftCertificateProvider`

```csharp
OftPeerOptions GetPeerOptions();
```

Produces the [OFT](Oft.md) `OftPeerOptions` (certificate, certificate validation, and security mode) used for both inbound and outbound peer connections. The default implementation looks up the certificate returned by `IOftPeerCertificateName` in the system certificate store, enforces chain validation (`SslPolicyErrors.None`), and selects `OftSecurityMode.DualAuthentication` when a certificate is found or `OftSecurityMode.Secure` (encrypted, unauthenticated) otherwise.

Override this interface only when you need custom certificate pinning, a non-store certificate source, or a different validation policy. In most cases, overriding `IOftPeerCertificateName` is sufficient.

**Engine default:** `OftCertificateProvider`

**Sample override:** none, deliberately — this is the one control interface Sample does not override. It duplicates ~60 lines of security-sensitive X.509 store-lookup and chain-validation logic that Engine's internal `OftCertificateProvider` cannot expose for reuse (it is `internal`, and only `Tests` has `InternalsVisibleTo` access — not `Sample`), and this doc's own guidance above says overriding `IOftPeerCertificateName` is sufficient for the vast majority of customization needs. Sample overrides that interface instead. A host that genuinely needs custom certificate pinning should still override this interface directly.

---

### Required (no default)

The engine has no built-in implementation for these interfaces at all — not even a stub. A host must register one before calling `UseEngine`, or the engine fails immediately at startup with a DI resolution error (`Unable to resolve service for type '...'`).

For hosts using the `EngineApplication` entry point, `EngineApplication.Start<TMessageFormat>(args, configureServices, windowIconUri)` (see `Sample/src/Program.cs`) takes the `IMessageFormat` implementation as a required generic type parameter and registers it itself (`services.AddSingleton<IMessageFormat, TMessageFormat>()`) before invoking `configureServices` — the host never registers it manually, and omitting the type argument is a compile error rather than a runtime DI failure.

---

#### `IMessageFormat`

```csharp
Type MessageType { get; }
object CreateMessage();
string GetMessageId(object message);
void SetMessageId(object message, string value);
string GetFromUser(object message);
void SetFromUser(object message, string value);
string GetSubject(object message);
void SetSubject(object message, string value);
string GetBody(object message);
void SetBody(object message, string value);
List<MessageAddress> GetAddresses(object message);
void SetAddresses(object message, List<MessageAddress> value);
DateTime GetSentAt(object message);
void SetSentAt(object message, DateTime value);
string GetConfirmationMessageId(object message);
void SetConfirmationMessageId(object message, string value);
bool GetIsAlert(object message);
void SetIsAlert(object message, bool value);
int GetPriority(object message);
void SetPriority(object message, int value);
string GetTag(object message);
void SetTag(object message, string value);
```

Supplies the concrete message type used throughout the engine — transmitted between peers and interfaces (see [Peer.md](Peer.md#message-format) and [Interface.md](Interface.md)) and stored in the database (see [Data.md](Data.md)) — and maps the engine's logical fields onto that type's real ones. `MessageType` must be protobuf-net serializable (`[ProtoContract]`/`[ProtoMember]`) for wire transport and LiteDB-serializable for storage. `GetConfirmationMessageId`/`SetConfirmationMessageId` and `GetIsAlert`/`SetIsAlert` back the user-read confirmation and alert-message features — see [Peer.md](Peer.md#read-confirmation) and [Peer.md](Peer.md#alert-messages). `GetPriority`/`SetPriority` back `IMessagePriorityProvider` and the OFT send priority; `GetTag`/`SetTag` back `IMessageTagConfiguration` and `IMessageTagPriorityPolicy` — see above.

The engine has no message DTO of its own; every access to a message's content goes through this interface, so a host must register an implementation to use the engine at all.

**Engine default:** none.

**`MessageFormat<TMessage>`** (also in `Engine/src/Control/MessageFormat.cs`) is an abstract base class that implements `IMessageFormat` against a concrete `TMessage` for you: it casts `object` to `TMessage` once, behind `protected abstract` members typed directly as `TMessage` (e.g. `protected abstract string GetSubject(TMessage message);`) instead of `object`, so a derived class never writes a cast itself. `MessageType` is implemented for you as `typeof(TMessage)`; `CreateMessage()` defaults to `new TMessage()` (requires a public parameterless constructor) and can be overridden for custom construction. Prefer deriving from this over implementing `IMessageFormat` directly.

**Sample implementation:** `SampleMessageFormat : MessageFormat<SampleMessage>`, demonstrating the mapping. See `Sample/src/SampleMessageFormat.cs`.

---

### Conditional / Auxiliary

---

#### `IDebugUserOverride`

```csharp
string? UserName { get; }
```

Supplies a fixed user name to `UserService`, bypassing the normal `State.json` lookup. Injected as `IEnumerable<IDebugUserOverride>` so registering multiple implementations is valid. When any registered implementation returns a non-null `UserName`, `UserService` uses that name instead of reading from disk — the user is considered permanently installed.

The Engine default (`DebugUserOverride`) returns `config.UserName`, which is `null` when not set and therefore has no effect. Intended for development and testing only.

**Sample override:** `SampleDebugUserOverride` — honors `config.json`'s `UserName` exactly like the Engine default, falling back to the `DEBUG_USER` environment variable. Registering a host implementation here takes the place of — rather than adds to — the Engine's own default, because `AddConventionSingletons` registers it via `TryAddSingleton`, which is a no-op once any registration for `IDebugUserOverride` exists (the `IEnumerable<IDebugUserOverride>` consumption pattern above only yields more than one instance if a host explicitly registers more than one itself). `SampleDebugUserOverride` therefore reproduces the config-driven behavior itself, so registering it does not silently stop `config.json`'s `UserName` field from working.

---

### Client API

---

#### `IServiceConnection`

```csharp
event Func<MessageReceivedEvent, Task>? MessageReceived;
event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;
Task Connect(CancellationToken cancellation = default);
Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default);
Task<List<string>> GetUserNames(CancellationToken cancellation = default);
Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default);
Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, bool isAlert = false, int priority = 0, string tag = "", CancellationToken cancellation = default);
Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
```

High-level API for host code to interact with the running Engine. Engine registers `DirectServiceConnection`, which calls Engine services in-process, in both Client and Headless mode — Headless mode acts as a normal peer client, just without a GUI. External programs instead plug into the message stream over the local interface listener (see [Interface.md](Interface.md)), which is unrelated to this interface.

Host applications resolve `IServiceConnection` from the container to send messages, install the user, and subscribe to inbound delivery events.

**Engine default:** `DirectServiceConnection`, registered in both Client and Headless mode.

**Sample override:** none, deliberately — unlike the interfaces above, this is not a small piece of *external configuration* a host swaps in (the "Concept" section's definition of a control interface); it is the client-facing API surface a host *consumes* to drive the running Engine, backed by `DirectServiceConnection`'s substantial in-process orchestration of Engine services. Sample resolves `IServiceConnection` directly from the container instead of replacing it. This is why it is documented in its own "Client API" section rather than "Optional"/"Required"/"Conditional" above.

---

### Supporting Type

#### `CurrentUserProvider`

Not an interface — a plain `public sealed class` registered as a singleton. Holds `UserName` (the active user name once installed, `null` beforehand). `UserService` writes to it; `OftCertificateProvider` reads from it to decide which certificate to request.

Host code should not write to `CurrentUserProvider` directly; let `UserService` manage it.
