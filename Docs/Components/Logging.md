# Logging

The engine configures two logging providers, both registered in `EngineExtensions.UseEngine`. All other providers (console, debug, etc.) are cleared.

## DailyFileLoggerProvider

Writes all log output to a daily rotating file and to stdout.

**File location**: `{AppDataPath}/Logs/yyyy-MM-dd.log` (the current user's folder, `%APPDATA%/{AppName}/{USERNAME}`; lines logged before a user is installed or named go to `%APPDATA%/{AppName}/Logs`, and the file follows the user once there is one)

**Log format**:
```
[dd-MMM-yyyy HH:mm:ss.fff] [CATEGORY] [USER] [ID] message
```

Example, with no field fixed (the default):
```
[30-JUL-2026 14:23:07.051] [ACTIVITY] [MYUSER] [66] Engine started
[30-JUL-2026 14:23:07.102] [ERROR] [MYUSER] [23] Failed to store received message from ALICE: System.IO.IOException: ...
[30-JUL-2026 14:23:07.140] [APP] [] [] No event ID, and no user yet
```

and with a log handler fixing the category to 8, the user to 8 and the ID to 2:
```
[30-JUL-2026 14:23:07.051] [ACTIVITY] [MYUSER--] [66] Engine started
[30-JUL-2026 14:23:07.102] [ERROR---] [MYUSER--] [23] Failed to store received message from ALICE: System.IO.IOException: ...
[30-JUL-2026 14:23:07.140] [APP-----] [--------] [--] No event ID, and no user yet
```

`LogLineFormatter` builds the line. **By default no field has a fixed size**: each is written at its natural length, so lines do not align. A host that wants lines aligned field by field states an `ILogHandler` (`engine.Logs<MyLogHandler>()`) whose `CategoryWidth`, `UserWidth` and `IdWidth` each give the fixed width of one field, in characters, or `null` (the default) to leave that field unfixed. The fields and their rules:

- **Category**: the only classification of an entry; there are no log levels. The category in capitals: one of the six the engine writes under (`ACTIVITY`, `FRAMES`, `PACKETS`, `APP`, `ERROR`, `CRASH`, described under Usage), or a host's own. The category of an engine event is fixed by the event (see the tables below), whichever logger writes it.
- **User**: the current value of `CurrentUserProvider.UserName`, empty before a user is installed or named.
- **ID**: the identifier of the event being logged, one of the `LogEvents` entries (every event has its own and the tables below list them), empty for a line written without one.

A fixed text field (category, user) is **padded on the right with hyphens** up to its width, so an empty one is a run of hyphens, and is **cut** to the width when longer. A fixed ID is padded the same way, with hyphens after the number and not zeros before it, but is **never cut**, since cutting it would name another event; an ID wider than its field simply overflows. An unfixed field is never padded. The timestamp is always the same width. An exception's text follows on the lines after the entry. The activity log shows an event ID in an ID column before the content, padded to the fixed ID width the same way.

**Multi-process safety**: The provider uses a named Windows Mutex (`Local\PCLog_{md5(logdir)}`) so that multiple processes writing to the same log directory serialize their writes. File is opened with `FileShare.ReadWrite` so all processes can hold handles simultaneously. Within a single process, a `lock` serializes all loggers (one per category) through a single shared `StreamWriter`.

**Day rollover**: The `StreamWriter` is reopened on the first write of a new day.

**Never throws**: every line is also written to the console, and a failure to write the file (for example an unwritable data folder) is swallowed after a single console notice, so logging can never take the application down. Month names use the invariant culture, so the format is the same on every machine.

## ActivityLoggerProvider

Writes structured activity events to the LiteDB database. Active in `Client` mode only.

**Filter**: only handles the events of the `ACTIVITY` category (an entry with no engine event, from a logger of that category). All other log calls are no-ops.

**Storage**: appends one `ActivityLogEntry { At, Message, EventId }` to today's `ActivityLogEntity` via `ActivityLogRepository.AppendEvent`, with the identifier of the logged event as `EventId`, which the activity view shows in its ID column, beside a column for the content, and the export carries. Creates the day's record if it doesn't exist.

**Before the database is open**: the database lives in the current user's data folder, so it is not opened until a user is installed or named, and the app starts logging before that (`starting`, `started`). `ActivityLogRepository.AppendEvent` therefore holds an event in memory, with the time it was logged, while `ILiteDbContext.IsOpen` is false, and writes the waiting events to the day they belong to when the database raises `Opened`, so a first run's activity log starts with its start events.

## Usage

Log categories follow the existing convention:

| Category | Usage |
|----------|-------|
| `ACTIVITY` | What a user should read, written to the in-app log and the log file, in general terms without technical detail: the app starting, started and exited, failed installs, and a general notice of a problem whose detail is under `ERROR` or `CRASH` |
| `FRAMES`, `PACKETS` | The bytes of every frame and every packet, off by default |
| `APP` | Technical events of the running application that a user has no use for: connections, services, the network layers and conditions that are handled |
| `ERROR` | Something failed, with the technical detail and any exception |
| `CRASH` | A failure the application cannot go on after, or that stops a part of it |

There are no log levels: the category is the whole classification. The category of an engine event belongs to the event (`LogEvents.CategoryOf`), so the same event always lands in the same category and the providers route by it: the activity provider keeps the `ACTIVITY` events, the file provider writes every enabled category.

Any other category string is valid and will appear in the file log.

### Enabling categories

`ACTIVITY`, `APP`, `ERROR` and `CRASH` are always written; `FRAMES` and `PACKETS` are off by default, since they write every frame and packet. `ILogSettings` (`LogSettings`) turns them on from two places, which add up:

- `Logging.json` in the app data folder, beside `User.json` (`IEngineController.LoggingFilePath`): `["Frames", "Packets"]`, a JSON list of the categories to enable, names in any case, unknown names ignored. The file need not exist. A file that is not valid JSON is ignored at startup (nothing can be logged yet) and fails a refresh like an unreadable network file.
- The `--log` argument, a comma separated list such as `--log Frames,Packets`, when the host allows command-line overrides.

The settings are read at startup and again by "Refresh configuration" in the title bar's info panel, so a trace can be turned on and off without restarting. The file provider asks before writing every entry, and the trace transports before building each entry, so a change also applies to connections that are already up.

```csharp
// Inject ILoggerFactory, then:
var logger = loggerFactory.CreateLogger("APP");
logger.Record(LogEvents.AppStarted, "{AppName} started", name);

```

## Log events

Every event the engine writes to the log, one table per log category. `{Name}` is a placeholder filled in when the event is written. Each event has an ID that is unique across all tables and never reused (a new event takes the next free number, so the IDs in a table are not always ascending); the log output carries it in its ID field, and the activity log lists it before each entry. The IDs are the `LogEvents` properties in the code, each defined with its category, and every log call passes one through `ILogger.Record`. Any change to a logged message or a new log call updates this section in the same change.

### ACTIVITY

What a user reads in the in-app activity log: general, in plain words, with no exceptions, services or other technical detail. When something technical goes wrong in a way the user should know about, the technical event is written under `ERROR` or `CRASH` and a general one here says that there is a problem.

| ID | Content | Scenario |
|----|---------|----------|
| 31 | `{Item} was not imported: its priority is not supported` | An import finds a message or draft with a priority that is not configured. |
| 55 | `Install of {UserName} failed: {Reason}` | An install on the install screen is refused: the name is not a user of the network, or the user's certificate is missing, not issued to them or not signed by the authority. |
| 65 | `{AppName} starting` | The host starts. |
| 66 | `{AppName} started` | Startup is done, once the networking services are launched or deferred until a user is installed, or once a user is installed on the install screen and the main window opens. |
| 67 | `{AppName} exited` | The host begins shutting down, so the application is exiting. |
| 68 | `{Change} {UserName}` | A client or server connection to its parent, a child or a sibling server comes up (`Connected to`) or goes down (`Disconnected from`). |
| 70 | `Network {Status}` | The network indicator goes `online` or `offline`. |
| 72 | `Network configuration reloaded` | The user chooses Refresh and the network file has been read again. |
| 76 | `{MessageId} received from {FromUser}` | The network processor records a received message. |
| 80 | `{MessageId} sending to {Destinations}` | A message sent with the GUI is stored and handed to the network processor. |
| 83 | `{MessageId} status for {User}: {Status}` | The network processor changes the delivery status of a sent message to a recipient. |
| 85 | `External system {Name} {Change}` | An external system's connection comes up (`connected`) or goes down (`disconnected`). |
| 90 | `{UserName} is not installed: {Problem}` | At startup the remembered user or the `--user` name is not in the network file, or its certificate fails the check. |
| 93 | `The application ran into an unexpected error` | An exception reaches the top-level handler of the application (technical detail: 1). |
| 94 | `Networking is not working: {Reason}` | Networking cannot start or no longer works, because the network configuration is not valid or the role's certificates or settings cannot be used (technical detail: 2, 3). |
| 95 | `The interface for other applications is not working` | The local interface listener cannot start (technical detail: 8). |
| 97 | `Could not send {Preview}` | Sending a draft or a staged message throws (technical detail: 26). |
| 98 | `A new draft could not be saved` | The automatic first store of a new draft throws (technical detail: 28). |
| 99 | `What you were writing could not be saved` | Saving a draft or note when leaving it throws (technical detail: 30). |
| 100 | `A print job could not be completed` | The content of a print job cannot be read, or the printer fails (technical detail: 33, 34). |
| 101 | `A message received from {FromUser} could not be saved` | Storing a message received while the UI runs throws (technical detail: 23). |
| 102 | `The application could not finish loading` | The initial setup of the main window's view model throws (technical detail: 88). |
| 103 | `The network configuration could not be reloaded` | A reload requested by the user fails (technical detail: 39). |
| 104 | `The saved settings for the last user could not be read` | Reading `User.json` throws (technical detail: 89). |
| 105 | `Part of the application stopped working unexpectedly` | A background service ends other than by being cancelled (technical detail: 87). |
| 107 | `External system {Name} stopped working` | An external system's run loop throws (technical detail: 40). |
| 108 | `External system {Name} {Problem}` | An external system cannot connect (`could not connect`) or cannot send a message (`could not send a message`) (technical detail: 57). |

### FRAMES

Written only while the `FRAMES` category is turned on (see Enabling categories). One entry per frame, as the serialized bytes in hexadecimal with the user on the other end (`unidentified` before the connection is identified). A frame is traced between the packetizer and the connection's initial exchange, so a frame the engine sends for the initial exchange is traced too, and when packetization is on a frame is traced whole rather than as the packets it travels in.

| ID | Content | Scenario |
|----|---------|----------|
| 111 | `Sent {Length} bytes to {User}: {Bytes}` | A frame is sent. |
| 112 | `Received {Length} bytes from {User}: {Bytes}` | A frame is received, whole. |

### PACKETS

Written only while the `PACKETS` category is turned on (see Enabling categories) and packetization is on. The same as the frame trace, for each packet, beneath everything else, so it includes the initial packet exchange and exactly the bytes put on the connection.

| ID | Content | Scenario |
|----|---------|----------|
| 113 | `Sent {Length} bytes to {User}: {Bytes}` | A packet is sent. |
| 114 | `Received {Length} bytes from {User}: {Bytes}` | A packet is received. |

### APP

Technical events of the running application: connections, the wire and the network layers, and conditions the engine handles itself.

| ID | Content | Scenario |
|----|---------|----------|
| 41 | `Rejected connection from {Name}, which is neither {Expected}` | A server (a child client or another server in the cluster) gets a connection from a user it does not know. |
| 43 | `Dropped a connection that {Reason}` | An IP connection cannot be identified or fails its initial exchange. |
| 44 | `Dropped a packet that could not be assembled: {Message}` | A received packet fails reassembly. |
| 45 | `IP connections are unavailable: {Message}` | The IP transport cannot be built, for example because certificates are missing. |
| 46 | `The packet size of {PacketSize} bytes is larger than the HDLC MaxInfoField of {MaxInfoField} bytes, so packets will fail to send over serial connections` | Packetization is on and its packet size exceeds the HDLC frame limit. |
| 47 | `Serial link to {Point} {Problem}` | A serial link drops other than by being closed (`lost`), fails to open (`cannot be established, retrying: ...`, once per outage) or its HDLC layer reports an error (`met an error: ...`). |
| 115 | `Cannot reach {Point}, retrying: {Reason}` | The connection to an outgoing point cannot be opened, or its heartbeat throws, logged once per outage with the reason (a failed TLS handshake, a refused connection, a timeout). |
| 54 | `Cannot deliver to {User}: no connection is identified as them` | A server has no live connection to a recipient. |
| 57 | `External system {Name} failed to {Action}` | An operation of an external system throws: `connect`, `poll connection status`, `release its connection cleanly`, `send a message`, `filter a received message` or `process a received message`. |
| 60 | `External system {Name} cannot send a {Type}; it only handles {Expected}` | A frame of the wrong type is sent to an external system. |
| 62 | `External system {Name} received a message while not running; dropping it` | A message arrives from an external system after it stopped. |
| 73 | `Role or certificates changed, restarting connections` | A reload finds a changed role, certificate store or authority certificate. |
| 74 | `Interface listener changed, restarting it` | A reload finds a changed interface port or certificate setting. |
| 75 | `Serial link to {Point} connected` | A serial link comes up. |
| 84 | `Retrieval found {Count} stored message(s)` | The network processor looks up stored messages. |
| 109 | `{Change} {UserName}` | A server connection to a parent, a child or a sibling server comes up (`Connected to`) or goes down (`Disconnected from`). |

### ERROR

Something failed. The technical detail of a problem that also has an `ACTIVITY` entry is here, and the scenario of the entry says which.

| ID | Content | Scenario |
|----|---------|----------|
| 2 | `Networking could not start: {Message}` | The role's peer service cannot be built, for example because a certificate file is missing. |
| 3 | `Invalid configuration file: {Problem}` | The network configuration file is invalid for the role: a client has no parent (at start or after a reload), or a server is missing from its own server map. |
| 8 | `Interface listener cannot start: {Message}` | The local interface listener cannot be built, for example because a certificate file is missing. |
| 11 | `A payload of {Length} bytes cannot be sent over {Point}: {Reason}` | A send is larger than the connection can carry: MSMT's largest message with packetization off, what the packetizer can split, or one HDLC frame (the reason says which and how to fix it). |
| 22 | `Failed to handle {UserName} {Action}` | A handler of a user connecting or disconnecting throws. |
| 23 | `Failed to store received message from {FromUser}` | Storing a message received while the UI runs throws. |
| 24 | `Failed to store a copy of {MessageId}` | The network processor stores a message and storing it fails. |
| 25 | `Failed to read stored messages for a retrieval by {Requester}` | A storage server fails to read messages for a retrieval request. |
| 26 | `{Kind} transmission failed for {Preview}` | Sending a draft (`Message`) or a staged message (`Staged send`) throws. |
| 28 | `Failed to store a new draft` | The automatic first store of a new draft throws. |
| 29 | `Failed to show where the opened entry is kept` | Selecting the folder and entry of what was opened throws. |
| 30 | `Failed to save what was written before leaving it` | Saving a draft or note when leaving it throws. |
| 33 | `Failed to load print content for {EntryId}` | Reading the content of a print job throws. |
| 34 | `Printing {EntryId} on {Printer} failed` | Sending a print job to the printer throws. |
| 37 | `The network processor's {Name} failed for {Subject}` | A method of the network processor throws. |
| 39 | `The network configuration could not be reloaded: {Message}` | A reload requested by the user fails because the file cannot be read or parsed. |
| 40 | `External system {Name} stopped unexpectedly` | An external system's run loop throws. |
| 88 | `Initialization failed` | The initial setup of the main window's view model throws. |
| 89 | `Failed to load user state` | Reading `User.json` throws. |

### CRASH

A failure the application cannot go on after, or that stops a part of it.

| ID | Content | Scenario |
|----|---------|----------|
| 1 | `Unhandled exception: {Message}` | An exception reaches the top-level handler of the application. |
| 87 | `{Service} stopped unexpectedly` | A background service (peer, interface, external systems, network processor, auto forward, disconnect alarm or network indicator) ends other than by being cancelled. |
