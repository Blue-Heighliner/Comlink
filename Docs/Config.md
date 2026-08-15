# Config File Reference

Engine supports an optional `--config <path>` argument pointing to a JSON configuration file. The `--config` argument is available in Debug builds and in Release builds that define the `ALLOW_CONFIG` compile-time constant.

```sh
Sample.exe --config path/to/config.json
```

If `--config` is omitted all fields take their defaults. If `--config` points to a non-existent or unreadable file the process throws at startup.

All property names are PascalCase; deserialization is case-insensitive. Unrecognised fields are silently ignored. Missing fields use their defaults. An empty config file (`{}`) behaves identically to omitting `--config`.

## Schema

```json
{
  "HeadlessMode":        false,
  "UserName":            null,
  "PeerPort":            50021,
  "InterfacePort":       50020,
  "DataFolder":          null,
  "PeerCertificateName": null,
  "AlertText":           null,
  "AlarmSoundSeconds":   null,
  "QuickConfirmationEnabled": null,
  "ComposeAlertsEnabled": null,
  "MessageTagsEnabled": null,
  "MessageTagLabel": null,
  "PrintReceivedEnabled": null,
  "NodeRole": null,
  "ServerEndpoint": null,
  "ServerUsers": {},
  "Users": {
    "USER-A": { "IpAddress": "192.168.1.10", "Port": 7890 }
  },
  "UserGroups": {
    "OPS": ["USER-A", "USER-B"]
  }
}
```

## Fields

### `HeadlessMode`

**Type:** `bool` | **Default:** `false`

Run the process headless — as a normal peer client, with the same local database and `IServiceConnection` as the GUI — instead of launching the desktop GUI. No window is opened. The local interface listener that lets external programs plug into this user's message stream (see [Interface.md](Interface.md)) is active regardless of this setting.

---

### `UserName`

**Type:** `string | null` | **Default:** `null`

Debug user name override. When set, `UserService` skips `State.json` entirely and uses this value as the active user name without requiring installation. Intended for development and testing only.

---

### `PeerPort`

**Type:** `int | null` | **Default:** `null` (uses Engine default of `50021`)

TCP port on which the peer-to-peer server listens for inbound messages from other nodes. Must be reachable from all peer nodes that will send messages to this user.

---

### `InterfacePort`

**Type:** `int | null` | **Default:** `null` (uses Engine default of `50020`)

TCP port on which the interface listener listens. Always active, in both GUI and headless mode. Loopback-only — intended for local programs on the same machine.

---

### `DataFolder`

**Type:** `string | null` | **Default:** `null` (`%APPDATA%\{AppName}`)

Root directory for all persistent data (LiteDB database, user state file, daily logs). Three forms are accepted:

| Value | Resolves to |
|-------|-------------|
| `null` or absent | `%APPDATA%\{AppName}` |
| Absolute path (e.g. `C:\Data\myuser`) | That exact path |
| `@`-prefixed path (e.g. `@test/user`) | `%APPDATA%\{AppName}\test\user` |

The `@` prefix is useful for running multiple instances in isolated sub-directories under the default app data folder.

---

### `PeerCertificateName`

**Type:** `string | null` | **Default:** `null` (auto-detect)

Controls which certificate is used for mutual TLS authentication on peer connections. The certificate is looked up by subject name (CN) in the system certificate store (`My` / Personal), checking CurrentUser then LocalMachine.

| Value | Behaviour |
|-------|-----------|
| `null` or absent | Auto-detect: look for a certificate named `USER-{userName}`. Throws at startup if not found. |
| `"disable"` | Disable peer authentication. Connections use an ephemeral self-signed certificate; no client certificate is required or validated. |
| Any other string | Look for a certificate with that exact subject name. Throws at startup if not found. |

When authentication is enabled both sides must present a certificate signed by a common root CA. The certificate is validated via chain trust (`SslPolicyErrors.None`). Client certificates are required.

---

### `AlertText`

**Type:** `string | null` | **Default:** `null` (uses Engine default of `"ALERT"`)

Text shown in the title bar's alert box while alarming (see `Docs/Peer.md#alert-messages`).

---

### `AlarmSoundSeconds`

**Type:** `double | null` | **Default:** `null` (uses Engine default of `30`)

Seconds the alarm sound plays after an alert is received before automatically stopping. Resets to this full duration whenever a new alert is received while already alarming.

---

### `QuickConfirmationEnabled`

**Type:** `bool | null` | **Default:** `null` (uses Engine default of `true`)

Whether clicking the alert box, or pressing Space/Enter while not focused in a text input, confirms (marks read) the latest unconfirmed alert.

---

### `ComposeAlertsEnabled`

**Type:** `bool | null` | **Default:** `null` (uses Engine default of `true`)

Whether the draft editor's alert checkbox is shown, letting the user mark and send a draft as an alert. Setting this to `false` only affects local origination — the app can still receive and alarm on an alert sent by a peer.

---

### `MessageTagsEnabled`

**Type:** `bool | null` | **Default:** `null` (uses Engine default of `true`)

Whether message tags are shown anywhere in the UI: the draft editor's tag input, and each message's tag label next to its priority in the entry listing.

---

### `MessageTagLabel`

**Type:** `string | null` | **Default:** `null` (uses Engine default of `"Tag"`)

Label used for the tag input's watermark in the draft editor. Lets a host call the concept something other than "Tag" (e.g. `"Category"`, `"Type"`) without changing engine behavior.

---

