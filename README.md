# Comlink

[![NuGet](https://img.shields.io/nuget/v/BlueHeighliner.Comlink.svg?label=NuGet)](https://www.nuget.org/packages/BlueHeighliner.Comlink)
[![License: MIT](https://img.shields.io/github/license/Blue-Heighliner/Comlink.svg)](LICENSE)
[![C#](https://github.com/Blue-Heighliner/Comlink/actions/workflows/csharp.yml/badge.svg)](https://github.com/Blue-Heighliner/Comlink/actions/workflows/csharp.yml)

A peer-to-peer messaging system built on .NET 10 and the [Open Frame Transport (OFT)](Docs/Oft.md) protocol.

## Projects

| Project | Description |
|---------|-------------|
| **Engine** | The whole engine — networking, data, services, ViewModels, and the Avalonia UI layer (Views, Themes, converters) — in one library. |
| **Sample** | Host application using Engine in GUI mode or headless mode. |
| **Tests** | xUnit tests for Engine services and ViewModels. |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Installing

Engine is published as a single NuGet package covering both the core library and the Avalonia UI layer. The Avalonia GUI is a required dependency of this package, not optional — a host can run Engine headless (no window shown, see [Docs/Architecture.md](Docs/Architecture.md#modes)), but Avalonia and its dependencies are always pulled in:

```sh
dotnet add package BlueHeighliner.Comlink
```

## Building

```sh
dotnet build
```

## Running

```sh
dotnet run --project Sample -- --config Configs/TEST1.json
```

Omit `--config` to use all defaults (GUI mode, default ports, system app data folder).

See [Docs/Config.md](Docs/Config.md) for the full configuration reference.

## Documentation

| File | Covers |
|------|--------|
| [Docs/Architecture.md](Docs/Architecture.md) | System overview, modes, startup sequence, data layout |
| [Docs/Config.md](Docs/Config.md) | All `config.json` fields and examples |
| [Docs/Interface.md](Docs/Interface.md) | Local interface listener contract (Headless mode) |
| [Docs/Peer.md](Docs/Peer.md) | Peer-to-peer networking protocol |
| [Docs/Oft.md](Docs/Oft.md) | Open Frame Transport (OFT) protocol reference |
| [Docs/Data.md](Docs/Data.md) | LiteDB entities and database layout |
| [Docs/Services.md](Docs/Services.md) | Business logic services |
| [Docs/ViewModels.md](Docs/ViewModels.md) | MVVM layer |
| [Docs/Logging.md](Docs/Logging.md) | Logging providers and format |
| [Docs/Control.md](Docs/Control.md) | DI control interfaces — required vs optional, each interface explained |
| [Docs/Configuration.md](Docs/Configuration.md) | DI configuration interfaces |

## License

[MIT](LICENSE)
