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

#### `ISiteCodeResolver`

```csharp
Task<SiteInfo?> Resolve(string siteCode, CancellationToken cancellation = default);
```

Converts a site activation code (entered by the user during installation) into a `SiteInfo` record, or returns `null` if the code is unrecognised. The result sets the site name, display environment title, and environment accent color.

**Engine default:** accepts code `"CODE"` → site `"TEST"` (via `SiteCodeResolver`)

**Sample override:** `SampleSiteCodeResolver` — checks hard-coded codes (`CODE1`/`CODE2`/`CODE3`) and falls back to `SITE_{CODE}_NAME` environment variables.

---

#### `ISiteLocator`

```csharp
Task<SiteEndpoint?> GetEndpoint(string siteName, CancellationToken cancellation = default);
```

Resolves a site name to its TCP peer endpoint (`IpAddress` + `Port`) for outbound P2P delivery. Returns `null` when the site is unknown and the message cannot be delivered.

**Engine default:** resolves sites defined in `config.json` `Sites`; returns `null` for unknown names (via `SiteLocator`)

**Sample override:** `SampleSiteLocator` — checks the `Sites` map from `config.json` first, then falls back to `PEER_{SITENAME}=ip:port` environment variables.

---

#### `ISiteNameDirectory`

```csharp
Task<IReadOnlyList<string>> GetAllSiteNames(CancellationToken cancellation = default);
```

Returns the names of every known site **and group** in the messaging system. Used to populate the destination auto-complete in the draft editor.

**Engine default:** returns site names from `config.json` `Sites` and group names from `SiteGroups` (via `SiteNameDirectory`)

**Sample override:** `SampleSiteNameDirectory` — unions config sites/groups with sites inferred from `PEER_*` environment variables.

---

#### `ISiteGroupProvider`

```csharp
Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default);
```

Returns all defined site groups as a map of group name → member names. Members may be site names or other group names, enabling nested group hierarchies. Engine uses this to expand group addresses to their constituent sites before delivery, deduplicating across overlapping groups and direct addresses.

When a message is sent to a group, the Engine records which addressed groups each site was reached through. The sent message view shows this context — e.g. `SITE-A (OPS)` — so the operator can see which group membership drove delivery.

**Engine default:** reads `SiteGroups` from `config.json`; returns empty map when no groups are defined (via `SiteGroupProvider`)

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

Absolute path to the root data directory. All persistent state (LiteDB, site state, logs) is written under this path.

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

#### `IOftPeerCertificateName`

```csharp
string? GetCertificateName(string siteName);
```

Maps the local site name to a certificate subject name (CN) to look up in the system store for mutual TLS. Returning `null` disables peer authentication.

| Return value | Behaviour |
|---|---|
| `null` | Disable authentication (ephemeral cert, no client cert required) |
| Any string | Require the cert with that CN; startup throws if it is not found |

**Engine default:** `$"SITE-{siteName}"` when `PeerCertificateName` is null; `"disable"` → `null`; explicit string → use it as-is (via `OftPeerCertificateName`). See [Config.md](Config.md).

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
string GetFromSite(object message);
void SetFromSite(object message, string value);
string GetSubject(object message);
void SetSubject(object message, string value);
string GetBody(object message);
void SetBody(object message, string value);
List<MessageAddress> GetAddresses(object message);
void SetAddresses(object message, List<MessageAddress> value);
DateTime GetSentAt(object message);
void SetSentAt(object message, DateTime value);
```

Supplies the concrete message type used throughout the engine — transmitted between peers and interfaces (see [Peer.md](Peer.md#message-format) and [Interface.md](Interface.md)) and stored in the database (see [Data.md](Data.md)) — and maps the engine's logical fields onto that type's real ones. `MessageType` must be protobuf-net serializable (`[ProtoContract]`/`[ProtoMember]`) for wire transport and LiteDB-serializable for storage.

The engine has no message DTO of its own; every access to a message's content goes through this interface, so a host must register an implementation to use the engine at all.

**Engine default:** none.

**Sample implementation:** `SampleMessageFormat`, backed by a `SampleMessage` type, demonstrating the mapping. See `Sample/src/SampleMessageFormat.cs`.

---

### Conditional / Auxiliary

---

#### `IDebugSiteOverride`

```csharp
string? SiteName { get; }
```

Supplies a fixed site name to `SiteService`, bypassing the normal `State.json` lookup. Injected as `IEnumerable<IDebugSiteOverride>` so registering multiple implementations is valid. When any registered implementation returns a non-null `SiteName`, `SiteService` uses that name instead of reading from disk — the site is considered permanently installed.

The Engine default (`DebugSiteOverride`) returns `config.SiteName`, which is `null` when not set and therefore has no effect. Intended for development and testing only.

---

### Client API

---

#### `IServiceConnection`

```csharp
event Func<MessageReceivedEvent, Task>? MessageReceived;
event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;
Task Connect(CancellationToken cancellation = default);
Task<SiteInfo?> GetSiteInfo(CancellationToken cancellation = default);
Task<List<string>> GetSiteNames(CancellationToken cancellation = default);
Task<SiteInfo?> InstallSite(string siteCode, CancellationToken cancellation = default);
Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, CancellationToken cancellation = default);
```

High-level API for host code to interact with the running Engine. Engine registers `DirectServiceConnection`, which calls Engine services in-process, in both Client and Headless mode — Headless mode acts as a normal peer client, just without a GUI. External programs instead plug into the message stream over the local interface listener (see [Interface.md](Interface.md)), which is unrelated to this interface.

Host applications resolve `IServiceConnection` from the container to send messages, install the site, and subscribe to inbound delivery events.

**Engine default:** `DirectServiceConnection`, registered in both Client and Headless mode.

**Sample override:** none — Sample resolves `IServiceConnection` directly from the container.

---

### Supporting Type

#### `CurrentSiteProvider`

Not an interface — a plain `public sealed class` registered as a singleton. Holds `SiteName` (the active site name once installed, `null` beforehand). `SiteService` writes to it; `OftCertificateProvider` reads from it to decide which certificate to request.

Host code should not write to `CurrentSiteProvider` directly; let `SiteService` manage it.
