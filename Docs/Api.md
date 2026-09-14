# API

The public API is `IEngineController` and the static `Engine` entry point. This document covers
the *design and flow* of that surface — how the pieces fit together and the order things happen
in — using public types only. It does not restate member-level detail already covered in the
source itself.

## Shape

A host implements `IEngineController` — the single interface through which Core reads every piece
of external configuration and rule-based behavior it needs, including the concrete message DTO
type and its logical field mapping. `DefaultEngineController<TMessage>` is the base a host
actually derives from: it is generic over the host's message type and `abstract`, since Core has
no message DTO of its own, so every other member has a sensible default and only the
message-field members must be implemented.

`Engine.Start(string[] args, Action<IServiceCollection> configureServices)` is the only entry
point. It takes no controller type or instance directly — `configureServices` registers the
host's `IEngineController` subclass exactly like any other service, and `Engine` resolves it from
the same container `configureServices` populates. This is deliberately asymmetric with a typical
generic-host-builder API: a single fixed two-parameter method, rather than a generic type
argument or a constructor-injected instance, keeps the entry point identical regardless of how
elaborate a host's own service registration becomes.

```csharp
await Engine.Start(args, services => services.AddSingleton<IEngineController, MyEngineController>());
```

## Flow

1. `Engine.Start` builds a minimal, throwaway service provider from `configureServices` alone and
   resolves `IEngineController` from it, to read `ConfigFileEnabled` before anything else exists.
2. If `ConfigFileEnabled` is true (the default), `EngineConfig.Load(args)` reads `--config`;
   otherwise every setting uses its default and `--config` is ignored.
3. If `EngineConfig.HeadlessMode` is set, `Engine` builds and runs an `IHost` with no UI. Otherwise
   it builds and shows the Avalonia desktop application.
4. In both cases, `configureServices` runs again against the real container, registering the
   host's `IEngineController` alongside every other service, and a decorator layers `EngineConfig`
   on top of whichever configuration members have a corresponding `config.json` field.
5. Once started, a host interacts with the running engine through `IServiceConnection` — sending
   messages, observing delivery status, and reading/writing entries — the same surface whether
   running with or without a UI.

## Modes

`EngineConfig.HeadlessMode` selects between `Client` (desktop UI) and `Headless` (no UI) at
startup. Both modes run the same peer listener, local interface listener, and persistence layer;
Headless mode does not remove any dependency from the build, it only skips showing a window.

## Configuration

Every piece of host-specific behavior is a member on `IEngineController` — never an environment
variable, and never a hardcoded path. Members with a corresponding `config.json` field are
overridden by a config-driven decorator layered on top of the host's own implementation; a host
implementation never reads `config.json` itself. `IEngineController.ConfigFileEnabled` decides
whether `config.json` is read at all, and is itself resolved before `EngineConfig` exists, so it
can never have a `config.json` field of its own.
