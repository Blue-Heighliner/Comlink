# API

The public API is the static `Engine` entry point, the `IEngineConfiguration` a host passes to it, and the fluent
builders that configuration is written against. This document covers the *design and flow* of that surface: how the
pieces fit together and the order things happen in, using public types only. It does not restate member-level detail
already covered in the source itself.

## Shape

A host implements `IEngineConfiguration`, whose single `Configure` method receives an `IEngineBuilder` and states, through
fluent calls, how the engine should run: the concrete message type and how its fields map onto the engine's logical
fields, and anything else that should differ from the engine's defaults (see [Configuration.md](Components/Configuration.md)).
Only the message type is required; Core has no message DTO of its own.

`Engine.Start<TConfiguration>(string[] args, Action<IServiceCollection>? configureServices = null)` is the only entry
point. The host never builds an engine object: it names the type that describes what it wants, and the engine constructs
it through dependency injection, so a configuration can take services in its constructor (a logger, options, its own
services) rather than reaching for them statically. The optional `configureServices` registers the host's own services:
the ones the configuration depends on, and any others the host wants in the running engine, such as a hosted service that
uses `IServiceConnection`.

```csharp
await Engine.Start<MyEngineConfiguration>(args, services => services.AddSingleton<IClock, SystemClock>());
```

## Flow

1. `Engine.Start` builds a bootstrap container holding logging plus the host's `configureServices` registrations,
   constructs the configuration from it, runs `Configure` against a new builder, and checks the result, failing with an
   `InvalidOperationException` if the configuration cannot be constructed, the message type is missing, or a logical field
   is unmapped. The bootstrap container stays alive for the life of the engine, since the configuration may have handed the
   builder functions that use what was injected.
2. If the configuration allowed it (`ConfigFile`, off by default), `EngineConfigFile.Load(args)` reads `--config`;
   otherwise every setting uses what the host stated or its default and `--config` is ignored.
3. If `EngineConfigFile.HeadlessMode` is set, `Engine` builds and runs an `IHost` with no UI. Otherwise
   it builds and shows the Avalonia desktop application.
4. In both cases the engine's services are registered from what the host stated, with a decorator layering
   `EngineConfigFile` on top of every setting that has a corresponding `config.json` field, and the host's
   `configureServices` registrations run again against the engine's own container, so a service the configuration used
   and a service the running engine uses are separate instances.
5. Once started, a host interacts with the running engine through `IServiceConnection`, sending
   messages, observing delivery status, and reading/writing entries: the same surface whether
   running with or without a UI.

## Modes

`EngineConfigFile.HeadlessMode` selects between `Client` (desktop UI) and `Headless` (no UI) at
startup. Both modes run the same peer listener, local interface listener, and persistence layer;
Headless mode does not remove any dependency from the build, it only skips showing a window.

## Configuration

Every piece of host-specific behavior is a call on `IEngineBuilder`, never an environment
variable and never a hardcoded path. Settings with a corresponding `config.json` field are
overridden by a config-driven decorator layered on top of what the host stated; a configuration
never reads `config.json` itself. Whether `config.json` is read at all is itself a setting, resolved before
`EngineConfigFile` exists, so it can never have a `config.json` field of its own.

## Package layout

Everything in `Core/src/Public` and the two primary types at the root of `Core/src` (`Engine` and `IEngineConfiguration`)
is the package's public surface, and everything under `Core/src/Internal` is not. A test guards the exported set so
nothing becomes public by accident.
