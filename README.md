# Comlink

[![NuGet](https://img.shields.io/nuget/v/BlueHeighliner.Comlink.svg?label=NuGet)](https://www.nuget.org/packages/BlueHeighliner.Comlink)
[![License: MIT](https://img.shields.io/github/license/Blue-Heighliner/Comlink.svg)](LICENSE)
[![Build](https://github.com/Blue-Heighliner/Comlink/actions/workflows/build.yml/badge.svg)](https://github.com/Blue-Heighliner/Comlink/actions/workflows/build.yml)
[![Coverage](.github/badges/badge_linecoverage.svg)](https://github.com/Blue-Heighliner/Comlink/actions/workflows/build.yml)

A peer-to-peer messaging engine built on .NET 10, with a built-in Avalonia desktop GUI. Nodes connect over IP using the Mercury Secure Message Transport (MSMT) protocol, or point-to-point over MicroGate serial cables, chosen per connection point by configuration. A node is configured with where it listens and connects, and works out who is on the other end of each connection as it forms. The GUI is a required dependency, not optional - it can run headless (no window shown), but Avalonia and its dependencies are always loaded.

## Installing

```sh
dotnet add package BlueHeighliner.Comlink
```

## Getting started

A host implements `IEngineConfiguration`, whose fluent `Configure` method states how the engine runs (starting with its own message DTO), and starts the engine with `Engine.Start<T>`, which constructs it through dependency injection:

```csharp
public sealed class MyEngineConfiguration : IEngineConfiguration
{
    public IEngineBuilder Configure(IEngineBuilder engine) => engine
        .Message<MyMessage>(message => message
            .Id(m => m.Id)
            // ...every other logical field...
            )
        .HomeText("Select a folder and entry to get started.");
}

await Engine.Start<MyEngineConfiguration>(args);
```

See `Sample/` for a complete, runnable host, and `Docs/Usage.md` for further examples.

## Documentation

| File | Covers |
|------|--------|
| [Docs/Api.md](Docs/Api.md) | Public API design and flow - `IEngineConfiguration`, `IEngineBuilder`, `Engine.Start` |
| [Docs/Architecture.md](Docs/Architecture.md) | High-level design decisions |
| [Docs/Project.md](Docs/Project.md) | This repo's own tooling: `Scripts/`, publishing, CI |
| [Docs/Usage.md](Docs/Usage.md) | Runnable usage examples |
| [Docs/Components/](Docs/Components/) | One file per complex internal component/subsystem |

## License

[MIT](LICENSE)
