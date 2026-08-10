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
  "SiteName":            null,
  "PeerPort":            50021,
  "InterfacePort":       50020,
  "DataFolder":          null,
  "PeerCertificateName": null,
  "Sites": {
    "SITE-A": { "IpAddress": "192.168.1.10", "Port": 7890 }
  },
  "SiteGroups": {
    "OPS": ["SITE-A", "SITE-B"]
  }
}
```

## Fields

### `HeadlessMode`

**Type:** `bool` | **Default:** `false`

Run the process headless — as a normal peer client, with the same local database and `IServiceConnection` as the GUI — instead of launching the desktop GUI. No window is opened. The local interface listener that lets external programs plug into this site's message stream (see [Interface.md](Interface.md)) is active regardless of this setting.

---

### `SiteName`

**Type:** `string | null` | **Default:** `null`

Debug site name override. When set, `SiteService` skips `State.json` entirely and uses this value as the active site name without requiring installation. Intended for development and testing only.

---

### `PeerPort`

**Type:** `int | null` | **Default:** `null` (uses Engine default of `50021`)

TCP port on which the peer-to-peer server listens for inbound messages from other nodes. Must be reachable from all peer nodes that will send messages to this site.

---

### `InterfacePort`

**Type:** `int | null` | **Default:** `null` (uses Engine default of `50020`)

TCP port on which the interface listener listens. Always active, in both GUI and headless mode. Loopback-only — intended for local programs on the same machine.

---

### `DataFolder`

**Type:** `string | null` | **Default:** `null` (`%APPDATA%\{AppName}`)

Root directory for all persistent data (LiteDB database, site state file, daily logs). Three forms are accepted:

| Value | Resolves to |
|-------|-------------|
| `null` or absent | `%APPDATA%\{AppName}` |
| Absolute path (e.g. `C:\Data\mysite`) | That exact path |
| `@`-prefixed path (e.g. `@test/site`) | `%APPDATA%\{AppName}\test\site` |

The `@` prefix is useful for running multiple instances in isolated sub-directories under the default app data folder.

---

### `PeerCertificateName`

**Type:** `string | null` | **Default:** `null` (auto-detect)

Controls which certificate is used for mutual TLS authentication on peer connections. The certificate is looked up by subject name (CN) in the system certificate store (`My` / Personal), checking CurrentUser then LocalMachine.

| Value | Behaviour |
|-------|-----------|
| `null` or absent | Auto-detect: look for a certificate named `SITE-{siteName}`. Throws at startup if not found. |
| `"disable"` | Disable peer authentication. Connections use an ephemeral self-signed certificate; no client certificate is required or validated. |
| Any other string | Look for a certificate with that exact subject name. Throws at startup if not found. |

When authentication is enabled both sides must present a certificate signed by a common root CA. The certificate is validated via chain trust (`SslPolicyErrors.None`). Client certificates are required.

---

### `Sites`

**Type:** `object` | **Default:** `{}`

A map of site name → endpoint used by the `ISiteLocator` implementation. Keys are site names (case-insensitive). Entries may override the endpoint of an existing known site or introduce an entirely new site that does not appear in environment variables.

```json
"Sites": {
  "SITE-A": { "IpAddress": "192.168.1.10", "Port": 7890 },
  "NEW-SITE": { "IpAddress": "192.168.1.12", "Port": 7890 }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `IpAddress` | `string` | IPv4 or IPv6 address of the remote node |
| `Port` | `int` | TCP port of the remote node's peer server |

---

### `SiteGroups`

**Type:** `object` | **Default:** `{}`

A map of group name → member list. Members may be site names or other group names, enabling nested hierarchies. Groups appear as addressable destinations in the draft editor alongside individual sites. When a message is addressed to a group, the Engine expands it recursively and delivers the message to every contained site exactly once.

```json
"SiteGroups": {
  "OPS": ["SITE-A", "SITE-B"],
  "ALL": ["OPS", "NEW-SITE"]
}
```

Sending to `ALL` delivers to `SITE-A`, `SITE-B`, and `NEW-SITE`. Cycles are ignored.

## Examples

### Production (authenticated, GUI)

```json
{
  "PeerPort": 50021,
  "Sites": {
    "SITE-B": { "IpAddress": "192.168.1.11", "Port": 50021 }
  }
}
```

`PeerCertificateName` is absent so authentication uses `SITE-{siteName}` auto-detection.

### Development (unauthenticated, Headless mode)

```json
{
  "HeadlessMode": true,
  "SiteName": "TEST1",
  "PeerPort": 50020,
  "InterfacePort": 50021,
  "DataFolder": "@TEST1",
  "PeerCertificateName": "disable",
  "Sites": {
    "TEST2": { "IpAddress": "127.0.0.1", "Port": 50030 }
  }
}
```

### Groups with nested membership

```json
{
  "Sites": {
    "SITE-A": { "IpAddress": "10.0.0.1", "Port": 50021 },
    "SITE-B": { "IpAddress": "10.0.0.2", "Port": 50021 }
  },
  "SiteGroups": {
    "WEST": ["SITE-A", "SITE-B"],
    "ALL":  ["WEST", "SITE-C"]
  }
}
```

### Named certificate override

```json
{
  "PeerCertificateName": "MY-CUSTOM-CERT",
  "Sites": {
    "SITE-B": { "IpAddress": "192.168.1.11", "Port": 50021 }
  }
}
```
