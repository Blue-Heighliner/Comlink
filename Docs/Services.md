# Services

Business logic lives in `Engine/src/Services/`. All services are registered as singletons.

```mermaid
graph TD
    PS[PeerService]
    DSC[DirectServiceConnection]
    MRS[MessageRoutingService]
    ES[EntryService]
    MVM[MainViewModel]
    DVM[DraftViewModel]
    EBV[EntryBarViewModel]
    CAV[ContentAreaViewModel]
    PS -->|MessageDelivered event| DSC
    PS -->|DeliveryStatusChanged event| MRS
    MRS -->|DeliveryStatusChanged event| DSC
    DSC -->|UpdateDeliveryStatus| ES
    DSC -->|MessageReceived event| MVM
    DSC -->|DeliveryStatusChanged event| MVM
    MVM -->|StoreIncomingMessage| ES
    CAV -->|creates| DVM
    DVM -->|SendMessage| DSC
    DVM -->|StoreSentMessage| ES
    MVM -->|PrependEntry| EBV
    MVM -->|UpdateEntryStatus| EBV
    EBV -->|EntrySelected event| CAV
```

Message and delivery-status persistence (`StoreIncomingMessage`, `StoreSentMessage`, `UpdateDeliveryStatus`) all go through `EntryService`, which is only ever driven from Client-mode ViewModels (`MainViewModel`, `DraftViewModel`) and `DirectServiceConnection`'s own delivery-status handler — never from `DirectServiceConnection.OnMessageDelivered`/`SendMessage` directly. In Headless mode, no ViewModels are constructed, so a host consuming `IServiceConnection` in that mode observes messages and delivery-status changes purely as events/calls and is responsible for its own persistence if it needs any — the data layer is Client-mode-only (see below).

## UserService

Manages user installation and persists user identity to `State.json`.

**Key responsibilities**:
- Load existing user state on startup (`Load`)
- Install a new user by resolving a code (`Install`)
- Apply a debug override (`IUserIdentity.DebugUserName`) that bypasses `State.json`

**State file**: `{AppDataPath}/State.json` — contains `UserName`, `UserCode`, `EnvironmentTitle`, `EnvironmentColor`. `IsInstalled` is a computed property: `true` when `UserName` is non-null.

**Thread safety**: `Install` uses a `SemaphoreSlim(1,1)` to prevent concurrent installs.

**Debug override**: If `IUserIdentity.DebugUserName` is non-null, `Load` skips the state file entirely and uses it (uppercased) with a synthetic `EnvironmentTitle = "DEBUG"` and color `#FF6200`. Useful for development without a real user code.

```csharp
// Consumers call:
UserInfo? info = service.GetCurrentUserInfo();  // null if not installed
UserState state = service.CurrentState;
await service.Load(cancellation);
UserInfo? installed = await service.Install("SN01", cancellation);
```

---

## MessageRoutingService

Routes outbound messages to peer nodes and surfaces their delivery status. OFT-level delivery status comes entirely from OFT's own delivery status stream (see [Peer.md](Peer.md#delivery-status)); the one application-level status above that — `Read` — comes from the user-read confirmation message flow (see [Peer.md](Peer.md#read-confirmation)).

