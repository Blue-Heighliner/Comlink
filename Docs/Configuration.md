# Configuration Interfaces

All external configuration points are expressed as interfaces in `Engine/src/Control/`. The engine registers defaults for each using `TryAddSingleton`, so any host can override by registering its own implementation. See [Control.md](Control.md) for the full reference — including the `Configured*` decorators that apply `config.json` on top of whichever implementation is registered, the `Default*` naming convention (virtual members a host can inherit and override just one of), and which interfaces have no default at all.

## Interfaces

### `IAppSettings`
```csharp
string AppName { get; }
string AppDataPath { get; }
bool IsKioskMode { get; }
string GetHomeText();
```
This app's own identity and top-level presentation: display/data-folder name, the root directory persistent data (database, state file, logs) is written under, whether the UI runs in kiosk mode, and the home screen's placeholder text. Default: entry assembly name / `%APPDATA%/{AppName}` (`AppDataPath` reads `AppName` through virtual dispatch, so overriding `AppName` alone keeps them in sync) / `false` / `"HOME"`. `AppDataPath` is overridable via `config.json`'s `DataFolder`.

---

### `IPortConfiguration`
```csharp
int InterfacePort { get; }   // default 50020
int PeerPort      { get; }   // default 50021
```
Network ports for the local interface listener (always active, in every mode; see [Interface.md](Interface.md)) and the peer-to-peer listener. The peer port must be reachable by other nodes; the interface port is loopback-only.

---

### `IUserDirectory`
```csharp
Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default);
Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default);
Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default);
```
Everything the engine knows about addressable users and groups: resolving a user name to its peer endpoint (called by `PeerService` when opening an outbound connection), group membership for address expansion, and the full list of known user/group names (used to populate address autocomplete and `IServiceConnection.GetUserNames`). Default: none known for any of the three: overridable via `config.json`'s `Users`/`UserGroups`.

---

### `IUserIdentity`
```csharp
string? DebugUserName { get; }
Task<UserInfo?> ResolveCode(string userCode, CancellationToken cancellation = default);
```
How this instance's own local user identity is established: a fixed debug override that bypasses `State.json` (`config.json`'s `UserName`), and resolving a user activation code to a `UserInfo` during installation. Default: no debug override; only the code `"CODE"` resolves (to user `"TEST"`).

---

### `IHomeContentProvider` / `IKioskModeProvider` / `IAppNameProvider` / `IAppDataPathProvider` / `IDebugUserOverride` / `IUserCodeResolver` / `IUserLocator` / `IUserNameDirectory`

Consolidated into `IAppSettings`, `IUserIdentity`, and `IUserDirectory` above — these no longer exist as separate interfaces. See [Control.md](Control.md) for the full mapping.

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
int GetPriority(object message);
void SetPriority(object message, int value);
string GetTag(object message);
void SetTag(object message, string value);
```
Supplies the concrete message type used throughout the engine — on the wire (peer and interface connections) and in the database — and maps the engine's logical fields onto that type's real fields. No default — the engine has no message DTO of its own and fails at startup if a host does not register an implementation. See [Peer.md](Peer.md#message-format).

---

### `IAlertSettings`
```csharp
string AlertText { get; }
TimeSpan AlarmSoundDuration { get; }
bool QuickConfirmationEnabled { get; }
bool ComposeAlertsEnabled { get; }
```
Configuration for the alert-message feature: the title bar's alarm box text (and draft editor's alert checkbox label), how long the alarm sound plays before auto-stopping, whether click/Space/Enter quick confirmation is enabled, and whether the draft editor can originate alerts at all. Default: `"ALERT"` / 30 seconds / `true` / `true` — all four are overridable via `config.json`. See [Peer.md](Peer.md#alert-messages). Actually playing the alarm sound is real platform behavior, not configuration — see `IAlertSoundPlayer` in [Control.md](Control.md), which is not a control interface and is always provided by the engine itself.

---

### `IMessageComposition`
```csharp
IReadOnlyList<MessagePriorityOption> GetPriorities();
bool TagsEnabled { get; }
string TagLabel { get; }
IReadOnlyList<TagPriorityBlock> GetBlockedCombinations();
```
How messages are composed and displayed: selectable priority levels, whether message tags are shown and what the tag input is labeled, and which tag/priority combinations are blocked outright. Default: a single `"Normal"` (0) priority level / tags enabled with label `"Tag"` / no blocked combinations. `TagsEnabled`/`TagLabel` are overridable via `config.json`.

---

### `IPrintPolicy`
```csharp
bool PrintReceivedDefaultEnabled { get; }
int GetPrintCount(object message);
```
The print manager's automatic "print received" behavior: whether it starts enabled, and how many copies of each received message to add to the print queue while it is. Default: disabled / `1` copy per message. `PrintReceivedDefaultEnabled` is overridable via `config.json`.

---

### `INetworkTopology`
```csharp
NodeRole Role { get; }
UserEndpoint? GetServerEndpoint();
Task<IReadOnlyDictionary<string, ServerUserConfig>> GetServerUsers(CancellationToken cancellation = default);
```
This instance's place in the peer/client/server networking topology — see [Peer.md](Peer.md#node-roles). Default: `NodeRole.Peer`, no server endpoint, no server users. Overridable via `config.json`'s `NodeRole`/`ServerEndpoint`/`ServerUsers`.

---

### `IConfigFileProvider`
```csharp
bool Enabled { get; }
```
Whether the `--config` command-line argument is honored at all. Resolved before `EngineConfig` itself exists, so an implementation must never depend on it. Default: `true`.

---

## Internal Providers

These are internal to the engine and not overridable from the host:

### `CurrentUserProvider`
Mutable string holding the currently active user name. Shared between `UserService`, `OftCertificateProvider`, and the logging system. Updated when a user is installed or loaded.
