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
      "PeerPoint": { "Host": "10.0.0.2", "Port": 50021 },
      "InterfacePort": 50020,
      "Parent": null,
      "Children": [],
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

The networking role of a node this user runs: `"Peer"`, `"Client"`, `"Server"` or `"Relay"` (case-insensitive). An unrecognized value is `"Peer"`. See [Peer.md](Peer.md#user-roles).

### `PeerPoint`

**Type:** `object | null` | **Default:** `null` (host `127.0.0.1`, port `50021`)

How other nodes reach this node over IP, and the port it listens on. `Host` is the host name or IP address others dial, and `Port` the TCP port the node listens on for IP connections opened by other nodes: peers dialing this peer, and clients, relays and other servers connecting to a server or relay. A user that nobody dials need not state one. Where one user dials another (a peer dialing a peer, a node dialing its parent, a parent dialing a child whose link is forced to `MsmtConnect`), it uses the other user's `PeerPoint`.

| Field | Type | Description |
|-------|------|-------------|
| `Host` | `string` | Host name or IP address other nodes use to reach this node (default `127.0.0.1`) |
| `Port` | `int` | TCP port this node listens on and others connect to (default `50021`) |

### `InterfacePort`

**Type:** `int | null` | **Default:** `null` (`50020`)

Loopback TCP port of the local interface listener, always active in every role (see [Interface.md](Interface.md)).

### `Parent` and `Children`

**Type:** `string | object | null` and `(string | object)[]` | **Default:** none

The links between this user and the users it is connected to in a hierarchy. A `"Client"` or `"Relay"` names its `Parent`, the server or relay above it; a `"Server"` or `"Relay"` lists its `Children`, the clients and relays below it. A server may also name another server as its `Parent` to join a cluster. By default a user opens an outgoing connection to its parent, at the parent's `PeerPoint`, and listens on its own `PeerPoint` for incoming connections from its children, so naming the other user is enough. A user listed in `Children` is also the only kind of user a server or relay accepts connections from (apart from other servers), and a server routes by them (see [Peer.md](Peer.md#user-roles)). A relay's own `Children` are the clients behind it, which the server learns from the relay's entry.

Instead of a plain user name, a link may be an object that forces how the connection forms. The other end of the link states the matching mode (a child forced to `MsmtConnect` is dialed by its parent, so that child states its `Parent` with `MsmtListen`; both ends of a serial cable state `SyncSerial`).

| Field | Type | Description |
|-------|------|-------------|
| `User` | `string` | The user at the other end of the link |
| `Mode` | `string` | `MsmtListen` (listen for an incoming MSMT connection from that user), `MsmtConnect` (open an outgoing MSMT connection to that user's `PeerPoint`) or `SyncSerial` (form a MicroGate peer connection over a serial port). Default: `MsmtConnect` for a parent, `MsmtListen` for a child, and `SyncSerial` when `SerialPort` is given |
| `SerialPort` | `string` | For `SyncSerial`, the name of the local MicroGate serial port cabled to that user |
| `SerialAddress` | `int` | For `SyncSerial`, this node's HDLC station address on the serial link (0-255, default 255). Must differ from `RemoteSerialAddress` |
| `RemoteSerialAddress` | `int` | For `SyncSerial`, the HDLC station address of the user at the other end of the cable (0-255, default 254). The other end states the two addresses the other way round |

```json
"CLIENT1": { "Role": "Client", "Parent": { "User": "SERVER", "Mode": "SyncSerial", "SerialPort": "ttyUSB0", "SerialAddress": 2, "RemoteSerialAddress": 1 } }
```

A serial link carries no certificate, so the connection is identified as the user the link names; override `Identify` or configure an initial packet or message for anything more elaborate. Peers have no parent or children: a `"Peer"` dials every other peer that states a `PeerPoint`, except that when both state one only the one whose name sorts first dials, so a pair is never connected both ways, and a peer with none dials all of them.

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

Two peers sharing one machine, the one whose name sorts first dialing the other (`Scripts/Scenarios/Peer/Config.json`):

```json
{
  "AuthorityCertificate": "../Root.cer",
  "CertificateStore": ".",
  "UserGroups": { "TEST": [ "PEER1", "PEER2" ] },
  "Users": {
    "PEER1": { "PeerPoint": { "Host": "127.0.0.1", "Port": 50021 }, "InterfacePort": 50020, "SecurityLevel": "PUBLIC" },
    "PEER2": { "PeerPoint": { "Host": "127.0.0.1", "Port": 50023 }, "InterfacePort": 50022, "SecurityLevel": "PUBLIC" }
  }
}
```

```sh
Sample.exe --config Scripts/Scenarios/Peer/Config.json --user PEER1
Sample.exe --config Scripts/Scenarios/Peer/Config.json --user PEER2
```

### A client/server hierarchy

One server with two clients that each name it as their parent, the server storing messages (`Scripts/Scenarios/ClientServer/Config.json`):

```json
{
  "AuthorityCertificate": "../Root.cer",
  "CertificateStore": ".",
  "Users": {
    "SERVER":  { "Role": "Server", "PeerPoint": { "Host": "127.0.0.1", "Port": 50121 }, "InterfacePort": 50120, "Children": [ "CLIENT1", "CLIENT2" ], "StoresMessages": true, "SecurityLevel": "RESTRICTED" },
    "CLIENT1": { "Role": "Client", "InterfacePort": 50122, "Parent": "SERVER", "SecurityLevel": "INTERNAL" },
    "CLIENT2": { "Role": "Client", "InterfacePort": 50124, "Parent": "SERVER", "SecurityLevel": "INTERNAL" }
  }
}
```

A relay sits between clients and a server: the server lists it as a child, the relay names the server as its parent and lists the clients behind it as its children, and those clients name the relay as their parent (`Scripts/Scenarios/ClientRelayServer/Config.json`).

```json
{
  "Users": {
    "SERVER":  { "Role": "Server", "PeerPoint": { "Host": "127.0.0.1", "Port": 50121 }, "Children": [ "CLIENT1", "RELAY" ] },
    "RELAY":   { "Role": "Relay", "PeerPoint": { "Host": "127.0.0.1", "Port": 50123 }, "Parent": "SERVER", "Children": [ "CLIENT2" ] },
    "CLIENT1": { "Role": "Client", "Parent": "SERVER" },
    "CLIENT2": { "Role": "Client", "Parent": "RELAY" }
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
