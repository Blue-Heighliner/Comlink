# Configuration Interfaces

All external configuration points are expressed as interfaces in `Engine/src/Control/`. The engine registers defaults for each using `TryAddSingleton`, so any host can override by registering its own implementation.

## Interfaces

### `IAppNameProvider`
```csharp
string AppName { get; }
```
Human-readable application name. Used in log output and the default app data path. Default: `"App"`.

---

### `IAppDataPathProvider`
```csharp
string AppDataPath { get; }
```
Root directory for all persistent data (database, state file, logs). Default: `%APPDATA%/{AppName}`. Can be overridden via `--data-folder` in the Sample host.

---

### `IPortConfiguration`
```csharp
int InterfacePort { get; }   // default 50020
int PeerPort      { get; }   // default 50021
```
Network ports for the local interface listener (always active, in every mode; see [Interface.md](Interface.md)) and the peer-to-peer listener. The peer port must be reachable by other nodes; the interface port is loopback-only.

---

### `ISiteLocator`
```csharp
Task<SiteEndpoint?> GetEndpoint(string siteName, CancellationToken cancellation = default);
```
Resolves a site name to a `SiteEndpoint` (`IpAddress`, `Port`). Called by `PeerService` when opening an outbound peer connection. No default — must be provided by the host.

---

### `ISiteCodeResolver`
```csharp
Task<SiteInfo?> Resolve(string siteCode, CancellationToken cancellation = default);
```
Resolves an installation code to a `SiteInfo` (name, environment title, color). Called during site installation. No default — must be provided by the host.

---

### `ISiteNameDirectory`
```csharp
Task<IReadOnlyList<string>> GetAllSiteNames(CancellationToken cancellation = default);
```
Returns all known site names. Used to populate address autocomplete in the UI and `IServiceConnection.GetSiteNames`. No default — must be provided by the host.

---

### `IHomeContentProvider`
```csharp
string GetHomeText();
```
Text shown on the home screen in Client mode. No default — must be provided by the host.

---

### `IKioskModeProvider`
```csharp
bool IsKioskMode { get; }
```
When `true`, the UI hides navigation and limits functionality to a single-purpose view. Default: `false`.

---

### `IDebugSiteOverride`
```csharp
string SiteName { get; }
```
When registered, `SiteService` skips `State.json` and uses this site name directly without installing. Intended for development/testing. Only one instance is expected; the first registered wins.

---

### `IOftCertificateProvider`
```csharp
OftPeerOptions GetPeerOptions();
```
Produces the [OFT](Oft.md) `OftPeerOptions` used by the peer layer. The default implementation delegates cert name resolution to `IOftPeerCertificateName`, looks up the matching certificate in the system store (`StoreName.My`), and enables mutual TLS (`OftSecurityMode.DualAuthentication`) with chain validation when a certificate is found. When no certificate is found the connection runs unauthenticated (`OftSecurityMode.Secure` — ephemeral server cert, no client cert required).

---

### `IOftPeerCertificateName`
```csharp
string? GetCertificateName(string siteName);
```
Maps a site name to the certificate subject name that should be located in the system cert store. Returning `null` disables peer authentication. Default: `"SITE-{siteName}"`.

The Sample host reads `PeerCertificateName` from `config.json` and uses it as follows:

| Config value | Behaviour |
|---|---|
| Absent / `null` | Auto-detect: look for `SITE-{siteName}` in system store. Throws at startup if not found. |
| `"disable"` | Disable peer authentication (ephemeral cert, no client cert required) |
| Any other string | Look for a cert with that exact subject name. Throws at startup if not found. |

---

### `IMessageFormat`
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
Supplies the concrete message type used throughout the engine — on the wire (peer and interface connections) and in the database — and maps the engine's logical fields onto that type's real fields. No default — the engine has no message DTO of its own and fails at startup if a host does not register an implementation. See [Peer.md](Peer.md#message-format).

---

## Internal Providers

These are internal to the engine and not overridable from the host:

### `CurrentSiteProvider`
Mutable string holding the currently active site name. Shared between `SiteService`, `OftCertificateProvider`, and the logging system. Updated when a site is installed or loaded.