### `PrintReceivedEnabled`

**Type:** `bool | null` | **Default:** `null` (uses Engine default of `false`)

Whether the print manager's "print received" toggle starts enabled, automatically adding every received message to the print queue (subject to `IEngineController.GetPrintCount`). The user can still toggle it off at any time in the print manager.

---

### `NodeRole`

**Type:** `string | null` | **Default:** `null` (uses Engine default of `"Peer"`)

Networking topology role: `"Peer"`, `"Client"`, or `"Server"` (case-insensitive). `null` or an unrecognized value uses `"Peer"` — direct peer-to-peer networking, unchanged from prior versions. See [Peer.md](Peer.md#node-roles) for the full description of each role.

---

### `ServerEndpoint`

**Type:** `object | null` | **Default:** `null`

The server endpoint a `"Client"`-role instance forms its long-term connection through. Required when `NodeRole` is `"Client"`; ignored otherwise.

```json
"ServerEndpoint": { "IpAddress": "10.0.0.1", "Port": 50021 }
```

| Field | Type | Description |
|-------|------|-------------|
| `IpAddress` | `string` | IPv4 or IPv6 address of the server |
| `Port` | `int` | TCP port the server listens on |

---

### `ServerUsers`

**Type:** `object` | **Default:** `{}`

The full server-user-map topology for a `"Server"`-role instance: a map of server user name → endpoint and child client list. Describes **every** server in the cluster, not just the local one — see [Peer.md](Peer.md#server). Required (with at least an entry for the local server user) when `NodeRole` is `"Server"`; ignored otherwise.

```json
"ServerUsers": {
  "SERVER-A": { "IpAddress": "10.0.0.1", "Port": 50021, "ChildClients": ["CLIENT-A1", "CLIENT-A2"] },
  "SERVER-B": { "IpAddress": "10.0.0.2", "Port": 50021, "ChildClients": ["CLIENT-B1"] }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `IpAddress` | `string` | IPv4 or IPv6 address this server user listens on and other servers dial to reach it |
| `Port` | `int` | TCP port this server user listens on and other servers dial to reach it |
| `ChildClients` | `string[]` | Names of the client users that belong to this server |

---

### `Users`

**Type:** `object` | **Default:** `{}`

A map of user name → endpoint used by the `IEngineController.GetEndpoint` implementation. Keys are user names (case-insensitive). Entries may override the endpoint of an existing known user or introduce an entirely new user.

```json
"Users": {
  "USER-A": { "IpAddress": "192.168.1.10", "Port": 7890 },
  "NEW-USER": { "IpAddress": "192.168.1.12", "Port": 7890 }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `IpAddress` | `string` | IPv4 or IPv6 address of the remote node |
| `Port` | `int` | TCP port of the remote node's peer server |

---

### `UserGroups`

**Type:** `object` | **Default:** `{}`

A map of group name → member list. Members may be user names or other group names, enabling nested hierarchies. Groups appear as addressable destinations in the draft editor alongside individual users. When a message is addressed to a group, the Engine expands it recursively and delivers the message to every contained user exactly once.

```json
"UserGroups": {
  "OPS": ["USER-A", "USER-B"],
  "ALL": ["OPS", "NEW-USER"]
}
```

Sending to `ALL` delivers to `USER-A`, `USER-B`, and `NEW-USER`. Cycles are ignored.

## Examples

### Production (authenticated, GUI)

```json
{
  "PeerPort": 50021,
  "Users": {
    "USER-B": { "IpAddress": "192.168.1.11", "Port": 50021 }
  }
}
```

`PeerCertificateName` is absent so authentication uses `USER-{userName}` auto-detection.

### Development (unauthenticated, Headless mode)

```json
{
  "HeadlessMode": true,
  "UserName": "TEST1",
  "PeerPort": 50020,
  "InterfacePort": 50021,
  "DataFolder": "@TEST1",
  "PeerCertificateName": "disable",
  "Users": {
    "TEST2": { "IpAddress": "127.0.0.1", "Port": 50030 }
  }
}
```

### Groups with nested membership

```json
{
  "Users": {
    "USER-A": { "IpAddress": "10.0.0.1", "Port": 50021 },
    "USER-B": { "IpAddress": "10.0.0.2", "Port": 50021 }
  },
  "UserGroups": {
    "WEST": ["USER-A", "USER-B"],
    "ALL":  ["WEST", "USER-C"]
  }
}
```

### Named certificate override

```json
{
  "PeerCertificateName": "MY-CUSTOM-CERT",
  "Users": {
    "USER-B": { "IpAddress": "192.168.1.11", "Port": 50021 }
  }
}
```

### Client/Server hierarchy

A client, running as user `CLIENT-A1`, pointed at its server:

```json
{
  "UserName": "CLIENT-A1",
  "NodeRole": "Client",
  "ServerEndpoint": { "IpAddress": "10.0.0.1", "Port": 50021 }
}
```

The server it connects to, running as user `SERVER-A`, with the full cluster's topology — including the other server, `SERVER-B`, and its own children:

```json
{
  "UserName": "SERVER-A",
  "NodeRole": "Server",
  "ServerUsers": {
    "SERVER-A": { "IpAddress": "10.0.0.1", "Port": 50021, "ChildClients": ["CLIENT-A1", "CLIENT-A2"] },
    "SERVER-B": { "IpAddress": "10.0.0.2", "Port": 50021, "ChildClients": ["CLIENT-B1"] }
  }
}
```
