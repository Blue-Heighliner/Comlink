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

### `IUserLocator`
```csharp
Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default);
```
Resolves a user name to a `UserEndpoint` (`IpAddress`, `Port`). Called by `PeerService` when opening an outbound peer connection. No default — must be provided by the host.

---

### `IUserCodeResolver`
```csharp
Task<UserInfo?> Resolve(string userCode, CancellationToken cancellation = default);
```
Resolves an installation code to a `UserInfo` (name, environment title, color). Called during user installation. No default — must be provided by the host.

---

### `IUserNameDirectory`
```csharp
Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default);
```
Returns all known user names. Used to populate address autocomplete in the UI and `IServiceConnection.GetUserNames`. No default — must be provided by the host.

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

### `IDebugUserOverride`
```csharp
string UserName { get; }
```
When registered, `UserService` skips `State.json` and uses this user name directly without installing. Intended for development/testing. Only one instance is expected; the first registered wins.

---

### `IOftCertificateProvider`
```csharp
OftPeerOptions GetPeerOptions();
```
Produces the [OFT](Oft.md) `OftPeerOptions` used by the peer layer. The default implementation delegates cert name resolution to `IOftPeerCertificateName`, looks up the matching certificate in the system store (`StoreName.My`), and enables mutual TLS (`OftSecurityMode.DualAuthentication`) with chain validation when a certificate is found. When no certificate is found the connection runs unauthenticated (`OftSecurityMode.Secure` — ephemeral server cert, no client cert required).

---

### `IOftPeerCertificateName`
```csharp
string? GetCertificateName(string userName);
```
Maps a user name to the certificate subject name that should be located in the system cert store. Returning `null` disables peer authentication. Default: `"USER-{userName}"`.

The Sample host reads `PeerCertificateName` from `config.json` and uses it as follows:

| Config value | Behaviour |
|---|---|
| Absent / `null` | Auto-detect: look for `USER-{userName}` in system store. Throws at startup if not found. |
| `"disable"` | Disable peer authentication (ephemeral cert, no client cert required) |
| Any other string | Look for a cert with that exact subject name. Throws at startup if not found. |

---

### `IMessageFormat`
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
Supplies the concrete message type used throughout the engine — on the wire (peer and interface connections) and in the database — and maps the engine's logical fields onto that type's real fields. No default — the engine has no message DTO of its own and fails at startup if a host does not register an implementation. See [Peer.md](Peer.md#message-format).

---

### `IAlertConfiguration`
```csharp
string AlertText { get; }
TimeSpan AlarmSoundDuration { get; }
bool QuickConfirmationEnabled { get; }
```
Configures the title bar's alarm box text, how long the alarm sound plays before auto-stopping, and whether click/Space/Enter quick confirmation is enabled. Default: `"ALERT"` / 30 seconds / `true`, overridable via `config.json`. See [Peer.md](Peer.md#alert-messages).

---

### `IAlertSoundPlayer`
```csharp
void Play();
void Stop();
```
Starts/stops the looping alarm sound for alert messages. Default: silent no-op — audio playback is platform-specific, so a host must register an implementation for real sound.

---

## Internal Providers

These are internal to the engine and not overridable from the host:

### `CurrentUserProvider`
Mutable string holding the currently active user name. Shared between `UserService`, `OftCertificateProvider`, and the logging system. Updated when a user is installed or loaded.