**Key responsibilities**:
- Build the outbound message via `IMessageFormat` (`CreateMessage()` then the `Set*` logical-field setters, including `SetIsAlert`, `SetPriority`, `SetTag`) so it can be sent as whatever concrete type the host has configured (see [Control.md](Control.md#imessageformat))
- For each recipient in `SendMessagePayload.Addresses`, deliver via `IPeerService.Send`
- Subscribe to `IPeerService.DeliveryStatusChanged` and map each `OftDeliveryStatus` to a `DestinationStatus`, re-raising its own `DeliveryStatusChanged`
- Subscribe to `IPeerService.ConfirmationReceived` and re-raise it as `DeliveryStatusChanged(messageId, confirmingUser, DestinationStatus.Read)` — reusing the same event as OFT-driven status changes

**Events**:
- `DeliveryStatusChanged(messageId, userName, DestinationStatus)` — raised on every per-user status change

**Result timing**: `IPeerService.Send` (and therefore `IOftPeer.Send`) does not return until OFT has fully delivered the message, so `Route`'s own per-user `UserDeliveryResult.Success` already reflects the final outcome by the time `Route` returns — there is no separate "sent but not yet confirmed" pending state to track.

**Self-addressing**: When a recipient user name matches the sending user (`fromUser`), that recipient is delivered in-process via `IPeerService.DeliverLocal` — no network connection is opened, and the delivery status for that user is immediately raised as `Confirmed`. A message can address itself alongside remote users in the same `Route` call; each recipient is handled independently.

```csharp
var (messageId, results) = await routing.Route(fromUser, payload, ct);
// results: IReadOnlyList<UserDeliveryResult> { UserName, Success, AddressedVia }
```

---

## EntryService

CRUD for messages, drafts, notes, and activity log reads. Runs in `Client` mode only.

**Events** (all `Func<entity, Task>`):
- `MessageInserted` — fired after `StoreIncomingMessage`
- `MessageRead` — fired after `MarkMessageRead` transitions an Inbox record from `Received` to `Read`; consumed by `AlertViewModel` to track pending alerts (see [Peer.md](Peer.md#read-confirmation))
- `DraftInserted` — after `CreateDraft`
- `DraftUpdated` — after `SaveDraft`, only if the draft has not yet been sent
- `NoteInserted` — after `CreateNote`
- `NoteUpdated` — after `SaveNote`

Both `StoreIncomingMessage` and `StoreSentMessage` take the message's logical fields (subject, body, addresses, etc.) as plain parameters, plus an `isAlert` flag, and build `MessageEntity.Message` from them via `IMessageFormat` (`CreateMessage()` + `Set*`) before saving — callers never construct the stored message type directly. `MessageEntity.MessageId` is denormalized from the same value passed to `IMessageFormat.SetMessageId` so it stays queryable/indexable (see [Data.md](Data.md#messageentity)).

**Key methods**:

| Method | Description |
|--------|-------------|
| `StoreIncomingMessage(messageId, fromUser, subject, body, addresses, sentAt, isAlert = false, priority = 0, tag = "")` | Creates a `MessageEntity` in the Inbox folder (`IsOutbound = false`, `ReadStatus = Received`), fires `MessageInserted` |
| `StoreSentMessage(messageId, subject, body, addresses, sentAt, userResults, isAlert = false, priority = 0, tag = "")` | Creates a `MessageEntity` in the Outbox (`IsOutbound = true`) with per-user delivery statuses seeded from the routing result — `Confirmed` when `Success` is `true` (a successful send already implies full OFT delivery, see `Docs/Peer.md`), otherwise `Failed` |
| `UpdateDeliveryStatus(messageId, userName, status)` | Updates per-user delivery status on the Outbox record for `messageId` — always scoped to the outbound record, since a self-addressed message also has an Inbox record sharing the same `messageId` |
| `MarkMessageRead(messageId)` | Transitions the Inbox record's `ReadStatus` from `Received` to `Read` and fires `MessageRead`. A no-op (returns `null`) if the record is missing or already `Read` — see [Peer.md](Peer.md#read-confirmation) |
| `CreateDraft()` | Creates a blank draft in the Drafts folder, fires `DraftInserted` |
| `CreateNote()` | Creates a blank note in the Notes folder, fires `NoteInserted` |
| `SaveDraft(entity)` | Persists draft changes, fires `DraftUpdated` if not yet sent |
| `SaveNote(entity)` | Persists note changes, fires `NoteUpdated` |
| `GetMessages(folderId, page)` | Paginated messages, ordered by `ReceivedAt` descending |
| `GetDrafts(folderId, page, alphabetical)` | Paginated drafts |
| `GetNotes(folderId, page, alphabetical)` | Paginated notes |
| `GetActivityLogs(page)` | Paginated activity log entries, newest first |
| `DeleteEntry(id, entryType, isOutboundMessage = false)` | Permanently deletes an entry; `isOutboundMessage` disambiguates the Inbox vs. Outbox record for a self-addressed message |
| `MoveEntry(entryId, entryType, targetFolderId, isOutboundMessage = false)` | Moves an entry to another folder; same disambiguation as `DeleteEntry` |

---

## DirectServiceConnection

Implements `IServiceConnection`, registered in both `Client` and `Headless` mode. Wires engine internals to the interface consumed by ViewModels (Client) or embedding host code (Headless).

**Responsibilities**:
- Forwards `IServiceConnection.SendMessage(subject, body, addresses, isAlert, priority, tag)` → `MessageRoutingService.Route` and returns the result. It does not persist anything itself — in Client mode, `DraftViewModel` calls `EntryService.StoreSentMessage` after a successful send
- Translates `PeerService.MessageDelivered` → fires `IServiceConnection.MessageReceived`. It does not persist the message itself — in Client mode, `MainViewModel`'s handler for that event calls `EntryService.StoreIncomingMessage`
- On `MessageRoutingService.DeliveryStatusChanged`, updates the Outbox record via `EntryService.UpdateDeliveryStatus`, then fires `IServiceConnection.DeliveryStatusChanged` with the resulting `OverallStatus`
- `MarkMessageRead(messageId)`: calls `EntryService.MarkMessageRead`, fires `IServiceConnection.DeliveryStatusChanged` locally (empty `UserName`, status `Read`) so Client-mode UI reflects the read state immediately, then sends a user-read confirmation message to the original sender via `IPeerService.Send` directly — or, for a self-addressed message, calls `EntryService.UpdateDeliveryStatus` directly with no network round-trip. See [Peer.md](Peer.md#read-confirmation)
- Implements install, user info query, and user names query by delegating to `UserService` / `IUserDirectory`

---

## InterfaceService

Hosts the local interface listener described in [Interface.md](Interface.md). Always active, in both `Client` and `Headless` mode. Mirrors `PeerService.MessageDelivered` out to every connected interface connection, and routes messages received from an interface via `MessageRoutingService.Route`.

---

## ExportService

Builds the entry-reference list for a full export and writes selected entries to a zip archive as one JSON file per entry. Backs the export feature described in `Docs/ViewModels.md` (`IExportViewModel`).

**Key responsibilities**:
- `GetAllEntryRefs()` — returns an `ExportEntryRef` (`Id`, `EntryType`, `IsOutboundMessage`) for every message (both Inbox and Outbox, across every folder), draft, note, and activity log document in the database, via each repository's `GetAll()`
- `Export(entries, zipPath, cancellation)` — for each reference, loads the full entity from the appropriate repository, maps it to a clean JSON DTO (`MessageExportData`/`DraftExportData`/`NoteExportData`/`ActivityLogExportData` in `ExportModels.cs`) via `IMessageFormat` for messages, and writes it as `{index}_{EntryType}_{id}.json` inside a new `ZipArchive` at `zipPath`. A reference whose entity has since been deleted is silently skipped.
- On cancellation (or any other failure) mid-write, the partially written zip file at `zipPath` is deleted before the exception propagates — the `try`/`catch` wraps the entire archive-writing block, so this holds regardless of how many entries had already been written.

Message content is read through `IMessageFormat`, matching every other message read path in Engine — `ExportService` has no knowledge of the host's concrete message type.

`IExportService.PackageExtension` (a `const` interface member, `".export.zip"`) is the file extension every export package is written with, distinguishing it from an ordinary zip file so `ImportService.GetPackages` can find it on a drive. `ExportViewModel` appends this to the user-entered file name.

```csharp
IReadOnlyList<ExportEntryRef> refs = await exportService.GetAllEntryRefs();
await exportService.Export(refs, "/media/usb/backup" + IExportService.PackageExtension, cancellation);
```

---

## ImportService

Lists export packages on a drive and restores their entries into the local database. Backs the import feature described in `Docs/ViewModels.md` (`IImportViewModel`). Operates directly on the repositories, the same way `ExportService` does — it is a bulk data-restore operation, not a "live" business event, so it does not raise `IEntryService`'s insert/update events (no retroactive alerting for an imported alert message, no auto-navigation to Drafts/Notes; the imported data is visible as soon as the user browses to the relevant folder, since `EntryBarViewModel.Refresh()` always re-queries the database).

**Key responsibilities**:
- `GetPackages(driveRootPath)` — returns every `IExportService.PackageExtension` file directly under the drive root as an `ImportPackageInfo` (`FileName`, `FullPath`), ordered by file name. Returns an empty list (never throws) if the path is inaccessible.
- `Import(packagePath, resolveConflict)` — opens the package and, for each JSON entry (its `EntryType` is parsed from the `{index}_{EntryType}_{id}.json` file name), applies per-type conflict resolution:
  | Entry type | Match key | On conflict |
  |---|---|---|
  | Message | `MessageId` + direction (`IsOutbound`), and the same calendar date (`ReceivedAt.Date`) | Skipped — no prompt |
  | Draft | `Subject`, trimmed | Invokes `resolveConflict` (unless a prior conflict in this call chose `OverwriteAll`) |
  | Note | First line of `Body`, trimmed (same rule `EntryBarViewModel` uses for a note's display title) | Invokes `resolveConflict` (unless a prior conflict in this call chose `OverwriteAll`) |
  | Activity log | `Date` | Always merged (see below) — no prompt |

  For a draft/note conflict, `resolveConflict` is awaited once and the returned `DraftNoteConflictResolution` applied: `KeepExisting` skips the imported entry; `Overwrite` replaces the existing entry's content fields (keeping its `Id` and `FolderId`); `OverwriteAll` overwrites this entry and is remembered for the rest of this `Import` call, so every subsequent draft/note conflict overwrites without asking again. New (non-conflicting) drafts/notes/messages are inserted into the corresponding root folder (`root-drafts`/`root-notes`/`root-inbox`/`root-outbox`); imported folder IDs are not preserved, since they are opaque to the source installation.
- Activity log merge: when an existing log exists for the imported log's `Date`, each imported `ActivityLogEntry` is checked against the existing entries — an exact match (`At` and `Message` both equal) is skipped, otherwise the line is inserted into the existing (already chronologically ordered) list at the position where its `At` timestamp keeps the list sorted. When no log exists for that date, the imported log is inserted as-is.
- Returns an `ImportSummary` (`Imported`, `Skipped`, `Overwritten` counts) once every entry in the package has been processed.

```csharp
IReadOnlyList<ImportPackageInfo> packages = importService.GetPackages("/media/usb");
ImportSummary summary = await importService.Import(packages[0].FullPath, conflict =>
{
    // Prompt the user; return their choice.
    return Task.FromResult(DraftNoteConflictResolution.Overwrite);
});
```

---

## ConnectionModels

DTOs used across the service layer:

| Type | Fields |
|------|--------|
| `MessageReceivedEvent` | `MessageId`, `FromUser`, `Subject`, `Body`, `Addresses[]`, `SentAt`, `IsAlert`, `Priority`, `Tag` |
| `AddressRequest` | `UserName`, `Type` |
| `UserDeliveryResult` | `UserName`, `Success (bool)`, `AddressedVia[]` |
| `SendMessageResult` | `MessageId`, `UserResults[]` |
| `DeliveryStatusChangedEvent` | `MessageId`, `UserName`, `Status`, `OverallStatus` — an empty `UserName` marks a local read-status notification for this user's own Inbox record rather than a remote destination (see [Peer.md](Peer.md#read-confirmation)) |
| `SendMessagePayload` | `Subject`, `Body`, `Addresses[]` (of `AddressPayload`), `IsAlert`, `Priority`, `Tag` |
| `AddressPayload` | `UserName`, `Type` |

---

## ExportModels

DTOs used by `ExportService` (`Engine/src/Services/ExportModels.cs`):

| Type | Fields |
|------|--------|
| `ExportEntryRef` | `Id`, `EntryType`, `IsOutboundMessage` — identifies one entry to export |
| `MessageExportData` | `MessageId`, `IsOutbound`, `FromUser`, `Subject`, `Body`, `Addresses[]`, `SentAt`, `IsAlert`, `Priority`, `Tag`, `ReceivedAt`, `ReadStatus`, `DeliveryStatuses[]` |
| `DraftExportData` | `Id`, `Subject`, `Body`, `Addresses[]`, `IsSent`, `IsAlert`, `Priority`, `Tag`, `SentAt`, `CreatedAt`, `ModifiedAt` |
| `NoteExportData` | `Id`, `Body`, `CreatedAt`, `ModifiedAt` |
| `ActivityLogExportData` | `Id`, `Date`, `EventEntries[]` |

---

## ImportModels

DTOs used by `ImportService` (`Engine/src/Services/ImportModels.cs`):

| Type | Fields |
|------|--------|
| `ImportPackageInfo` | `FileName`, `FullPath` — an export package found on a drive |
| `ImportConflict` | `EntryType` (always `Draft` or `Note`), `Name` (the conflicting subject or note first line) |
| `DraftNoteConflictResolution` (enum) | `KeepExisting`, `Overwrite`, `OverwriteAll` |
| `ImportSummary` | `Imported`, `Skipped`, `Overwritten` (counts) |
