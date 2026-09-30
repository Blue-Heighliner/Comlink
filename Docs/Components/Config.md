# Network Configuration File Reference

The engine defines the schema of one JSON file that describes a whole network: every user, their role, ports and connections, who is in which group, and the trusted certificate authority. It is shared by every node of the network, so nothing about a user is stated in code or in per-node files. It works identically in every build configuration; `Config.json` in the working directory is always read, and whether the command-line arguments below may override it is decided solely by the host, through `IEngineBuilder.CommandLineOverrides(bool)` (see [Configuration.md](Configuration.md#command-line-overrides)); they are ignored unless the host allows them.

```sh
Sample.exe --config path/to/Config.json --user CLIENT1
```

- `--config <path>` names the file (only when the host allows command-line overrides). When omitted, `Config.json` in the current working directory is used if it exists, and otherwise the network is empty. A `--config` path that does not exist or cannot be read makes the process throw at startup.
- `--user <name>` (also only when overrides are allowed) names the user this process runs as, so a node starts as that user without the install screen and uses that user's `Headless` choice. Without the argument, a `User.json` in the current working directory names the user instead, holding `{ "User": "USER-A" }` (or just the name as a JSON string). Without either the user is the one installed through the install screen (an install code is the name of a user in this file, case-insensitive), which is remembered between runs.

The file is read again while the application runs when the user right-clicks their name in the title bar and chooses "Refresh"; see [Configuration.md](Configuration.md#network-configuration-file) for what that applies. Property names are PascalCase; deserialization is case-insensitive, and so are user names. Unrecognised fields are silently ignored and missing fields use their defaults. An empty file (`{}`) is an empty network. By convention user names are all uppercase.

## Schema

```json
{
  "TrustedAuthorityCertificateName": null,
  "AuthorityCertificate": "../Root.cer",
  "CertificateStore": ".",
  "UserGroups": {
    "OPS": [ "USER-A", "USER-B" ]
  },
  "Users": {
    "USER-A": {
      "Role": "Peer",
      "PeerPort": 50021,
      "InterfacePort": 50020,
      "OutgoingPoints": [ { "IpAddress": "10.0.0.1", "Port": 50021 } ],
      "ChildClients": [],
      "StoresMessages": false,
      "SecurityLevel": null,
      "CertificateName": null,
      "Data": { "role": "clerk" },

      "Headless": false,
      "AlertText": null,
      "AlarmSoundSeconds": null,
      "QuickConfirmationEnabled": null,
      "ComposeAlertsEnabled": null,
      "MessageTagsEnabled": null,
      "MessageTagLabel": null,
      "PrintReceivedEnabled": null
    }
  }
}
```

## Network

### `TrustedAuthorityCertificateName`

**Type:** `string | null` | **Default:** `null` (uses the host's `TrustedAuthority`, then `COMLINK-ROOT`)

Subject name of the certificate authority every user's identity certificate must chain to, looked up in the system certificate store. Ignored when `AuthorityCertificate` is set.

### `AuthorityCertificate`

**Type:** `string | null` | **Default:** `null`

Path to a public certificate file (for example `.cer`) for that authority, used instead of a store lookup. A relative path resolves against the directory of the configuration file. It must be used together with `CertificateStore`; setting only one of the two throws when connections are set up.

### `CertificateStore`

**Type:** `string | null` | **Default:** `null`

Path to a folder of PKCS#12 (`.pfx`) files, one per user named `{USERNAME}.pfx` (for example `PEER1.pfx`, matching the user name's case on a case-sensitive file system), holding each user's identity certificate and private key. A node loads the running user's own identity from it instead of a system store lookup. A relative path resolves against the directory of the configuration file. It must be used together with `AuthorityCertificate`.

### `UserGroups`

**Type:** `object` | **Default:** `{}`

Group name to member names. Members may be user names or other group names, enabling nested groups; addressing a group sends to every member (see [Peer.md](Peer.md#user-roles)). A user's info lists the groups it is a member of, and group names appear in the address directory alongside user names.

```json
"UserGroups": {
  "INNER": [ "USER-A" ],
  "OUTER": [ "INNER", "USER-B" ]
}
```

### `Users`

**Type:** `object` | **Default:** `{}`

Every user of the network, keyed by user name. The fields of an entry are in two groups: what is known about the user, which becomes their `UserInfo`, and the settings of the node that user runs, which apply only while that user is the current one.

## What is known about a user

These become the user's `UserInfo` (see [Configuration.md](Configuration.md#user-info)) for every node on the network.

### `Role`

**Type:** `string | null` | **Default:** `null` (`"Peer"`)

The networking role of a node this user runs: `"Peer"`, `"Client"` or `"Server"` (case-insensitive). An unrecognized value is `"Peer"`. See [Peer.md](Peer.md#user-roles).

### `PeerPort`

**Type:** `int | null` | **Default:** `null` (`50021`)

TCP port on which the node listens for IP connections opened by other nodes: peers dialing this peer, and clients and other servers connecting to a server. A client opens its connection outward and does not listen.

### `InterfacePort`

**Type:** `int | null` | **Default:** `null` (`50020`)

Loopback TCP port of the local interface listener, always active in every role (see [Interface.md](Interface.md)).

### `OutgoingPoints`

**Type:** `object[]` | **Default:** `[]`

The points the node connects out to and keeps connected. A `"Client"` uses the first as its server. Nothing here says which user is at a point; that is worked out when the connection forms (see [Identification.md](Identification.md)).

| Field | Type | Description |
|-------|------|-------------|
| `IpAddress` | `string` | IPv4 or IPv6 address of the remote node |
| `Port` | `int` | TCP port the remote node listens on |
| `SerialPort` | `string` | Name of the local MicroGate serial port cabled to the remote node. When set, `IpAddress` and `Port` are ignored and the point is reached over serial. List the port on both nodes that share the cable |
| `SerialAddress` | `int` | This node's HDLC station address on the serial link (0-255, default 255). Must differ from `RemoteSerialAddress` |
| `RemoteSerialAddress` | `int` | HDLC station address of the node at the other end of the cable (0-255, default 254). The other end lists the two addresses the other way round |
| `User` | `string` | For a serial point, the user at the other end of the cable, which the connection is identified as |

A serial link carries no certificate, so unless the point names its `User`, its user is named after the port; override `IdentifyConnection` or configure a connection message for anything more elaborate.

### `ChildClients`

**Type:** `string[]` | **Default:** `[]`

For a `"Server"`, the client users that belong to it. The topology a server routes with, every server of the cluster and the children each owns, is built from every `"Server"` user's entry.

### `StoresMessages`

**Type:** `bool` | **Default:** `false`

For a `"Server"`, whether it keeps a copy of every message it routes and answers retrieval requests (see [Configuration.md](Configuration.md#server-storage)).

### `SecurityLevel`

**Type:** `string | null` | **Default:** `null` (the lowest configured level)

The name of the security level the user runs at (see [Configuration.md](Configuration.md#security-levels)).

### `CertificateName`

**Type:** `string | null` | **Default:** `null` (the user name)

The user's certificate subject name: the identity certificate to look up for the local user, and the name a connecting user's certificate must carry for others to accept it as this user.

### `Data`

**Type:** `object` | **Default:** `{}`

App-specific string keys and values attached to the user. The engine does not interpret them; they travel with the user's `UserIdentity` wherever the user is identified.

## Settings of the node a user runs

These apply to a node running as this user; the one marked "launched" only when the user is named with `--user`, since an installed user is not known until networking starts.

### `Headless` (launched)

**Type:** `bool` | **Default:** `false`

Run with no GUI, as a normal peer.

### `AlertText`

**Type:** `string | null` | **Default:** `null` (`"ALERT"`)

Text shown in the title bar's alert box while alarming, and the draft editor's alert checkbox label.

### `AlarmSoundSeconds`

**Type:** `number | null` | **Default:** `null` (`30`)

Seconds the alarm sound plays after an alert is received before automatically stopping; resets whenever a new alert arrives.

### `QuickConfirmationEnabled`

**Type:** `bool | null` | **Default:** `null` (`true`)

Whether clicking the alert box, or pressing Space or Enter outside a text input, confirms the latest unconfirmed alert.

### `ComposeAlertsEnabled`

**Type:** `bool | null` | **Default:** `null` (`true`)

Whether the draft editor's alert checkbox is shown. Disabling it never prevents receiving and alarming on alerts.

### `MessageTagsEnabled`

**Type:** `bool | null` | **Default:** `null` (`true`)

Whether message tags are shown anywhere in the UI.

### `MessageTagLabel`

**Type:** `string | null` | **Default:** `null` (`"Tag"`)

Label of the tag input's watermark in the draft editor.

### `PrintReceivedEnabled`

**Type:** `bool | null` | **Default:** `null` (`false`)

Whether the print manager's "print received" toggle starts enabled, automatically adding every received message to the print queue.

## Examples

### A peer network

Two peers that dial each other, sharing one machine (`Scripts/Scenarios/Peer/Config.json`):

```json
{
  "AuthorityCertificate": "../Root.cer",
  "CertificateStore": ".",
  "UserGroups": { "TEST": [ "PEER1", "PEER2" ] },
  "Users": {
    "PEER1": { "PeerPort": 50021, "InterfacePort": 50020, "OutgoingPoints": [ { "IpAddress": "127.0.0.1", "Port": 50023 } ], "SecurityLevel": "PUBLIC" },
    "PEER2": { "PeerPort": 50023, "InterfacePort": 50022, "OutgoingPoints": [ { "IpAddress": "127.0.0.1", "Port": 50021 } ], "SecurityLevel": "PUBLIC" }
  }
}
```

```sh
Sample.exe --config Scripts/Scenarios/Peer/Config.json --user PEER1
Sample.exe --config Scripts/Scenarios/Peer/Config.json --user PEER2
```

### A client/server hierarchy

One server with two clients that connect to it, the server storing messages (`Scripts/Scenarios/ClientServer/Config.json`):

```json
{
  "AuthorityCertificate": "../Root.cer",
  "CertificateStore": ".",
  "Users": {
    "SERVER":  { "Role": "Server", "PeerPort": 50121, "InterfacePort": 50120, "ChildClients": [ "CLIENT1", "CLIENT2" ], "StoresMessages": true, "SecurityLevel": "RESTRICTED" },
    "CLIENT1": { "Role": "Client", "InterfacePort": 50122, "OutgoingPoints": [ { "IpAddress": "127.0.0.1", "Port": 50121 } ], "SecurityLevel": "INTERNAL" },
    "CLIENT2": { "Role": "Client", "InterfacePort": 50124, "OutgoingPoints": [ { "IpAddress": "127.0.0.1", "Port": 50121 } ], "SecurityLevel": "INTERNAL" }
  }
}
```

### Certificates from the system store

Without `CertificateStore` and `AuthorityCertificate`, certificates are looked up in the system store by each user's `CertificateName` (the user name by default) and the trusted authority's name:

```json
{
  "TrustedAuthorityCertificateName": "MY-ROOT",
  "Users": { "USER-A": { "CertificateName": "COMLINK-USER-A" } }
}
```

### Headless

```json
{ "Users": { "GATEWAY": { "Headless": true, "InterfacePort": 50030 } } }
```

```sh
Sample.exe --config Config.json --user GATEWAY
```
