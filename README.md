# Comlink

[![NuGet](https://img.shields.io/nuget/v/BlueHeighliner.Comlink.svg?label=NuGet)](https://www.nuget.org/packages/BlueHeighliner.Comlink)
[![License: MIT](https://img.shields.io/github/license/Blue-Heighliner/Comlink.svg)](LICENSE)
[![Build](https://github.com/Blue-Heighliner/Comlink/actions/workflows/build.yml/badge.svg)](https://github.com/Blue-Heighliner/Comlink/actions/workflows/build.yml)
[![Coverage](.github/badges/badge_linecoverage.svg)](https://github.com/Blue-Heighliner/Comlink/actions/workflows/build.yml)

A peer-to-peer messaging engine built on .NET 10, with a built-in Avalonia desktop GUI. Nodes connect over IP using the Mercury Secure Message Transport (MSMT) protocol, or point-to-point over MicroGate serial cables, chosen per user by configuration. The GUI is a required dependency, not optional - it can run headless (no window shown), but Avalonia and its dependencies are always loaded.

## Installing

```sh
dotnet add package BlueHeighliner.Comlink
```

## Getting started

A host implements `IEngineController` (via `DefaultEngineController<TMessage>`, generic over its own message DTO) and starts the engine with `Engine.Start`:

```csharp
public sealed class MyEngineController(ICurrentUserProvider currentUserProvider)
    : DefaultEngineController<MyMessage>(currentUserProvider)
{
    protected override string GetMessageId(MyMessage message) => message.Id;
    // ...every other required message-field member...
}

await Engine.Start(args, services => services.AddSingleton<IEngineController, MyEngineController>());
```

See `Sample/` for a complete, runnable host, and `Docs/Usage.md` for further examples.

## Documentation

| File | Covers |
|------|--------|
| [Docs/Api.md](Docs/Api.md) | Public API design and flow — `IEngineController`, `Engine.Start` |
| [Docs/Architecture.md](Docs/Architecture.md) | High-level design decisions |
| [Docs/Project.md](Docs/Project.md) | This repo's own tooling: `Scripts/`, publishing, CI |
| [Docs/Usage.md](Docs/Usage.md) | Runnable usage examples |
| [Docs/Components/](Docs/Components/) | One file per complex internal component/subsystem |

## License

[MIT](LICENSE)
