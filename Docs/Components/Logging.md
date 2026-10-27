# Logging

The engine configures two logging providers, both registered in `EngineExtensions.UseEngine`. All other providers (console, debug, etc.) are cleared.

## DailyFileLoggerProvider

Writes all log output to a daily rotating file and to stdout.

**File location**: `{AppDataPath}/Logs/yyyy-MM-dd.log` (the app's one data folder, `%APPDATA%/{AppName}` or the folder `--data` names, whoever the user is)

**Log format**:
```
[dd-MMM-yyyy HH:mm:ss.fff] [CATEGORY] [USER] [ID] message
```

Example, with no field fixed (the default):
```
[30-JUL-2026 14:23:07.051] [ACTIVITY] [MYUSER] [CL66] Engine started
[30-JUL-2026 14:23:07.102] [ERROR] [MYUSER] [CL23] Failed to store received message from ALICE: System.IO.IOException: ...
[30-JUL-2026 14:23:07.140] [APP] [] [MYAPP7] A host's own ID, and no user yet
```

and with a log handler fixing the category to 8, the user to 8 and the ID to 5:
```
[30-JUL-2026 14:23:07.051] [ACTIVITY] [MYUSER--] [CL66-] Engine started
[30-JUL-2026 14:23:07.102] [ERROR---] [MYUSER--] [CL23-] Failed to store received message from ALICE: System.IO.IOException: ...
[30-JUL-2026 14:23:07.140] [APP-----] [--------] [MYAPP7] A host's own ID, wider than the field and so not cut, and no user yet
```

`LogLineFormatter` builds the line. **By default no field has a fixed size**: each is written at its natural length, so lines do not align. A host that wants lines aligned field by field states an `ILogHandler` (`engine.Logs<MyLogHandler>()`) whose `CategoryWidth`, `UserWidth` and `IdWidth` each give the fixed width of one field, in characters, or `null` (the default) to leave that field unfixed. The fields and their rules:

- **Category**: the only classification of an entry; there are no log levels. The category in capitals: one of those the engine writes under (`ACTIVITY`, `FRAMES`, `PACKETS`, `SERIAL`, `IP`, `AVALONIA`, `APP`, `ERROR`, `CRASH`, and `DEBUG` which only a host writes under, described under Usage and public as `LogCategories`), or a host's own. The category of an engine event is fixed by the event (see the tables below), whichever logger writes it.
- **User**: the current value of `CurrentUserProvider.UserName`, the user the node's certificate names, empty while the node cannot be identified.
- **ID**: the identifier of the event being logged, text. The engine's are `CL` and a number (`CL56`), the `LogEvents` entries, which every event has its own of and the tables below list; a host's are whatever it gave `ILogger`. It is empty only for a line that did not come from the engine or `ILogger`, such as one a framework library writes.

A fixed text field (category, user) is **padded on the right with hyphens** up to its width, so an empty one is a run of hyphens, and is **cut** to the width when longer. A fixed ID is padded the same way, with hyphens after it, but is **never cut**, since cutting it would name another event; an ID wider than its field simply overflows. An unfixed field is never padded. The timestamp is always the same width. What an entry reports about an exception follows on the lines after it: the type and message of the exception and of each exception inside it, joined with `--->`, or, unless the host stated `StackTrace(false)`, the whole exception with its stack trace. The activity log shows an event ID in an ID column before the content, padded to the fixed ID width the same way.

**Multi-process safety**: The provider uses a named Windows Mutex (`Local\PCLog_{md5(logdir)}`) so that multiple processes writing to the same log directory serialize their writes. File is opened with `FileShare.ReadWrite` so all processes can hold handles simultaneously. Within a single process, a `lock` serializes all loggers (one per category) through a single shared `StreamWriter`.

**Day rollover**: The `StreamWriter` is reopened on the first write of a new day.

**Never throws**: every line is also written to the console, and a failure to write the file (for example an unwritable data folder) is swallowed after a single console notice, so logging can never take the application down. Month names use the invariant culture, so the format is the same on every machine.

## ActivityLoggerProvider

Writes structured activity events to the LiteDB database. Active in `Client` mode only.

**Filter**: only handles the events of the `ACTIVITY` category (an entry with no engine event, from a logger of that category). All other log calls are no-ops.

**Storage**: appends one `ActivityLogEntry { At, Message, EventId }` to today's `ActivityLogEntity` via `ActivityLogRepository.AppendEvent`, with the identifier of the logged event (text) as `EventId`, which the activity view shows in its ID column, beside a column for the content, and the export carries. Creates the day's record if it doesn't exist.

**Before the database is open**: the database is opened by the host once it starts, and the app starts logging before that (`starting`). `ActivityLogRepository.AppendEvent` therefore holds an event in memory, with the time it was logged, while `ILiteDbContext.IsOpen` is false, and writes the waiting events to the day they belong to when the database raises `Opened`, so the activity log starts with its start events.

## Usage

Log categories follow the existing convention:

| Category | Usage |
|----------|-------|
| `ACTIVITY` | What a user should read, written to the in-app log and the log file, in general terms without technical detail: the app starting, started and exited, a refresh that switches the node to another user, a certificate problem that leaves the node unidentified, and a general notice of a problem whose detail is under `ERROR` or `CRASH` |
| `FRAMES`, `PACKETS` | The bytes of every frame and every packet, off by default |
| `SERIAL` | The activity of the serial (MicroGate) links with their serial details: the port, the local and remote station addresses and the HDLC state, a port failing to open or connect, a link closed or opened by the application, modem signals and errors the port reports; off by default |
| `IP` | The activity of the IP (MSMT) connections with their IP details: the listener starting and stopping, outgoing connection attempts and failures (reported once per point until it has connected), connections made, accepted and closed, and incoming connections that never complete, each with host, port and certificate subject; off by default |
| `DEBUG` | For the host's own debugging output: the engine never writes under it. It is written exactly while `IDebugMode.IsDebug` is true and cannot be turned on otherwise, by `Logging.json`, `--log` or the log handler |
| `AVALONIA` | What Avalonia reports about itself, such as a binding that could not be resolved, off by default; Avalonia's own console output is switched off, so this is the only place it appears |
| `APP` | Technical events of the running application that a user has no use for: connections, services, the network layers and conditions that are handled |
| `ERROR` | Something failed, with the technical detail and any exception |
| `CRASH` | A failure the application cannot go on after, or that stops a part of it |

There are no log levels: the category is the whole classification. The category of an engine event belongs to the event (`LogEvents.CategoryOf`), so the same event always lands in the same category and the providers route by it: the activity provider keeps the `ACTIVITY` events, the file provider writes every enabled category.

Any other category string is valid, but is off like `FRAMES` until `Logging.json` or `--log` names it. `DEBUG` is the one exception: it follows debug mode alone.

### Enabling categories

Only `ACTIVITY`, `APP`, `ERROR` and `CRASH` are written by default, a whitelist in `LogCategories.OnByDefault` that the host's `ILogHandler.CategoriesOnByDefault` replaces as a whole when it states a list (names in any case; it may leave an engine category off or turn on any other); every other category is off (`FRAMES` and `PACKETS` write every frame and packet, `AVALONIA` many entries), including a host's own, until named. `DEBUG` is outside this: debug mode alone decides it, so it needs no naming and cannot be named on in a release build. `ILogSettings` (`LogSettings`) turns them on from two places, which add up:

- `Logging.json` in the app data folder, beside `Config.json` (`IEngineController.LoggingFilePath`): `["Frames", "Packets"]`, a JSON list of the categories to enable, names in any case, unknown names ignored. The file need not exist. A file that is not valid JSON is ignored at startup (nothing can be logged yet) and fails a refresh like an unreadable network file.
- The `--log` argument, a comma separated list such as `--log Frames,Packets`, when the host allows command-line overrides.

The settings are read at startup and again by "Refresh" in the title bar's info panel, so a trace can be turned on and off without restarting. The file provider asks before writing every entry, and the trace transports before building each entry, so a change also applies to connections that are already up.

```csharp
// Inject ILoggerFactory, then:
var logger = loggerFactory.CreateLogger("APP");
logger.Record(LogEvents.AppStarted, "{AppName} started", name);

```

### Logging from the host's components

A component of the host, a handler, a hosted service or anything built through dependency injection, logs through the public `ILogger` it asks for, without knowing how the engine logs. `Log(category, id, message)` and `Log(category, id, exception, message)` go through the same loggers and providers as the engine's own entries, so they are in the file, follow the same layout and, for an `ACTIVITY` entry, appear in the activity log the user reads. An entry written this way always has an identifier, a required `id` that names the kind of event (never empty, or the call throws `ArgumentException`), which its line carries and the activity log lists. The engine's are prefixed `CL`, so a host gives its own a prefix of its own (`MYAPP7`) and keeps them unique. The `DEBUG` category (`LogCategories.Debug`) is meant for exactly this: a host's debugging output, written while `IDebugMode.IsDebug` is true and dropped otherwise, with no switch to flip. Which entries are written follows the category as for any other: the engine's own are on, and a category of the host's own is off until `Logging.json` or `--log` names it, so a component either logs under one of `LogCategories` or documents the category to turn on. The log handed to what is built before the engine runs (the configuration and the display handler) writes nothing.

## Log events

Every event the engine writes to the log, one table per log category. `{Name}` is a placeholder filled in when the event is written. Each event has an ID that is unique across all tables and never reused (a new event takes the next free number, so the IDs in a table are not always ascending); an ID is `CL` and its number (`CL56`), the `EventId.Name` of the event, which is how it travels through the loggers to the log output's ID field and to the activity log, which lists it before each entry. The events are the `LogEvents` properties in the code, each defined with its number and category, and every log call passes one through `ILogger.Record`. Any change to a logged message or a new log call updates this section in the same change.

### ACTIVITY

What a user reads in the in-app activity log: general, in plain words, with no exceptions, services or other technical detail. When something technical goes wrong in a way the user should know about, the technical event is written under `ERROR` or `CRASH` and a general one here says that there is a problem.

| ID | Content | Scenario |
|----|---------|----------|
| CL31 | `{Item} was not imported: its priority is not supported` | An import finds a message or draft with a priority that is not configured. |
| CL65 | `{AppName} starting` | The host starts. |
| CL66 | `{AppName} started` | Startup is done, once the networking services are launched. |
| CL67 | `{AppName} exited` | The host begins shutting down, so the application is exiting. |
| CL117 | `Now running as {UserName}` | A refresh finds that the node's certificate now names another user, which becomes the current user. |
| CL118 | `Cannot identify the user: {Problem}` | The node cannot be identified, at startup or after a refresh: a certificate file is missing or unreadable, the user certificate is not signed by the authority certificate, or it is not issued to a user of the network. The UI shows the problem in place of the app. |
| CL120 | `The application's own work after a configuration change failed` | The host's configuration handler throws; the technical detail is under `ERROR`. |
| CL68 | `{Change} {UserName}` | A client or server connection to its parent, a child or a sibling server comes up (`Connected to`) or goes down (`Disconnected from`). |
| CL70 | `Network {Status}` | The network indicator goes `online` or `offline`. |
| CL72 | `Network configuration reloaded` | The user chooses Refresh and the network file has been read again. |
| CL76 | `{MessageId} received from {FromUser}` | The frame handler records a received message. |
| CL80 | `{MessageId} sending to {Destinations}` | A message sent with the GUI is stored and handed to the frame handler. |
| CL83 | `{MessageId} status for {User}: {Status}` | The frame handler changes the delivery status of a sent message to a recipient. |
| CL85 | `External system {Name} {Change}` | An external system's connection comes up (`connected`) or goes down (`disconnected`). |
| CL93 | `The application ran into an unexpected error` | An exception reaches the top-level handler of the application (technical detail: 1). |
| CL94 | `Networking is not working: {Reason}` | Networking cannot start or no longer works, because the network configuration is not valid or the role's certificates or settings cannot be used (technical detail: 2, 3). |
| CL95 | `The interface for other applications is not working` | The local interface listener cannot start (technical detail: 8). |
| CL97 | `Could not send {Preview}` | Sending a draft or a staged message throws (technical detail: 26). |
| CL98 | `A new draft could not be saved` | The automatic first store of a new draft throws (technical detail: 28). |
| CL99 | `What you were writing could not be saved` | Saving a draft or note when leaving it throws (technical detail: 30). |
| CL100 | `A print job could not be completed` | The content of a print job cannot be read, or the printer fails (technical detail: 33, 34). |
| CL101 | `A message received from {FromUser} could not be saved` | Storing a message received while the UI runs throws (technical detail: 23). |
| CL102 | `The application could not finish loading` | The initial setup of the main window's view model throws (technical detail: 88). |
| CL103 | `The network configuration could not be reloaded` | A reload requested by the user fails (technical detail: 39). |
| CL105 | `Part of the application stopped working unexpectedly` | A background service ends other than by being cancelled (technical detail: 87). |
| CL107 | `External system {Name} stopped working` | An external system's run loop throws (technical detail: 40). |
| CL108 | `External system {Name} {Problem}` | An external system cannot connect (`could not connect`) or cannot send a message (`could not send a message`) (technical detail: 57). |

### FRAMES

Written only while the `FRAMES` category is turned on (see Enabling categories). One entry per frame, as the serialized bytes in hexadecimal with the user on the other end (`unidentified` before the connection is identified). A frame is traced between the packetizer and the connection's handshake, so a frame the engine sends for the initial exchange is traced too, and when packetization is on a frame is traced whole rather than as the packets it travels in.

| ID | Content | Scenario |
|----|---------|----------|
| CL111 | `Sent {Length} bytes to {User}: {Bytes}` | A frame is sent. |
| CL112 | `Received {Length} bytes from {User}: {Bytes}` | A frame is received, whole. |

### PACKETS

Written only while the `PACKETS` category is turned on (see Enabling categories) and packetization is on. The same as the frame trace, for each packet, beneath everything else, so it includes the handshake and exactly the bytes put on the connection.

| ID | Content | Scenario |
|----|---------|----------|
| CL113 | `Sent {Length} bytes to {User}: {Bytes}` | A packet is sent. |
| CL114 | `Received {Length} bytes from {User}: {Bytes}` | A packet is received. |

### SERIAL

Written only while the `SERIAL` category is turned on (see Enabling categories). Every entry names the port and the local and remote HDLC station addresses (`SerialLink` knows the remote address it is trying, which rotates when several users may be at the other end of the cable). To keep a cable that is not connected from filling the log, the states of the retries after a failed attempt, and their failures, are written once and then not again until the link has connected; whatever happens to a link that is up is always written.

| ID | Content | Scenario |
|----|---------|----------|
| CL129 | `Serial port {Port} (local address {Local}, remote address {Remote}) is {State}` | The port's HDLC peer moves to `Ready` (the device is open), `Connecting`, `Connected` or `Disconnected`. |
| CL130 | `Serial port {Port} (local address {Local}, remote address {Remote}) cannot be opened or connected, retrying: {Reason}` | Opening the port or forming the link fails; written once until the link has connected. |
| CL131 | `Serial port {Port} (local address {Local}) {Action} by the application` | The link is closed (`closed`) or reopened (`opened again`) by a refresh or the connection monitor. |
| CL132 | `Serial port {Port} (local address {Local}, remote address {Remote}) signals: {Signals}` | The modem wires change, which the port reports only when signals are enabled in the HDLC options. |
| CL133 | `Serial port {Port} (local address {Local}, remote address {Remote}) reported an error: {Message}` | The port reports an error that does not break the link, such as one thrown by a receiver. |

### IP

Written only while the `IP` category is turned on (see Enabling categories), from the MSMT transport. Every entry names the host and port of the other end. Connection attempts and failures to an outgoing point are written once and not again until a connection to it has been made, since the connection monitor retries every few seconds.

| ID | Content | Scenario |
|----|---------|----------|
| CL121 | `Listening for connections on {Endpoint}` | The listener starts, with the address and port it is bound to. |
| CL122 | `Stopped listening for connections` | The listener stops, such as when the listen port changes. |
| CL123 | `Connecting to {Host}:{Port}` | An outgoing connection is being opened; written once until the point has connected. |
| CL124 | `Could not connect to {Host}:{Port}: {Reason}` | An outgoing connection ends before it is established (refused, timed out, a failed TLS handshake or a certificate that is not trusted); written once until the point has connected. |
| CL125 | `Connected to {Host}:{Port} as {Subject}` | An outgoing connection is established, with the certificate subject the other end presented. |
| CL126 | `Accepted a connection from {Host}:{Port} as {Subject}` | The listener accepts an incoming connection, with the certificate subject the other end presented. |
| CL127 | `Connection {Direction} {Host}:{Port} closed: {Reason}` | An established connection ends (`to` for one this node opened, `from` for one it accepted); the reason is the exception that closed it, or that it ended normally. |
| CL128 | `A connection from {Host}:{Port} never completed: {Reason}` | An incoming connection ends before it is established, such as a client that never completes its handshake. |

### AVALONIA

Written only while the `AVALONIA` category is turned on (see Enabling categories). Avalonia's own console output is replaced by `AvaloniaLogSink`, which records warnings and errors here instead, so a binding error never reaches the console.

| ID | Content | Scenario |
|----|---------|----------|
| CL116 | `Avalonia {Level} in {Area} ({Source}): {Avalonia's own message}` | Avalonia reports a warning or an error about itself, such as a binding that could not be resolved. |

### APP

Technical events of the running application: connections, the wire and the network layers, and conditions the engine handles itself.

| ID | Content | Scenario |
|----|---------|----------|
| CL41 | `Rejected connection from {Name}, which is neither {Expected}` | A server (a child client or another server in the cluster) gets a connection from a user it does not know. |
| CL43 | `Dropped a connection that {Reason}` | An IP connection cannot be identified, fails its handshake, or is rejected by a handshake handler (the reason it gave is in the message). |
| CL44 | `Dropped a packet that could not be assembled: {Message}` | A received packet fails reassembly. |
| CL45 | `IP connections are unavailable: {Message}` | The IP transport cannot be built, for example because certificates are missing. |
| CL46 | `The payload size of {MaxPayloadSize} bytes leaves no room for a packet's own fields within the HDLC MaxInfoField of {MaxInfoField} bytes, so packets will fail to send over serial connections` | Packetization is on and its maximum payload size is not below the HDLC frame limit. |
| CL54 | `Cannot deliver to {User}: no connection is identified as them` | A server has no live connection to a recipient. |
| CL57 | `External system {Name} failed to {Action}` | An operation of an external system throws: `connect`, `poll connection status`, `release its connection cleanly`, `send a message`, `filter a received message` or `process a received message`. |
| CL60 | `External system {Name} cannot send a {Type}; it only handles {Expected}` | A frame of the wrong type is sent to an external system. |
| CL62 | `External system {Name} received a message while not running; dropping it` | A message arrives from an external system after it stopped. |
| CL73 | `Role or certificates changed, restarting connections` | A reload finds a changed role, or a replaced user or authority certificate for the same user. |
| CL74 | `Interface listener changed, restarting it` | A reload finds a changed interface port or certificate setting. |
| CL84 | `Retrieval found {Count} stored message(s)` | The frame handler looks up stored messages. |
| CL109 | `{Change} {UserName}` | A server connection to a parent, a child or a sibling server comes up (`Connected to`) or goes down (`Disconnected from`). |

### ERROR

Something failed. The technical detail of a problem that also has an `ACTIVITY` entry is here, and the scenario of the entry says which.

| ID | Content | Scenario |
|----|---------|----------|
| CL2 | `Networking could not start: {Message}` | The role's peer service cannot be built, for example because a certificate file is missing. |
| CL3 | `Invalid configuration file: {Problem}` | The network configuration file is invalid for the role: a client has no parent (at start or after a reload), or a server is missing from its own server map. |
| CL8 | `Interface listener cannot start: {Message}` | The local interface listener cannot be built, for example because a certificate file is missing. |
| CL11 | `A payload of {Length} bytes cannot be sent over {Point}: {Reason}` | A send is larger than the connection can carry: MSMT's largest message with packetization off, what the packetizer can split, or one HDLC frame (the reason says which and how to fix it). |
| CL22 | `Failed to handle {UserName} {Action}` | A handler of a user connecting or disconnecting throws. |
| CL23 | `Failed to store received message from {FromUser}` | Storing a message received while the UI runs throws. |
| CL24 | `Failed to store a copy of {MessageId}` | The frame handler stores a message and storing it fails. |
| CL25 | `Failed to read stored messages for a retrieval by {Requester}` | A storage server fails to read messages for a retrieval request. |
| CL26 | `{Kind} transmission failed for {Preview}` | Sending a draft (`Message`) or a staged message (`Staged send`) throws. |
| CL28 | `Failed to store a new draft` | The automatic first store of a new draft throws. |
| CL29 | `Failed to show where the opened entry is kept` | Selecting the folder and entry of what was opened throws. |
| CL30 | `Failed to save what was written before leaving it` | Saving a draft or note when leaving it throws. |
| CL33 | `Failed to load print content for {EntryId}` | Reading the content of a print job throws. |
| CL34 | `Printing {EntryId} on {Printer} failed` | Sending a print job to the printer throws. |
| CL37 | `The frame handler's {Name} failed for {Subject}` | A method of the frame handler throws. |
| CL39 | `The network configuration could not be reloaded: {Message}` | A reload requested by the user fails because the file cannot be read or parsed. |
| CL40 | `External system {Name} stopped unexpectedly` | An external system's run loop throws. |
| CL88 | `Initialization failed` | The initial setup of the main window's view model throws. |
| CL119 | `The configuration handler failed: {Message}` | The host's configuration handler, stated with `Configure<T>()`, throws in `OnReconfigured`, or cannot be built. The engine goes on. |

### CRASH

A failure the application cannot go on after, or that stops a part of it.

| ID | Content | Scenario |
|----|---------|----------|
| CL1 | `Unhandled exception: {Message}` | An exception reaches the top-level handler of the application. |
| CL87 | `{Service} stopped unexpectedly` | A background service (peer, interface, external systems, frame handler, auto forward, disconnect alarm or network indicator) ends other than by being cancelled. |
