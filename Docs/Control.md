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

---

#### `IAppNameProvider`

```csharp
string AppName { get; }
```

The application name used as the default data folder name and in log headers.

**Engine default:** derives from entry assembly name (via `AppNameProvider`)

**Sample override:** `SampleAppNameProvider` — returns `"Sample"`, so data lands in `%APPDATA%\Sample`.

---

#### `IAppDataPathProvider`

```csharp
string AppDataPath { get; }
```

Absolute path to the root data directory. All persistent state (LiteDB, user state, logs) is written under this path.

**Engine default:** `%APPDATA%\{AppName}` when `DataFolder` is null; supports absolute paths and the `@`-prefix shorthand (see [Config.md](Config.md)) via `AppDataPathProvider`

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

---

#### `IKioskModeProvider`

```csharp
bool IsKioskMode { get; }
```

When `true`, the main window hides its chrome (title bar, resize handles) and restricts navigation to prevent the user from leaving the application. Intended for locked-down deployments.

**Engine default:** `false` (via `KioskModeProvider`)

**Sample override:** none — Sample uses the Engine default.

---

#### `IAlertConfiguration`

```csharp
string AlertText { get; }
TimeSpan AlarmSoundDuration { get; }
bool QuickConfirmationEnabled { get; }
```

Configures the alarm triggered by alert messages (`IMessageFormat.GetIsAlert`) in Client mode: the text shown in the title bar's alert box, how long the alarm sound plays before automatically stopping (resetting whenever a new alert arrives), and whether click/Space/Enter quick confirmation is enabled. See [Peer.md](Peer.md#alert-messages) and `Docs/ViewModels.md`.

**Engine default:** reads `AlertText`/`AlarmSoundSeconds`/`QuickConfirmationEnabled` from `config.json`, falling back to `"ALERT"` / 30 seconds / `true` (via `AlertConfiguration`). See [Config.md](Config.md).

**Sample override:** none — Sample uses the Engine default.

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

#### `IExternalDriveProvider`

```csharp
IReadOnlyList<ExternalDriveInfo> GetDrives();
```

Enumerates the external (removable/optical) drives currently available as a destination for the export feature or a source for the import feature (see `Docs/ViewModels.md`, `IExportViewModel`/`IImportViewModel`) — both share this same provider and drive list. Each `ExternalDriveInfo` carries a `RootPath` (to write to or read from) and a `DisplayName` (volume label + drive name, for the drive picker).

**Engine default:** `DriveInfo.GetDrives()` filtered to ready `Removable`/`CDRom` drives that pass a live write probe (a small temp file is written and deleted at the drive root) (via `ExternalDriveProvider`).

**Sample override:** none — Sample uses the Engine default.

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

---

#### `IOftCertificateProvider`

```csharp
OftPeerOptions GetPeerOptions();
```

Produces the [OFT](Oft.md) `OftPeerOptions` (certificate, certificate validation, and security mode) used for both inbound and outbound peer connections. The default implementation looks up the certificate returned by `IOftPeerCertificateName` in the system certificate store, enforces chain validation (`SslPolicyErrors.None`), and selects `OftSecurityMode.DualAuthentication` when a certificate is found or `OftSecurityMode.Secure` (encrypted, unauthenticated) otherwise.

Override this interface only when you need custom certificate pinning, a non-store certificate source, or a different validation policy. In most cases, overriding `IOftPeerCertificateName` is sufficient.

**Engine default:** `OftCertificateProvider`

**Sample override:** none — Sample uses the Engine default.

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
```

Supplies the concrete message type used throughout the engine — transmitted between peers and interfaces (see [Peer.md](Peer.md#message-format) and [Interface.md](Interface.md)) and stored in the database (see [Data.md](Data.md)) — and maps the engine's logical fields onto that type's real ones. `MessageType` must be protobuf-net serializable (`[ProtoContract]`/`[ProtoMember]`) for wire transport and LiteDB-serializable for storage. `GetConfirmationMessageId`/`SetConfirmationMessageId` and `GetIsAlert`/`SetIsAlert` back the user-read confirmation and alert-message features — see [Peer.md](Peer.md#read-confirmation) and [Peer.md](Peer.md#alert-messages).

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
Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, bool isAlert = false, CancellationToken cancellation = default);
Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
```

High-level API for host code to interact with the running Engine. Engine registers `DirectServiceConnection`, which calls Engine services in-process, in both Client and Headless mode — Headless mode acts as a normal peer client, just without a GUI. External programs instead plug into the message stream over the local interface listener (see [Interface.md](Interface.md)), which is unrelated to this interface.

Host applications resolve `IServiceConnection` from the container to send messages, install the user, and subscribe to inbound delivery events.

**Engine default:** `DirectServiceConnection`, registered in both Client and Headless mode.

**Sample override:** none — Sample resolves `IServiceConnection` directly from the container.

---

### Supporting Type

#### `CurrentUserProvider`

Not an interface — a plain `public sealed class` registered as a singleton. Holds `UserName` (the active user name once installed, `null` beforehand). `UserService` writes to it; `OftCertificateProvider` reads from it to decide which certificate to request.

Host code should not write to `CurrentUserProvider` directly; let `UserService` manage it.
