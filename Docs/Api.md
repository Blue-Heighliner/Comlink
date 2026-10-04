# API

The public API is the static `Engine` entry point, the `IEngineConfiguration` a host passes to it, and the fluent
builders that configuration is written against. This document covers the *design and flow* of that surface: how the
pieces fit together and the order things happen in, using public types only. It does not restate member-level detail
already covered in the source itself.

## Shape

A host implements `IEngineConfiguration`, whose single `Configure` method receives an `IEngineBuilder` and states, through
fluent calls, how the engine should run. It starts with `Types`, which fixes the frame type, the optional packet type, the
priority enum and the security level enum, and returns the typed builder every other setting is stated on, so handlers,
priorities and security levels are all checked against those types. After `Types`, the handlers for each kind of frame are
required and anything else that should differ from the engine's defaults is optional (see [Configuration.md](Components/Configuration.md)).
Core has no frame DTO of its own.

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
   `InvalidOperationException` if the configuration cannot be constructed, the frame type is missing, or a logical field
   is unmapped. The bootstrap container stays alive for the life of the engine, since the configuration may have handed the
   builder functions that use what was injected.
2. `NetworkConfig.Load` reads the network configuration from `Config.json` in the working directory and the running user from
   `User.json` there; when the configuration allowed command-line overrides (`CommandLineOverrides`, off by default), `--config`
   and `--user` take precedence, and otherwise they are ignored.
3. If the `--user` user's entry sets `Headless`, `Engine` builds and runs an `IHost` with no UI. Otherwise
   it builds and shows the Avalonia desktop application.
4. In both cases the engine's services are registered from what the host stated, with a decorator layering
   the network file's node settings for the current user on top of the settings that have a corresponding
   field, and the host's
   `configureServices` registrations run again against the engine's own container, so a service the configuration used
   and a service the running engine uses are separate instances.
5. Once started, a host interacts with the running engine through `IServiceConnection`, sending
   messages, observing delivery status, and reading/writing entries: the same surface whether
   running with or without a UI.

## Modes

The `--user` user's `Headless` setting selects between `Client` (desktop UI) and `Headless` (no UI) at
startup. Both modes run the same peer listener, local interface listener, and persistence layer;
Headless mode does not remove any dependency from the build, it only skips showing a window.

## Configuration

Every piece of host-specific behavior is a call on `IEngineBuilder`, never an environment
variable and never a hardcoded path. Everything about the network's users comes from the network
configuration file, and the node settings that have a corresponding field are applied by a decorator layered
on top of what the host stated; a configuration never reads the file itself. Whether the file is read at all
is itself a setting, resolved before `NetworkConfig` exists, so it can never have a field of its own.

## Package layout

Everything in `Core/src/Public` and the two primary types at the root of `Core/src` (`Engine` and `IEngineConfiguration`)
is the package's public surface, and everything under `Core/src/Internal` is not. A test guards the exported set so
nothing becomes public by accident.
