# Configuration Interfaces

All external configuration and rule-based behavior — including the concrete message type and its logical field mapping — is expressed as members on a single interface, `IEngineController`, declared in `Engine/src/Control/EngineController.cs`. `DefaultEngineController<TMessage>` is generic over the host's concrete message type and `abstract` (its message-field members are `protected abstract`), so a host must always define and register its own subclass — there is no generic-free Engine default to fall back on. See [Control.md](Control.md) for the full member-by-member reference — including the `ConfiguredEngineController` decorator that applies `config.json` on top of whichever implementation is registered, the `Default{Thing}` naming convention (virtual members a host can inherit and override just one of), and the interface (`IServiceConnection`, a consumed client API) that is deliberately *not* part of `IEngineController`.

## `IEngineController` members, by app area

| App area | Members |
|---|---|
| Message Format | `MessageType`, `CreateMessage()`, and a `Get`/`Set` pair for each logical field (message id, sender, subject, body, addresses, sent time, confirmation id, alert flag, priority, tag) |
| App Settings | `AppName`, `AppDataPath`, `IsKioskMode`, `HomeText` |
| User Identity | `DebugUserName`, `ResolveCode(userCode)` |
| User Directory | `GetEndpoint(userName)`, `UserGroups`, `Users` |
| Ports | `PeerPort` (default `50021`), `InterfacePort` (default `50020`) |
| Alert Settings | `AlertLabel`, `AlarmSoundDuration`, `QuickConfirmationEnabled`, `ComposeAlertsEnabled` |
| Message Composition | `Priorities`, `TagsEnabled`, `TagLabel`, `BlockedCombinations` |
| Print Policy | `PrintReceivedDefaultEnabled`, `GetPrintCount(message)` |
| OFT Certificate | `GetCertificateName(userName)`, `ConnectionOptions` |
| Network Topology | `Role`, `ServerEndpoint`, `Servers` |
| Config File | `ConfigFileEnabled` |
| External Systems | `GetExternalSystems()` |

Members with a corresponding `config.json` field are listed in [Config.md](Config.md) and overridden field-by-field by `ConfiguredEngineController`; every other member always delegates straight to whichever `IEngineController` implementation is registered (the host's `DefaultEngineController<TMessage>` subclass). See [Control.md](Control.md) for the full description, Engine default, config override, and Sample override of each app area.

## Not part of `IEngineController`

- **`IServiceConnection`** — the client-facing API surface a host *consumes* to drive the running Engine (send messages, install the user, subscribe to delivery events), not a piece of configuration a host swaps in. Engine registers `DirectServiceConnection`. See [Control.md](Control.md#iserviceconnection).
- **`IAlertSoundPlayer`** — real OS-level alarm sound playback, not configuration. Always provided by the engine itself (`Engine/src/Devices/AlertSoundPlayer.cs`), never overridden by a host. See [Control.md](Control.md#ialertsoundplayer-not-a-control-interface).
- **`IPrintDriver`** — real OS-level printer discovery and line-printing, not configuration. Always provided by the engine itself (`Engine/src/Devices/PrintDriver.cs`), never overridden by a host. See [Control.md](Control.md#iprintdriver-not-a-control-interface).
- **`IExternalDriveProvider`** — real OS-level external drive discovery, not configuration. Always provided by the engine itself (`Engine/src/Devices/ExternalDriveProvider.cs`), never overridden by a host. See [Control.md](Control.md#iexternaldriveprovider-not-a-control-interface).
- **`ICurrentUserProvider`** — mutable runtime state (the currently installed user name), not configuration. See [Control.md](Control.md#icurrentuserprovider--currentuserprovider).
