# Control Interfaces

Control interfaces are the extension points through which a host application customises Engine behaviour without modifying Engine code. Every piece of configuration and rule-based behaviour a host might want to override — including the concrete message type and its logical field mapping — is consolidated into a single interface, `IEngineController`, in `Engine/src/Control/EngineController.cs`. See each app-area section below for what it covers.

## Concept

Engine never reads environment variables, hardcodes paths, or calls host-specific APIs directly. Instead, every piece of external configuration and rule-based behaviour is expressed as one member on `IEngineController`. `DefaultEngineController<TMessage>` is the single default implementation, generic over the host's concrete message type — the engine has no message DTO of its own, so this generic parameter is how a host supplies one; a host must always define a subclass, since the message-field members are `protected abstract` (see [Message Format](#message-format) below) and the class itself is `abstract`. `AddConventionSingletons` does *not* auto-register `IEngineController` (it skips open generic and abstract types).

For hosts using the `EngineApplication` entry point, `EngineApplication.Start<TEngineController>(args, windowIconUri, configureServices)` (see `Sample/src/Program.cs`) takes the subclass as a required generic type parameter and registers it itself (`services.AddSingleton<IEngineController, TEngineController>()`) before invoking the optional `configureServices` callback — the host never registers it manually, and omitting the type argument is a compile error rather than a runtime DI failure. A host bypassing `EngineApplication` and composing its own `IHostBuilder` must register it explicitly instead:

```csharp
services.AddSingleton<IEngineController, MyEngineController>();
```

**A control-interface implementation — a host's `DefaultEngineController<TMessage>` subclass — describes non-config-file behavior only. It must never read `EngineConfig` itself, and it must never read an environment variable.** Where a member has a corresponding `config.json` field, that override is applied separately, at the Engine level, as a decorator layered on top of whichever implementation ends up registered — see [Config Overrides](#config-overrides) below. This split keeps "what does this app do out of the box" (`IEngineController`) and "what does `config.json` change about that" (`ConfiguredEngineController`, a decorator Engine owns) as two independent, separately testable concerns, and means a host is never tempted to reimplement `config.json` parsing itself just to add one small piece of non-config behavior.

**`DefaultEngineController<TMessage>` is `public abstract` (not `sealed`) with `virtual` members** for every app area other than the message format, specifically so a host can inherit from it and override just the one member it actually wants to change, instead of reimplementing the whole interface. For example:

```csharp
// Only overrides HomeText (plus the required message-field mapping); every other non-message
// member keeps DefaultEngineController<TMessage>'s own behavior.
public sealed class SampleEngineController : DefaultEngineController<SampleMessage>
{
    public SampleEngineController(ICurrentUserProvider currentUserProvider) : base(currentUserProvider) { }

    protected override string GetMessageId(SampleMessage message) => message.Id;
    // ...every other message-field member...

    public override string HomeText => "Select a folder and entry to get started, or create a new draft or note.";
}
```

A base class's own virtual members may call each other, so overriding one can affect another by design — e.g. `DefaultEngineController<TMessage>.AppDataPath` computes `Path.Combine(..., AppName)` by reading the (possibly overridden) `AppName` property through virtual dispatch, so a host overriding only `AppName` automatically gets a matching default data folder without needing to also override `AppDataPath`. Likewise `ConnectionOptions` looks up a certificate via `GetCertificateName(userName)` through virtual dispatch, so overriding just `GetCertificateName` is enough for most peer-authentication customization needs.

`DefaultEngineController<TMessage>`'s constructor takes an `ICurrentUserProvider` — needed by `ConnectionOptions` to know which user's certificate to look up. A host subclass with no constructor parameters of its own still needs an explicit constructor forwarding it to the base, as shown above.

## Config Overrides

