# Control Interfaces

Control interfaces are the extension points through which a host application customises Engine behaviour without modifying Engine code. They live in `Engine/src/Control/` and follow a consistent DI-first pattern. Related concerns are consolidated into a small number of interfaces per app area (e.g. all alert-related settings live on one `IAlertSettings`) rather than one interface per individual field — see each interface's members below for what it covers.

## Concept

Engine never reads environment variables, hardcodes paths, or calls host-specific APIs directly. Instead, every piece of external configuration is expressed as a small `public interface`. Each interface has a single co-located default implementation, named `Default{Thing}`. The convention scanner in `AddConventionSingletons` automatically registers every `IThing → Thing`/`IThing → DefaultThing` pair it finds in the Engine assembly with `TryAddSingleton`, so hosts can override any interface simply by registering their own implementation.

```csharp
services.AddSingleton<IAppSettings, MyAppSettings>();
```

**A control-interface implementation — Engine's default or a host's override — describes non-config-file behavior only. It must never read `EngineConfig` itself, and it must never read an environment variable.** Where an interface has a corresponding `config.json` field, that override is applied separately, at the Engine level, as a decorator layered on top of whichever implementation ends up registered — see [Config Overrides](#config-overrides) below. This split keeps "what does this app do out of the box" (a control interface) and "what does `config.json` change about that" (a decorator Engine owns) as two independent, separately testable concerns, and means a host is never tempted to reimplement `config.json` parsing itself just to add one small piece of non-config behavior.

**Every `Default{Thing}` class is `public` (not `sealed`) with `virtual` members**, specifically so a host can inherit from it and override just the one member it actually wants to change, instead of reimplementing the whole interface. `AddConventionSingletons` treats a class named `Default{Thing}` (or a class named exactly `{Thing}`, matching the interface's own name) as the default registration for `I{Thing}` — see `EngineExtensions.AddConventionSingletons`. For example:

```csharp
// Only overrides AlertText; AlarmSoundDuration, QuickConfirmationEnabled, and ComposeAlertsEnabled
// all keep DefaultAlertSettings' own behavior.
public sealed class SampleAlertSettings : DefaultAlertSettings
{
    public override string AlertText => "!ALERT!";
}
```

A base class's own virtual members may call each other, so overriding one can affect another by design — e.g. `DefaultAppSettings.AppDataPath` computes `Path.Combine(..., AppName)` by reading the (possibly overridden) `AppName` property through virtual dispatch, so a host overriding only `AppName` automatically gets a matching default data folder without needing to also override `AppDataPath`.

## Config Overrides

`EngineExtensions.UseEngineConfigOverrides()` registers a small decorator for every control interface that has a corresponding `config.json` field. Call it last, after `UseEngine` and after any host `ConfigureServices` callback that registers control-interface overrides, so it wraps whichever implementation actually ends up registered for each interface:

```csharp
var config = EngineConfig.Load(args);
Host.CreateDefaultBuilder()
    .UseEngineConfig(config)
    .UseEngine(EngineMode.Client)
    .ConfigureServices((_, services) => configureServices(services)) // host overrides, e.g. Sample's
    .UseEngineConfigOverrides()                                      // config.json overlaid last
    .Build();
```

Internally, for each affected interface `IThing`, this moves whichever `IThing` registration currently exists (the Engine default, or a host override — if both were registered, the host's, matching normal last-registration-wins resolution) into a keyed "fallback" slot, then registers a `Configured{Thing}` class as the new unkeyed `IThing` — the one everything else in the container actually resolves. Each `Configured{Thing}` takes the keyed fallback and `EngineConfig` as constructor dependencies and, field by field, returns the config value when it is non-null and the fallback's value otherwise; a member with no corresponding `config.json` field always delegates straight to the fallback. These decorator classes are co-located with their interface and default implementation (e.g. `ConfiguredPortConfiguration` lives in `PortConfiguration.cs` alongside `IPortConfiguration`/`DefaultPortConfiguration`) but are registered only by `UseEngineConfigOverrides()`, never by convention scanning (their own name doesn't match the `I{Thing}`/`Default{Thing}` convention).

`EngineApplication.Start` (the entry point `Sample` uses) calls `UseEngineConfigOverrides()` for you in both Client and Headless mode; a host bypassing `EngineApplication` and composing its own `IHostBuilder` must call it explicitly.

## Interface Reference

### Optional (Engine provides a default)

These interfaces have a built-in default. Override them by registering a replacement, or by inheriting from the `Default{Thing}` class and overriding just the members you want to change.

---

#### `IAppSettings`

```csharp
string AppName { get; }
string AppDataPath { get; }
bool IsKioskMode { get; }
string GetHomeText();
```

This app's own identity and top-level presentation: the display/data-folder name, the root directory persistent state (LiteDB, user state, logs) is written under, whether the main window runs in kiosk mode (hides window chrome and restricts navigation), and the placeholder text shown in the content area when no entry is selected.

**Engine default:** `AppName` derives from the entry assembly name; `AppDataPath` is `%APPDATA%\{AppName}` (computed from `AppName` via virtual dispatch — see [Concept](#concept)); `IsKioskMode` is `false`; `GetHomeText()` returns `"HOME"` (via `DefaultAppSettings`).

**Config override:** `ConfiguredAppSettings` applies `config.json`'s `DataFolder` over the wrapped provider's `AppDataPath` when set: `null` uses it unchanged; an absolute path is used verbatim; an `@`-prefixed path is relative to it (see [Config.md](Config.md)) — supporting the `@`-prefix shorthand this way, rather than reading `AppName` again itself, is what lets a host override *both* `AppName` and `DataFolder` at once and have them compose correctly. `AppName`, `IsKioskMode`, and `GetHomeText()` have no corresponding `config.json` field and always delegate to the wrapped provider.

**Sample override:** `SampleAppSettings` — overrides `GetHomeText()` with a product-appropriate instruction string; every other member uses the Engine default. Changing the app data path's default runtime behavior has caused real data loss in this project before, so Sample deliberately never touches `AppDataPath`/`AppName`.

---

#### `IUserIdentity`

```csharp
string? DebugUserName { get; }
Task<UserInfo?> ResolveCode(string userCode, CancellationToken cancellation = default);
```

How this instance's own local user identity is established: a fixed debug override that bypasses the normal `State.json` lookup, and resolving a user activation code (entered during installation) to a `UserInfo`. See `Services.UserService`.

**Engine default:** no debug override (`DebugUserName` is `null`); `ResolveCode` accepts code `"CODE"` → user `"TEST"` (via `DefaultUserIdentity`).

**Config override:** `ConfiguredUserIdentity` applies `config.json`'s `UserName` over the wrapped provider's `DebugUserName` when set. See [Config.md](Config.md). `ResolveCode` has no corresponding `config.json` field and always delegates to the wrapped provider.

**Sample override:** `SampleUserIdentity` — overrides `ResolveCode` to recognize three hard-coded test codes (`CODE1`/`CODE2`/`CODE3`) instead of the Engine default's one; `DebugUserName` uses the Engine default (`null`, `config.json`'s `UserName` applied automatically on top).

---

#### `IUserDirectory`

```csharp
Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default);
Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default);
Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default);
```

Everything the engine knows about addressable users and groups: resolving a user name to its TCP peer endpoint for outbound P2P delivery (`GetEndpoint` returns `null` when the user is unknown), group membership for address expansion (members may be user names or other group names, enabling nested hierarchies), and the full list of known user and group names for the destination auto-complete in the draft editor.

When a message is sent to a group, the Engine records which addressed groups each user was reached through. The sent message view shows this context — e.g. `USER-A (OPS)` — so the operator can see which group membership drove delivery.

**Engine default:** no known users, groups, or names for any of the three members (via `DefaultUserDirectory`).

**Config override:** `ConfiguredUserDirectory` resolves `config.json`'s `Users` map before falling back to the wrapped provider for `GetEndpoint`; merges `config.json`'s `UserGroups` over the wrapped provider's own groups for `GetGroups` (a config entry replaces a same-named group from the wrapped provider; groups only defined by the wrapped provider still pass through); and unions the wrapped provider's names with `config.json`'s `Users`/`UserGroups` keys, deduplicated and sorted, for `GetAllUserNames`. See [Config.md](Config.md).

**Sample override:** none — the Engine default plus the automatic config override already cover every genuinely useful case (a fixed, non-config user/group directory has no sensible non-config content to demonstrate).

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

**Engine default:** fixed `50021`/`50020` (via `DefaultPortConfiguration`)

**Config override:** `ConfiguredPortConfiguration` applies `config.json`'s `PeerPort`/`InterfacePort` over the wrapped provider's ports, field by field, when set. See [Config.md](Config.md).

**Sample override:** none — the Engine default plus the automatic config override already cover every genuinely useful case.

---

#### `IAlertSettings`

```csharp
string AlertText { get; }
TimeSpan AlarmSoundDuration { get; }
bool QuickConfirmationEnabled { get; }
bool ComposeAlertsEnabled { get; }
```

Configuration for the alert-message feature (`IMessageFormat.GetIsAlert`) in Client mode: the title bar's alarm box text (also the draft editor's alert checkbox label — both surfaces always show the same word for "alert"), how long the alarm sound plays before automatically stopping (resetting whenever a new alert arrives while already alarming), whether click/Space/Enter quick confirmation is enabled, and whether the draft editor shows its alert checkbox at all (disabling only affects local origination — the app can still receive and alarm on a peer-originated alert). See [Peer.md](Peer.md#alert-messages) and `Docs/ViewModels.md`. Actually playing the alarm sound is real platform behavior, not configuration — see [`IAlertSoundPlayer`](#ialertsoundplayer-not-a-control-interface) below.

**Engine default:** `"ALERT"` / 30 seconds / `true` / `true` (via `DefaultAlertSettings`).

**Config override:** `ConfiguredAlertSettings` applies `config.json`'s `AlertText`/`AlarmSoundSeconds`/`QuickConfirmationEnabled`/`ComposeAlertsEnabled` over the wrapped provider's values, field by field, when set. See [Config.md](Config.md).

**Sample override:** `SampleAlertSettings` — overrides `AlertText` to `"!ALERT!"` (so both the title bar's alert box and the draft editor's alert checkbox read `"!ALERT!"` unless overridden by config). `AlarmSoundDuration`/`QuickConfirmationEnabled`/`ComposeAlertsEnabled` use the Engine default, since this override isn't meant to change them — `config.json` can still override any of them, applied automatically by the config override on top.

---

#### `IAlertSoundPlayer` (not a control interface)

```csharp
void Play();
void Stop();
```

Starts/stops the alarm sound itself while one or more alerts are pending. Unlike `IAlertSettings` above, this is real OS-level audio playback, not configuration or rules, so it does not live in `Engine/src/Control/` and is not registered/overridable the way control interfaces are — it lives in `Engine/src/Audio/`, and Engine always provides real behavior for it directly, the same way it always provides real behavior for [`IPrinterProvider`/`ILinePrinter`](#iprinterprovider--ilineprinter) rather than leaving it to a host. It is `public` only because `AlertViewModel` (itself `public`) takes it as a separate constructor dependency alongside `IAlertSettings`, not because it is meant to be overridden.

**Engine implementation:** `AlertSoundPlayer` (`Engine/src/Audio/AlertSoundPlayer.cs`) loops a synthesized beep tone using the operating system's own audio facilities — `paplay` (PulseAudio) on Linux, `winmm.dll`'s `PlaySound` (via P/Invoke, with the same raw tone data wrapped in a WAV header) on Windows, a no-op on any other platform. Best-effort: any failure (missing binary, no audio device, unsupported platform) is swallowed so the alert box and quick confirmation still work with no sound rather than crashing the app. Not unit tested directly, for the same reason as `IPrinterProvider`/`IOftCertificateProvider` above — inherently environment- and OS-dependent.

---

#### `IMessageComposition`

```csharp
IReadOnlyList<MessagePriorityOption> GetPriorities();
bool TagsEnabled { get; }
string TagLabel { get; }
IReadOnlyList<TagPriorityBlock> GetBlockedCombinations();
```

How messages are composed and displayed: the set of selectable priority levels (each a `MessagePriorityOption` pairing a display `Name` with the `Value` stored via `IMessageFormat.SetPriority` and used verbatim as the OFT send priority — larger values are sent first, see [Peer.md](Peer.md)); whether message tags (`IMessageFormat.GetTag`/`SetTag`) are shown anywhere in the UI and what the tag input's watermark says; and which tag/priority combinations are blocked outright when composing a draft (each `TagPriorityBlock` pairs an optional `Tag` — case-insensitive exact match — with an optional `Priority`; leaving either `null` matches any value for that field). The `TagPriorityBlockExtensions.IsBlocked(tag, priority)` and `MessagePriorityOptionExtensions.GetLabel(value)` extension methods evaluate a set of either type.

`DraftViewModel` enforces the blocked-combination rules proactively rather than only at send time: `AvailablePriorities` excludes any priority blocked for the currently-entered tag, and setting `Tag` to a value blocked for the currently-selected priority is rejected outright (the value reverts) — so a blocked combination can never actually be entered in the draft editor. `SendCommand` also re-checks before sending, as a defense-in-depth safety net. See `Docs/ViewModels.md`.

**Engine default:** a single `"Normal"` (value `0`) priority level; tags enabled with label `"Tag"`; no blocked combinations (via `DefaultMessageComposition`). Hosts that need multiple selectable priority levels or blocked combinations should override this registration.

**Config override:** `ConfiguredMessageComposition` applies `config.json`'s `MessageTagsEnabled`/`MessageTagLabel` over the wrapped provider's `TagsEnabled`/`TagLabel`, field by field, when set. See [Config.md](Config.md). `GetPriorities`/`GetBlockedCombinations` have no corresponding `config.json` field and always delegate to the wrapped provider.

**Sample override:** `SampleMessageComposition` — three priority levels (`"Low"`/`"Medium"`/`"High"`, values 0/1/2) instead of the Engine default's one; renames the tag label to `"Category"` (tags left enabled, matching the Engine default); demonstrates both blocked-combination kinds — the `"SPAM"` tag is blocked regardless of priority, and `High` priority is blocked regardless of tag. Unlike Sample's other control interface overrides, the blocked combinations deliberately change default behavior from the Engine's permissive "no blocks" default, since that is the only way to usefully demonstrate that part of the interface.

---

#### `IExternalDriveProvider`

```csharp
IReadOnlyList<ExternalDriveInfo> GetDrives();
```

Enumerates the external (removable/optical) drives currently available as a destination for the export feature or a source for the import feature (see `Docs/ViewModels.md`, `IExportViewModel`/`IImportViewModel`) — both share this same provider and drive list. Each `ExternalDriveInfo` carries a `RootPath` (to write to or read from) and a `DisplayName` (volume label + drive name, for the drive picker). No `config.json` field — not affected by config overrides.

**Engine default:** `DriveInfo.GetDrives()` filtered to ready `Removable`/`CDRom` drives that pass a live write probe (a small temp file is written and deleted at the drive root) (via `DefaultExternalDriveProvider`).

**Sample override:** none — Sample uses the Engine default.

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

`IPrinterProvider` enumerates the printers available on this computer for the print manager to target (see `Docs/ViewModels.md`, `IPrintManagerViewModel`): `GetAvailablePrinters` populates the printer picker, `GetDefaultPrinter` selects the initial `SelectedPrinter` automatically. `ILinePrinter` drives the selected printer for the print queue: prints one line at a time, and the returned task from `PrintLine` completing is treated as confirmation that the line finished printing — the queue will not print the next line, or check whether a higher-priority job should interrupt the current one, until it completes. `PageFeed` is called after the last line of an entry and also when a job is interrupted partway through. Neither has a `config.json` field — not affected by config overrides.

Unlike most other control interfaces, printing is one Engine handles directly rather than leaving to a host — printer discovery is a genuine operating-system resource (like `IExternalDriveProvider`'s drives), not app-specific configuration, and driving a printer line-by-line with real completion confirmation only makes sense against the operating system's own print spooler, not a bundled library. Both interfaces are implemented by the single `DefaultPrinterProvider` class (`Engine/src/Control/PrinterProvider.cs`) — it is the one case in this codebase where a class implements two control interfaces that don't share its name (`ILinePrinter` has no matching `DefaultLinePrinter` class), so `EngineExtensions.UseEngine` registers `ILinePrinter → DefaultPrinterProvider` explicitly alongside the usual convention-scanned `IPrinterProvider → DefaultPrinterProvider`.

**Engine default:** OS-branched via `OperatingSystem.IsWindows()`/`IsLinux()`:
- **Windows:** printer discovery shells out to PowerShell, querying WMI's `Win32_Printer` class (`Get-CimInstance -ClassName Win32_Printer`) for the printer list and the entry with `Default = true` for the default printer — no extra module dependency (unlike `Get-Printer`, which requires the PrintManagement module). Line printing uses the Windows Print Spooler (WinSpool) directly via P/Invoke (`OpenPrinter`/`StartDocPrinter`/`StartPagePrinter`/`WritePrinter`/`EndPagePrinter`/`EndDocPrinter`): each line (and each page feed, sent as a form-feed byte `\f`) is submitted as its own raw print job, and `PrintLine`/`PageFeed` don't return until polling `GetJob` reports the job has reached a terminal status (`JOB_STATUS_PRINTED`, `JOB_STATUS_COMPLETE`, `JOB_STATUS_DELETED`, or `JOB_STATUS_ERROR`) — a genuine OS-confirmed completion, not just "the app handed the bytes off."
- **Linux:** printer discovery shells out to `lpstat -p`/`lpstat -d` (CUPS). Line printing submits each line (and each page feed, as `\f`) as its own raw job via `lp -d {printer} -o raw` (parsing the returned job ID from `lp`'s "request id is …" output), then polls `lpstat -W not-completed -o {printer}` until that specific job ID no longer appears among the printer's pending jobs — the CUPS-level equivalent of the same "wait for OS-confirmed completion" contract.
- **Other platforms:** printer discovery returns an empty list/no default; line printing is a no-op.
- Both platforms poll every 150ms with a 30-second-per-line safety timeout, so a stuck or offline printer cannot hang the print queue forever; discovery and printing are both best-effort — any failure (missing tooling, no printers configured, permission error) degrades gracefully (empty list / no default / a line that times out and moves on) rather than throwing.

Not unit tested directly, for the same reason as `IOftCertificateProvider` below: both are inherently environment- and OS-dependent, so a unit test could only meaningfully assert against whatever printers happen to be installed (and reachable) on the machine running the test — `Docs/ViewModels.md`'s `PrintManagerViewModelTests` instead test the print queue's own logic (ordering, interruption, restart) against a fake `ILinePrinter`.

**Sample override:** none — Sample uses the Engine default for both interfaces.

---

#### `IPrintPolicy`

```csharp
bool PrintReceivedDefaultEnabled { get; }
int GetPrintCount(object message);
```

The print manager's automatic "print received" behavior: whether its toggle (`IPrintManagerViewModel.PrintReceivedEnabled`) starts enabled — automatically adding every received message to the print queue from the moment the app starts, though the user can still toggle it at any time — and how many times each received message is added to the print queue while it is (`0` to not print it, `1` to print it once, `2` for two copies, and so on). Consulted once per received message via `IEntryService.MessageInserted`.

**Engine default:** `false` / `1` for every message (via `DefaultPrintPolicy`).

**Config override:** `ConfiguredPrintPolicy` applies `config.json`'s `PrintReceivedEnabled` over the wrapped provider's `PrintReceivedDefaultEnabled` when set. See [Config.md](Config.md). `GetPrintCount` has no corresponding `config.json` field and always delegates to the wrapped provider.

**Sample override:** `SamplePrintPolicy` — overrides `GetPrintCount` to print an alert message (`IMessageFormat.GetIsAlert`) twice and every other received message once, demonstrating a rule that inspects the message itself; `PrintReceivedDefaultEnabled` uses the Engine default.

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

**Engine default:** always `$"USER-{userName}"` (via `DefaultOftPeerCertificateName`)

**Config override:** `ConfiguredOftPeerCertificateName` applies `config.json`'s `PeerCertificateName`: `null` falls back to the wrapped provider; `"disable"` forces `null` (no authentication); an explicit name is used as-is. See [Config.md](Config.md).

**Sample override:** none — the Engine default plus the automatic config override already cover every genuinely useful case.

---

#### `IOftCertificateProvider`

```csharp
OftPeerOptions GetPeerOptions();
```

Produces the [OFT](Oft.md) `OftPeerOptions` (certificate, certificate validation, and security mode) used for both inbound and outbound peer connections. The default implementation looks up the certificate returned by `IOftPeerCertificateName` in the system certificate store, enforces chain validation (`SslPolicyErrors.None`), and selects `OftSecurityMode.DualAuthentication` when a certificate is found or `OftSecurityMode.Secure` (encrypted, unauthenticated) otherwise. No `config.json` field of its own — not affected by config overrides (it consumes the already-config-overridden `IOftPeerCertificateName`). `GetPeerOptions` is `virtual` (on `DefaultOftCertificateProvider`) so a host can inherit and override it, though for most customization needs, overriding `IOftPeerCertificateName` instead is sufficient and does not require touching this security-sensitive class at all.

Override this interface only when you need custom certificate pinning, a non-store certificate source, or a different validation policy.

**Engine default:** `DefaultOftCertificateProvider`

**Sample override:** none, deliberately — this is the one control interface Sample does not override. It would duplicate ~60 lines of security-sensitive X.509 store-lookup and chain-validation logic, and this doc's own guidance above says overriding `IOftPeerCertificateName` is sufficient for the vast majority of customization needs. Sample overrides that interface instead. A host that genuinely needs custom certificate pinning should still override this interface directly.

---

#### `INetworkTopology`

```csharp
NodeRole Role { get; }
UserEndpoint? GetServerEndpoint();
Task<IReadOnlyDictionary<string, ServerUserConfig>> GetServerUsers(CancellationToken cancellation = default);
```

This instance's place in the peer/client/server networking topology — see [Peer.md](Peer.md#node-roles). `Role` selects one of `NodeRole.Peer`/`Client`/`Server`; `GetServerEndpoint` is the server endpoint a `Client`-role instance forms its single long-term connection to (unused outside `Client`); `GetServerUsers` is the full server-user map a `Server`-role instance routes with, keyed by server user name — every server in the cluster, not just the local one (unused outside `Server`).

This control interface is the read-only surface a host's own code can use to query the resolved topology at runtime; it is not what actually selects the `IPeerService` implementation (`PeerService`/`ClientPeerService`/`ServerRoutingService`) — that happens earlier, directly from `EngineConfig.NodeRole`, synchronously in `EngineExtensions.UseEngine`, before the container (and therefore this interface) exists to consult.

**Engine default:** always `NodeRole.Peer`, no server endpoint, no server users (via `DefaultNetworkTopology`).

**Config override:** `ConfiguredNetworkTopology` applies `config.json`'s `NodeRole` over the wrapped provider's `Role` when set and recognized (`"Peer"`/`"Client"`/`"Server"`, case-insensitive; an unrecognized value falls back to the wrapped provider rather than forcing `Peer`); applies `ServerEndpoint` over `GetServerEndpoint` when set; and merges `ServerUsers` over `GetServerUsers`'s own server users (a config entry replaces a same-named server user from the wrapped provider; server users only defined by the wrapped provider still pass through). See [Config.md](Config.md).

**Sample override:** none — the Engine default plus the automatic config override already cover every genuinely useful case.

---

#### `IConfigFileProvider`

```csharp
bool Enabled { get; }
```

Determines whether the `--config` command-line argument is honored at all. Resolved once, before any other control interface — before `EngineConfig.Load` even runs — from a minimal, throwaway service provider built in `EngineApplication.Start` (see `Docs/Architecture.md`), since the real host container cannot be built until `EngineConfig` itself exists. Because of this ordering, **an implementation of this interface must never depend on `EngineConfig`** — the one interface in this codebase for which that restriction is structural, not just a style choice. When `Enabled` is `false`, `--config` is ignored entirely and every setting uses its default, as if the argument had never been passed.

**Engine default:** `true` (via `DefaultConfigFileProvider`)

**Sample override:** none — Sample uses the Engine default (config file reading enabled). A host wanting to lock down a deployment to never read `--config` would register its own implementation returning `false`.

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

Supplies the concrete message type used throughout the engine — transmitted between peers and interfaces (see [Peer.md](Peer.md#message-format) and [Interface.md](Interface.md)) and stored in the database (see [Data.md](Data.md)) — and maps the engine's logical fields onto that type's real ones. `MessageType` must be protobuf-net serializable (`[ProtoContract]`/`[ProtoMember]`) for wire transport and LiteDB-serializable for storage. `GetConfirmationMessageId`/`SetConfirmationMessageId` and `GetIsAlert`/`SetIsAlert` back the user-read confirmation and alert-message features — see [Peer.md](Peer.md#read-confirmation) and [Peer.md](Peer.md#alert-messages). `GetPriority`/`SetPriority` back `IMessageComposition` and the OFT send priority; `GetTag`/`SetTag` back `IMessageComposition` too — see above.

The engine has no message DTO of its own; every access to a message's content goes through this interface, so a host must register an implementation to use the engine at all.

**Engine default:** none.

**`MessageFormat<TMessage>`** (also in `Engine/src/Control/MessageFormat.cs`) is an abstract base class that implements `IMessageFormat` against a concrete `TMessage` for you: it casts `object` to `TMessage` once, behind `protected abstract` members typed directly as `TMessage` (e.g. `protected abstract string GetSubject(TMessage message);`) instead of `object`, so a derived class never writes a cast itself. `MessageType` is implemented for you as `typeof(TMessage)`; `CreateMessage()` defaults to `new TMessage()` (requires a public parameterless constructor) and can be overridden for custom construction. Prefer deriving from this over implementing `IMessageFormat` directly.

**Sample implementation:** `SampleMessageFormat : MessageFormat<SampleMessage>`, demonstrating the mapping. See `Sample/src/SampleMessageFormat.cs`.

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

**Sample override:** none, deliberately — unlike the interfaces above, this is not a small piece of *external configuration* a host swaps in (the "Concept" section's definition of a control interface); it is the client-facing API surface a host *consumes* to drive the running Engine, backed by `DirectServiceConnection`'s substantial in-process orchestration of Engine services. Sample resolves `IServiceConnection` directly from the container instead of replacing it. This is why it is documented in its own "Client API" section rather than "Optional"/"Required" above.

---

### Supporting Type

#### `CurrentUserProvider`

Not an interface — a plain `public sealed class` registered as a singleton. Holds `UserName` (the active user name once installed, `null` beforehand). `UserService` writes to it; `OftCertificateProvider` reads from it to decide which certificate to request.

Host code should not write to `CurrentUserProvider` directly; let `UserService` manage it.
