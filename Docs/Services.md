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

## SiteService

Manages site installation and persists site identity to `State.json`.

**Key responsibilities**:
- Load existing site state on startup (`Load`)
- Install a new site by resolving a code (`Install`)
- Apply a debug override (`IDebugSiteOverride`) that bypasses `State.json`

**State file**: `{AppDataPath}/State.json` — contains `SiteName`, `SiteCode`, `EnvironmentTitle`, `EnvironmentColor`. `IsInstalled` is a computed property: `true` when `SiteName` is non-null.

**Thread safety**: `Install` uses a `SemaphoreSlim(1,1)` to prevent concurrent installs.

**Debug override**: If any `IDebugSiteOverride` is registered, `Load` skips the state file entirely and uses the override's `SiteName` (uppercased) with a synthetic `EnvironmentTitle = "DEBUG"` and color `#FF6200`. Useful for development without a real site code.

```csharp
// Consumers call:
SiteInfo? info = service.GetCurrentSiteInfo();  // null if not installed
SiteState state = service.CurrentState;
await service.Load(cancellation);
SiteInfo? installed = await service.Install("SN01", cancellation);
```

---

## MessageRoutingService

Routes outbound messages to peer nodes and surfaces their delivery status. There is no custom acknowledgement or confirmation layer — delivery status comes entirely from OFT's own delivery status stream (see [Peer.md](Peer.md#delivery-status)).

**Key responsibilities**:
- Build the outbound message via `IMessageFormat` (`CreateMessage()` then the `Set*` logical-field setters) so it can be sent as whatever concrete type the host has configured (see [Control.md](Control.md#imessageformat))
- For each recipient in `SendMessagePayload.Addresses`, deliver via `IPeerService.Send`
- Subscribe to `IPeerService.DeliveryStatusChanged` and map each `OftDeliveryStatus` to a `DestinationStatus`, re-raising its own `DeliveryStatusChanged`

**Events**:
- `DeliveryStatusChanged(messageId, siteName, DestinationStatus)` — raised on every per-site status change

**Result timing**: `IPeerService.Send` (and therefore `IOftPeer.Send`) does not return until OFT has fully delivered the message, so `Route`'s own per-site `SiteDeliveryResult.Success` already reflects the final outcome by the time `Route` returns — there is no separate "sent but not yet confirmed" pending state to track.

**Self-addressing**: When a recipient site name matches the sending site (`fromSite`), that recipient is delivered in-process via `IPeerService.DeliverLocal` — no network connection is opened, and the delivery status for that site is immediately raised as `Confirmed`. A message can address itself alongside remote sites in the same `Route` call; each recipient is handled independently.

```csharp
var (messageId, results) = await routing.Route(fromSite, payload, ct);
// results: IReadOnlyList<SiteDeliveryResult> { SiteName, Success, AddressedVia }
```

---

## EntryService

CRUD for messages, drafts, notes, and activity log reads. Runs in `Client` mode only.

**Events** (all `Func<entity, Task>`):
- `MessageInserted` — fired after `StoreIncomingMessage`
- `DraftInserted` — after `CreateDraft`
- `DraftUpdated` — after `SaveDraft`, only if the draft has not yet been sent
- `NoteInserted` — after `CreateNote`
- `NoteUpdated` — after `SaveNote`

Both `StoreIncomingMessage` and `StoreSentMessage` take the message's logical fields (subject, body, addresses, etc.) as plain parameters and build `MessageEntity.Message` from them via `IMessageFormat` (`CreateMessage()` + `Set*`) before saving — callers never construct the stored message type directly. `MessageEntity.MessageId` is denormalized from the same value passed to `IMessageFormat.SetMessageId` so it stays queryable/indexable (see [Data.md](Data.md#messageentity)).

**Key methods**:

| Method | Description |
|--------|-------------|
| `StoreIncomingMessage(messageId, fromSite, subject, body, addresses, sentAt)` | Creates a `MessageEntity` in the Inbox folder (`IsOutbound = false`), fires `MessageInserted` |
| `StoreSentMessage(messageId, subject, body, addresses, sentAt, siteResults)` | Creates a `MessageEntity` in the Outbox (`IsOutbound = true`) with per-site delivery statuses seeded from the routing result — `Confirmed` when `Success` is `true` (a successful send already implies full OFT delivery, see `Docs/Peer.md`), otherwise `Failed` |
| `UpdateDeliveryStatus(messageId, siteName, status)` | Updates per-site delivery status on the Outbox record for `messageId` — always scoped to the outbound record, since a self-addressed message also has an Inbox record sharing the same `messageId` |
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
- Forwards `IServiceConnection.SendMessage` → `MessageRoutingService.Route` and returns the result. It does not persist anything itself — in Client mode, `DraftViewModel` calls `EntryService.StoreSentMessage` after a successful send
- Translates `PeerService.MessageDelivered` → fires `IServiceConnection.MessageReceived`. It does not persist the message itself — in Client mode, `MainViewModel`'s handler for that event calls `EntryService.StoreIncomingMessage`
- On `MessageRoutingService.DeliveryStatusChanged`, updates the Outbox record via `EntryService.UpdateDeliveryStatus`, then fires `IServiceConnection.DeliveryStatusChanged` with the resulting `OverallStatus`
- Implements install, site info query, and site names query by delegating to `SiteService` / `ISiteNameDirectory`

---

## InterfaceService

Hosts the local interface listener described in [Interface.md](Interface.md). Always active, in both `Client` and `Headless` mode. Mirrors `PeerService.MessageDelivered` out to every connected interface connection, and routes messages received from an interface via `MessageRoutingService.Route`.

---

## ConnectionModels

DTOs used across the service layer:

| Type | Fields |
|------|--------|
| `MessageReceivedEvent` | `MessageId`, `FromSite`, `Subject`, `Body`, `Addresses[]`, `SentAt` |
| `AddressRequest` | `SiteName`, `Type` |
| `SiteDeliveryResult` | `SiteName`, `Success (bool)`, `AddressedVia[]` |
| `SendMessageResult` | `MessageId`, `SiteResults[]` |
| `DeliveryStatusChangedEvent` | `MessageId`, `SiteName`, `Status`, `OverallStatus` |
| `SendMessagePayload` | `Subject`, `Body`, `Addresses[]` (of `AddressPayload`) |
| `AddressPayload` | `SiteName`, `Type` |