`EngineExtensions.UseEngineConfigOverrides()` registers a single decorator, `ConfiguredEngineController`, wrapping whichever `IEngineController` is currently registered. Call it last, after `UseEngine` and after whatever registers `IEngineController` (the `TEngineController` generic argument to `EngineApplication.Start`, or a host's own `ConfigureServices` call when composing the `IHostBuilder` directly), so it wraps whichever implementation actually ends up registered:

```csharp
var config = EngineConfig.Load(args);
Host.CreateDefaultBuilder()
    .UseEngineConfig(config)
    .UseEngine(EngineMode.Client)
    .ConfigureServices((_, services) => services.AddSingleton<IEngineController, MyEngineController>())
    .UseEngineConfigOverrides()                                      // config.json overlaid last
    .Build();
```

Internally, this moves whichever `IEngineController` registration currently exists (the host's own `DefaultEngineController<TMessage>` subclass — there is no Engine-provided default, since only a host knows its message type) into a keyed "fallback" slot, then registers `ConfiguredEngineController` as the new unkeyed `IEngineController` — the one everything else in the container actually resolves. `ConfiguredEngineController` takes the keyed fallback, `EngineConfig`, and `ICurrentUserProvider` as constructor dependencies and, member by member, returns the config value when it is non-null and the fallback's value otherwise; a member with no corresponding `config.json` field always delegates straight to the fallback. `ConfiguredEngineController` is registered only by `UseEngineConfigOverrides()`, never by convention scanning (its own name doesn't match the `IEngineController`/`DefaultEngineController` convention, and convention scanning skips it anyway since it isn't a `DefaultEngineController<TMessage>` subclass).

`EngineApplication.Start<TEngineController>` (the entry point `Sample` uses) calls `UseEngineConfigOverrides()` for you in both Client and Headless mode; a host bypassing `EngineApplication` and composing its own `IHostBuilder` must call it explicitly.

**Bootstrap resolution:** `IEngineController.ConfigFileEnabled` (see [Config File](#config-file) below) is resolved once, before `EngineConfig` even exists, from a minimal throwaway container in `EngineApplication.ResolveEngineController()` — `ConfiguredEngineController` is never registered in that container (it needs `EngineConfig`, which doesn't exist yet), so this bootstrap resolution always sees the plain, unconfigured `IEngineController` registration, never a config-overridden one. This is fine, since there is no `config.json` field for `ConfigFileEnabled` in the first place — that would be circular. The `TEngineController` registration itself happens unconditionally as part of `EngineApplication.Start`, before this bootstrap phase runs, so it is always present regardless of any config-derived state.

## Interface Reference: `IEngineController`

### Message Format

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

Supplies the concrete message type used throughout the engine — on the wire (peer and interface connections) and in the database — and maps the engine's logical fields onto that type's real fields. `MessageType` must be protobuf-net serializable (`[ProtoContract]`/`[ProtoMember]`) for wire transport and LiteDB-serializable for storage. `GetConfirmationMessageId`/`SetConfirmationMessageId` and `GetIsAlert`/`SetIsAlert` back the user-read confirmation and alert-message features — see [Peer.md](Peer.md#read-confirmation) and [Peer.md](Peer.md#alert-messages). `GetPriority`/`SetPriority` back [Message Composition](#message-composition) and the OFT send priority; `GetTag`/`SetTag` back [Message Composition](#message-composition) too.

These members are declared on `IEngineController` as `object`-typed, since that's the boundary every other layer (LiteDB storage, OFT wire serialization) operates at — but `DefaultEngineController<TMessage>` implements them *explicitly* (`string IEngineController.GetSubject(object message) => GetSubject((TMessage)message);`), casting to `TMessage` once on your behalf, and exposes type-safe `protected abstract` members of the same name instead (`protected abstract string GetSubject(TMessage message);`) — a derived class never sees or writes an `object`-to-`TMessage` cast itself. `MessageType` is implemented as `typeof(TMessage)`; `CreateMessage()` defaults to `new TMessage()` (the `where TMessage : class, new()` constraint requires a public parameterless constructor) via a `protected virtual TMessage CreateMessage()` that can be overridden for custom construction. Because the message-field members are `protected abstract`, `DefaultEngineController<TMessage>` itself is `abstract` — a host must always define a subclass implementing them, the compile-time equivalent of "no default; the engine has no message DTO of its own."

**Engine default:** none — every message-field member is `protected abstract` on `DefaultEngineController<TMessage>`; a host must implement all of them (see `Sample/src/SampleEngineController.cs` for a full worked example).

**Config override:** none — there is no `config.json` field for any message-field member (there could not sensibly be one, since the whole point is that the engine doesn't know the DTO's shape). Every member always delegates straight to the wrapped provider.

**Sample override:** `SampleEngineController` maps every logical field onto `SampleMessage`, a DTO with deliberately differently-named fields (`Id`, `Sender`, `Title`, `Text`, `Recipients`, …) to demonstrate that the mapping — not any assumed field name or shape — is what the engine actually relies on.

---

### App Settings

```csharp
string AppName { get; }
string AppDataPath { get; }
bool IsKioskMode { get; }
string HomeText { get; }
```

This app's own identity and top-level presentation: the display/data-folder name, the root directory persistent state (LiteDB, user state, logs) is written under, whether the main window runs in kiosk mode (hides window chrome and restricts navigation), and the placeholder text shown in the content area when no entry is selected.

**Engine default:** `AppName` derives from the entry assembly name; `AppDataPath` is `%APPDATA%\{AppName}` (computed from `AppName` via virtual dispatch — see [Concept](#concept)); `IsKioskMode` is `false`; `HomeText` returns `"HOME"`.

**Config override:** `config.json`'s `DataFolder` overrides `AppDataPath` when set: `null` uses it unchanged; an absolute path is used verbatim; an `@`-prefixed path is relative to it (see [Config.md](Config.md)) — supporting the `@`-prefix shorthand this way, rather than reading `AppName` again itself, is what lets a host override *both* `AppName` and `DataFolder` at once and have them compose correctly. `AppName`, `IsKioskMode`, and `HomeText` have no corresponding `config.json` field and always delegate to the wrapped provider.

**Sample override:** `SampleEngineController` overrides `HomeText` with a product-appropriate instruction string; every other member uses the Engine default. Changing the app data path's default runtime behavior has caused real data loss in this project before, so Sample deliberately never touches `AppDataPath`/`AppName`.

---

### User Identity

```csharp
string? DebugUserName { get; }
UserInfo? ResolveCode(string userCode);
```

How this instance's own local user identity is established: a fixed debug override that bypasses the normal `State.json` lookup, and resolving a user activation code (entered during installation) to a `UserInfo`. See `Services.UserService`.

**Engine default:** no debug override (`DebugUserName` is `null`); `ResolveCode` accepts code `"CODE"` → user `"TEST"`.

**Config override:** `config.json`'s `UserName` overrides `DebugUserName` when set. See [Config.md](Config.md). `ResolveCode` has no corresponding `config.json` field and always delegates to the wrapped provider.

**Sample override:** `SampleEngineController` overrides `ResolveCode` to recognize three hard-coded test codes (`CODE1`/`CODE2`/`CODE3`) instead of the Engine default's one; `DebugUserName` uses the Engine default (`null`, `config.json`'s `UserName` applied automatically on top).

---

### User Directory

```csharp
UserEndpoint? GetEndpoint(string userName);
IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups { get; }
IReadOnlyList<string> Users { get; }
```

Everything the engine knows about addressable users and groups: resolving a user name to its TCP peer endpoint for outbound P2P delivery (`GetEndpoint` returns `null` when the user is unknown), group membership for address expansion (members may be user names or other group names, enabling nested hierarchies), and the full list of known user and group names for the destination auto-complete in the draft editor.

When a message is sent to a group, the Engine records which addressed groups each user was reached through. The sent message view shows this context — e.g. `USER-A (OPS)` — so the operator can see which group membership drove delivery.

**Engine default:** no known users, groups, or names for any of the three members.

**Config override:** resolves `config.json`'s `Users` map before falling back to the wrapped provider for `GetEndpoint`; merges `config.json`'s `UserGroups` over the wrapped provider's own groups for `UserGroups` (a config entry replaces a same-named group from the wrapped provider; groups only defined by the wrapped provider still pass through); and unions the wrapped provider's names with `config.json`'s `Users`/`UserGroups` keys, deduplicated and sorted, for `Users`. See [Config.md](Config.md).

**Sample override:** `SampleEngineController` overrides `Users` to return three hard-coded built-in user names, matching its built-in codes above; `config.json`'s `Users`/`UserGroups` names are still unioned in, and `GetEndpoint`/`UserGroups` still use the Engine default, applied separately at the Engine level.

---

### Ports

```csharp
int PeerPort { get; }
int InterfacePort { get; }
```

TCP port numbers for the peer listener and the local interface listener (always active, in every mode; see [Interface.md](Interface.md)).

| Port | Default |
|------|---------|
| `PeerPort` | `50021` |
| `InterfacePort` | `50020` |

**Engine default:** fixed `50021`/`50020`.

**Config override:** `config.json`'s `PeerPort`/`InterfacePort` override the wrapped provider's ports, field by field, when set. See [Config.md](Config.md).

**Sample override:** none — the Engine default plus the automatic config override already cover every genuinely useful case.

---

### Alert Settings

```csharp
string AlertLabel { get; }
TimeSpan AlarmSoundDuration { get; }
bool QuickConfirmationEnabled { get; }
bool ComposeAlertsEnabled { get; }
```

Configuration for the alert-message feature (`GetIsAlert`) in Client mode: the title bar's alarm box text (also the draft editor's alert checkbox label — both surfaces always show the same word for "alert"), how long the alarm sound plays before automatically stopping (resetting whenever a new alert arrives while already alarming), whether click/Space/Enter quick confirmation is enabled, and whether the draft editor shows its alert checkbox at all (disabling only affects local origination — the app can still receive and alarm on a peer-originated alert). See [Peer.md](Peer.md#alert-messages) and `Docs/ViewModels.md`. Actually playing the alarm sound is real platform behavior, not configuration — see [`IAlertSoundPlayer`](#ialertsoundplayer-not-a-control-interface) below.

**Engine default:** `"ALERT"` / 30 seconds / `true` / `true`.

**Config override:** `config.json`'s `AlertText`/`AlarmSoundSeconds`/`QuickConfirmationEnabled`/`ComposeAlertsEnabled` override the wrapped provider's `AlertLabel`/`AlarmSoundDuration`/`QuickConfirmationEnabled`/`ComposeAlertsEnabled` values, field by field, when set. See [Config.md](Config.md).

**Sample override:** none — the Engine default plus the automatic config override already cover every genuinely useful case.

---

### `IAlertSoundPlayer` (not a control interface)

```csharp
void Play();
void Stop();
```

Starts/stops the alarm sound itself while one or more alerts are pending. Unlike the members above, this is real OS-level audio playback, not configuration or rules, so it is a separate interface that does not live on `IEngineController` and is not registered/overridable the way control interfaces are — it lives in `Engine/src/Devices/`, and Engine always provides real behavior for it directly, the same way it always provides real behavior for printer discovery/driving (below) rather than leaving either to a host. It is `public` only because `AlertViewModel` (itself `public`) takes it as a separate constructor dependency alongside `IEngineController`, not because it is meant to be overridden.

**Engine implementation:** `AlertSoundPlayer` (`Engine/src/Devices/AlertSoundPlayer.cs`) loops a synthesized beep tone using the operating system's own audio facilities — `paplay` (PulseAudio) on Linux, `winmm.dll`'s `PlaySound` (via P/Invoke, with the same raw tone data wrapped in a WAV header) on Windows, a no-op on any other platform. Best-effort: any failure (missing binary, no audio device, unsupported platform) is swallowed so the alert box and quick confirmation still work with no sound rather than crashing the app. Not unit tested directly — inherently environment- and OS-dependent, the same reasoning as printer discovery/driving below.

---

### Message Composition

```csharp
IReadOnlyList<MessagePriorityOption> Priorities { get; }
bool TagsEnabled { get; }
string TagLabel { get; }
IReadOnlyList<TagPriorityBlock> BlockedCombinations { get; }
```

How messages are composed and displayed: the set of selectable priority levels (each a `MessagePriorityOption` pairing a display `Name` with the `Value` stored via `SetPriority` and used verbatim as the OFT send priority — larger values are sent first, see [Peer.md](Peer.md)); whether message tags (`GetTag`/`SetTag`) are shown anywhere in the UI and what the tag input's watermark says; and which tag/priority combinations are blocked outright when composing a draft (each `TagPriorityBlock` pairs an optional `Tag` — case-insensitive exact match — with an optional `Priority`; leaving either `null` matches any value for that field). The `TagPriorityBlockExtensions.IsBlocked(tag, priority)` and `MessagePriorityOptionExtensions.GetLabel(value)` extension methods evaluate a set of either type; both types and their extensions live in `Engine/src/Control/MessageComposition.cs` alongside `IEngineController`.

`DraftViewModel` enforces the blocked-combination rules proactively rather than only at send time: `AvailablePriorities` excludes any priority blocked for the currently-entered tag, and setting `Tag` to a value blocked for the currently-selected priority is rejected outright (the value reverts) — so a blocked combination can never actually be entered in the draft editor. `SendCommand` also re-checks before sending, as a defense-in-depth safety net. See `Docs/ViewModels.md`.

**Engine default:** a single `"Normal"` (value `0`) priority level; tags enabled with label `"Tag"`; no blocked combinations. Hosts that need multiple selectable priority levels or blocked combinations should override this registration.

**Config override:** `config.json`'s `MessageTagsEnabled`/`MessageTagLabel` override the wrapped provider's `TagsEnabled`/`TagLabel`, field by field, when set. See [Config.md](Config.md). `Priorities`/`BlockedCombinations` have no corresponding `config.json` field and always delegate to the wrapped provider.

**Sample override:** `SampleEngineController` overrides `Priorities` with three priority levels (`"Low"`/`"Medium"`/`"High"`, values 0/1/2) instead of the Engine default's one; demonstrates both blocked-combination kinds via `BlockedCombinations` — the `"SPAM"` tag is blocked regardless of priority, and `High` priority is blocked regardless of tag. Unlike Sample's other overrides, the blocked combinations deliberately change default behavior from the Engine's permissive "no blocks" default, since that is the only way to usefully demonstrate that part of the interface. `TagsEnabled`/`TagLabel` use the Engine default, since this override isn't meant to change them — `config.json` can still override either, applied automatically by the config override on top.

---

### `IExternalDriveProvider` (not a control interface)

```csharp
IReadOnlyList<ExternalDriveInfo> GetDrives();
```

Enumerates the external (removable/optical) drives currently available as a destination for the export feature or a source for the import feature (see `Docs/ViewModels.md`, `IExportViewModel`/`IImportViewModel`) — both share this same member and drive list. Each `ExternalDriveInfo` carries a `RootPath` (to write to or read from) and a `DisplayName` (volume label + drive name, for the drive picker); both the record and the interface live in `Engine/src/Devices/ExternalDriveProvider.cs`.

Unlike the members above, this is real OS-level behavior, not configuration or rules, so it is a separate interface that does not live on `IEngineController` and is not registered/overridable the way control interfaces are — Engine always provides real behavior for it directly, the same way it always provides real behavior for alarm sound playback (see `IAlertSoundPlayer`, above) and printer discovery/driving (see `IPrintDriver`, below).

**Engine implementation:** `ExternalDriveProvider` (`Engine/src/Devices/ExternalDriveProvider.cs`) — `DriveInfo.GetDrives()` filtered to ready `Removable`/`CDRom` drives that pass a live write probe (a small temp file is written and deleted at the drive root). Not unit tested directly — inherently environment-dependent, so a unit test could only meaningfully assert against whatever removable drives happen to be connected to the machine running the test.

---

### `IPrintDriver` (not a control interface)

```csharp
IReadOnlyList<string> GetAvailablePrinters();
string? GetDefaultPrinter();
Task PrintLine(string printerName, string line, CancellationToken cancellation = default);
Task PageFeed(string printerName, CancellationToken cancellation = default);
```

`GetAvailablePrinters`/`GetDefaultPrinter` enumerate the printers available on this computer for the print manager to target (see `Docs/ViewModels.md`, `IPrintManagerViewModel`): `GetAvailablePrinters` populates the printer picker, `GetDefaultPrinter` selects the initial `SelectedPrinter` automatically. `PrintLine`/`PageFeed` drive the selected printer for the print queue: prints one line at a time, and the returned task from `PrintLine` completing is treated as confirmation that the line finished printing — the queue will not print the next line, or check whether a higher-priority job should interrupt the current one, until it completes. `PageFeed` is called after the last line of an entry and also when a job is interrupted partway through.

Unlike the members above, this is real OS-level behavior, not configuration or rules, so it is a separate interface that does not live on `IEngineController` and is not registered/overridable the way control interfaces are — it lives in `Engine/src/Devices/`, and Engine always provides real behavior for it directly, the same way it always provides real behavior for alarm sound playback (see `IAlertSoundPlayer`, above) rather than leaving either to a host. Printer discovery is a genuine operating-system resource (like external drives, above), not app-specific configuration, and driving a printer line-by-line with real completion confirmation only makes sense against the operating system's own print spooler, not a bundled library. None of the four members has a `config.json` field.

**Engine implementation:** `PrintDriver` (`Engine/src/Devices/PrintDriver.cs`), backed by a `file`-scoped `PrintOperations` helper class (marked `[ExcludeFromCodeCoverage]`), OS-branched via `OperatingSystem.IsWindows()`/`IsLinux()`:
- **Windows:** printer discovery shells out to PowerShell, querying WMI's `Win32_Printer` class (`Get-CimInstance -ClassName Win32_Printer`) for the printer list and the entry with `Default = true` for the default printer — no extra module dependency (unlike `Get-Printer`, which requires the PrintManagement module). Line printing uses the Windows Print Spooler (WinSpool) directly via P/Invoke (`OpenPrinter`/`StartDocPrinter`/`StartPagePrinter`/`WritePrinter`/`EndPagePrinter`/`EndDocPrinter`): each line (and each page feed, sent as a form-feed byte `\f`) is submitted as its own raw print job, and `PrintLine`/`PageFeed` don't return until polling `GetJob` reports the job has reached a terminal status (`JOB_STATUS_PRINTED`, `JOB_STATUS_COMPLETE`, `JOB_STATUS_DELETED`, or `JOB_STATUS_ERROR`) — a genuine OS-confirmed completion, not just "the app handed the bytes off."
- **Linux:** printer discovery shells out to `lpstat -p`/`lpstat -d` (CUPS). Line printing submits each line (and each page feed, as `\f`) as its own raw job via `lp -d {printer} -o raw` (parsing the returned job ID from `lp`'s "request id is …" output), then polls `lpstat -W not-completed -o {printer}` until that specific job ID no longer appears among the printer's pending jobs — the CUPS-level equivalent of the same "wait for OS-confirmed completion" contract.
- **Other platforms:** printer discovery returns an empty list/no default; line printing is a no-op.
- Both platforms poll every 150ms with a 30-second-per-line safety timeout, so a stuck or offline printer cannot hang the print queue forever; discovery and printing are both best-effort — any failure (missing tooling, no printers configured, permission error) degrades gracefully (empty list / no default / a line that times out and moves on) rather than throwing.

Not unit tested directly, for the same reason `ConnectionOptions`'s certificate store lookup below isn't: both are inherently environment- and OS-dependent, so a unit test could only meaningfully assert against whatever printers happen to be installed (and reachable) on the machine running the test — `Docs/ViewModels.md`'s `PrintManagerViewModelTests` instead test the print queue's own logic (ordering, interruption, restart) against a mocked `IPrintDriver`/`IEngineController`.

---

### Print Policy

```csharp
bool PrintReceivedDefaultEnabled { get; }
int GetPrintCount(object message);
```

The print manager's automatic "print received" behavior: whether its toggle (`IPrintManagerViewModel.PrintReceivedEnabled`) starts enabled — automatically adding every received message to the print queue from the moment the app starts, though the user can still toggle it at any time — and how many times each received message is added to the print queue while it is (`0` to not print it, `1` to print it once, `2` for two copies, and so on). Consulted once per received message via `IEntryService.MessageInserted`.

Like the [Message Format](#message-format) members, `GetPrintCount` is declared `object`-typed on `IEngineController` (the boundary `PrintManagerViewModel` operates at) but implemented on `DefaultEngineController<TMessage>` via an explicit interface implementation that casts once and delegates to a type-safe `public virtual int GetPrintCount(TMessage message)` — a host override never writes an `object`-to-`TMessage` cast itself.

**Engine default:** `false` / `1` for every message.

**Config override:** `config.json`'s `PrintReceivedEnabled` overrides the wrapped provider's `PrintReceivedDefaultEnabled` when set. See [Config.md](Config.md). `GetPrintCount` has no corresponding `config.json` field and always delegates to the wrapped provider.

**Sample override:** `SampleEngineController` overrides `GetPrintCount(SampleMessage message)` to print an alert message (`GetIsAlert`) twice and every other received message once, demonstrating a rule that inspects the message itself with no cast required; `PrintReceivedDefaultEnabled` uses the Engine default.

---

### OFT Certificate

```csharp
string? GetCertificateName(string userName);
OftPeerOptions ConnectionOptions { get; }
```

`GetCertificateName` maps the local user name to a certificate subject name (CN) to look up in the system store for mutual TLS. Returning `null` disables peer authentication.

| Return value | Behaviour |
|---|---|
| `null` | Disable authentication (ephemeral cert, no client cert required) |
| Any string | Require the cert with that CN; startup throws if it is not found |

`ConnectionOptions` produces the [OFT](Oft.md) `OftPeerOptions` (certificate, certificate validation, and security mode) used for both inbound and outbound peer connections. The default implementation looks up the certificate returned by `GetCertificateName` — through virtual dispatch, so overriding just that member is enough for most customization needs — in the system certificate store, enforces chain validation (`SslPolicyErrors.None`), and selects `OftSecurityMode.DualAuthentication` when a certificate is found or `OftSecurityMode.Secure` (encrypted, unauthenticated) otherwise. `ConnectionOptions` has no `config.json` field of its own — not affected by config overrides directly, though it transitively picks up `GetCertificateName`'s config override (see below). `ConnectionOptions` is `virtual` so a host can override the whole policy directly, though for most customization needs overriding `GetCertificateName` instead is sufficient and does not require touching this security-sensitive logic at all.

Override `ConnectionOptions` only when you need custom certificate pinning, a non-store certificate source, or a different validation policy.

**Engine default:** `GetCertificateName` always returns `$"USER-{userName}"`.

**Config override:** `config.json`'s `PeerCertificateName` overrides `GetCertificateName`: `null` falls back to the wrapped provider; `"disable"` forces `null` (no authentication); an explicit name is used as-is. See [Config.md](Config.md). Because `ConfiguredEngineController.ConnectionOptions` is reimplemented (not delegated) to call its own `GetCertificateName` rather than the wrapped provider's raw one, `ConnectionOptions` correctly reflects this override even though it has no `config.json` field of its own.

**Sample override:** none, deliberately — this is the one area of `IEngineController` Sample does not override. Overriding `ConnectionOptions` directly would duplicate ~60 lines of security-sensitive X.509 store-lookup and chain-validation logic, and overriding `GetCertificateName` instead is sufficient for the vast majority of customization needs. A host that genuinely needs custom certificate pinning should override `ConnectionOptions` directly.

---

### Network Topology

```csharp
NodeRole Role { get; }
UserEndpoint? ServerEndpoint { get; }
IReadOnlyDictionary<string, ServerUserConfig> Servers { get; }
```

This instance's place in the peer/client/server networking topology — see [Peer.md](Peer.md#node-roles). `Role` selects one of `NodeRole.Peer`/`Client`/`Server` (the enum lives in `Engine/src/Control/NodeRole.cs`); `ServerEndpoint` is the server endpoint a `Client`-role instance forms its single long-term connection to (unused outside `Client`); `Servers` is the full server-user map a `Server`-role instance routes with, keyed by server user name — every server in the cluster, not just the local one (unused outside `Server`).

This is the read-only surface a host's own code can use to query the resolved topology at runtime; it is not what actually selects the `IPeerService` implementation (`PeerService`/`ClientPeerService`/`ServerRoutingService`) — that happens earlier, directly from `EngineConfig.NodeRole`, synchronously in `EngineExtensions.UseEngine`, before the container (and therefore `IEngineController`) exists to consult.

**Engine default:** always `NodeRole.Peer`, no server endpoint, no server users.

**Config override:** `config.json`'s `NodeRole` overrides `Role` when set and recognized (`"Peer"`/`"Client"`/`"Server"`, case-insensitive; an unrecognized value falls back to the wrapped provider rather than forcing `Peer`); `config.json`'s `ServerEndpoint` overrides `ServerEndpoint` when set; and `ServerUsers` merges over `Servers`'s own server users (a config entry replaces a same-named server user from the wrapped provider; server users only defined by the wrapped provider still pass through). See [Config.md](Config.md).

**Sample override:** none — the Engine default plus the automatic config override already cover every genuinely useful case.

---

### Config File

```csharp
bool ConfigFileEnabled { get; }
```

Determines whether the `--config` command-line argument is honored at all. Resolved once, before any other member — before `EngineConfig.Load` even runs — from a minimal, throwaway service provider built in `EngineApplication.Start` (see [Bootstrap resolution](#config-overrides) above and `Docs/Architecture.md`), since the real host container cannot be built until `EngineConfig` itself exists. Because of this ordering, **an `IEngineController` implementation must never depend on `EngineConfig`** — a restriction that is structural, not just a style choice, specifically for this member (every other member is likewise config-file-independent by the general rule in [Concept](#concept), but this one cannot even be config-decorated at all, on pain of circularity). When `ConfigFileEnabled` is `false`, `--config` is ignored entirely and every setting uses its default, as if the argument had never been passed.

**Engine default:** `false`.

**Config override:** none possible — there is no `config.json` field for whether `config.json` is read (that would be circular). Always delegates straight to the wrapped provider.

**Sample override:** `SampleEngineController` overrides `ConfigFileEnabled` to `true`, so Sample honors a `--config` argument. A host that wants `--config` ignored (e.g. to lock down a deployment) simply uses the Engine default instead of overriding it.

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
Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default);
Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, bool isAlert = false, int priority = 0, string tag = "", CancellationToken cancellation = default);
Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
```

High-level API for host code to interact with the running Engine. Engine registers `DirectServiceConnection`, which calls Engine services in-process, in both Client and Headless mode — Headless mode acts as a normal peer client, just without a GUI. External programs instead plug into the message stream over the local interface listener (see [Interface.md](Interface.md)), which is unrelated to this interface.

Host applications resolve `IServiceConnection` from the container to send messages, install the user, and subscribe to inbound delivery events.

**Engine default:** `DirectServiceConnection`, registered in both Client and Headless mode.

**Sample override:** none, deliberately — unlike `IEngineController`, this is not a small piece of *external configuration* a host swaps in (the "Concept" section's definition of a control interface); it is the client-facing API surface a host *consumes* to drive the running Engine, backed by `DirectServiceConnection`'s substantial in-process orchestration of Engine services. Sample resolves `IServiceConnection` directly from the container instead of replacing it. This is why it is documented in its own "Client API" section rather than as part of `IEngineController`.

---

## Supporting Type

#### `ICurrentUserProvider` / `CurrentUserProvider`

```csharp
string? UserName { get; set; }
```

Exposes the mutable user name of the currently running instance, once installed (`null` beforehand). `UserService` writes to it; `IEngineController.ConnectionOptions` (via `DefaultEngineController`'s constructor dependency) reads from it to decide which certificate to request. Registered as a singleton via convention scanning (`ICurrentUserProvider → CurrentUserProvider`), like any other `IThing`/`Thing` pair, but is not itself a control interface — it holds mutable runtime state, not configuration, and is consumed as an ordinary constructor dependency by many unrelated parts of the app (logging, peer services, `UserService`), not just by `IEngineController`.

Host code should not write to `CurrentUserProvider` directly; let `UserService` manage it.
