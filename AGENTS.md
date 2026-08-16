# AGENTS.md

Guidance for AI agents working in this repository.

## Project Overview

Comlink is a peer-to-peer messaging system. The solution has three projects:

- **Engine** — the whole engine, in one assembly: networking, data, services, ViewModels, and the Avalonia UI layer (Views, Themes, converters, `TextDocumentBodyDocument`). The only project that depends on Avalonia. The GUI is Avalonia-based and is **not optional** — Avalonia and its packages are a hard dependency of the `BlueHeighliner.Comlink` NuGet package and are always loaded, even in Headless mode (see [Modes](Docs/Architecture.md#modes)); `HeadlessMode` only skips showing a window, it does not remove Avalonia from the dependency graph.
- **Sample** — host application. References Engine.
- **Tests** — xUnit tests for Engine.

The `Docs/` folder contains component documentation. **Review and update relevant docs whenever you change Engine behavior, add new features, or modify public interfaces.**

## Documentation

| File | Covers |
|------|--------|
| `Docs/Architecture.md` | System overview, modes, startup sequence, data storage layout |
| `Docs/Config.md` | All `config.json` fields and examples |
| `Docs/Interface.md` | Local interface listener contract for external programs (Headless mode) |
| `Docs/Peer.md` | Peer-to-peer networking protocol |
| `Docs/Oft.md` | Open Frame Transport (OFT) protocol reference |
| `Docs/Data.md` | LiteDB entities, repositories, database layout |
| `Docs/Services.md` | Business logic services |
| `Docs/ViewModels.md` | MVVM layer |
| `Docs/Logging.md` | Logging providers and format |
| `Docs/Control.md` | DI control interfaces — concept, required vs optional, each interface explained |
| `Docs/Configuration.md` | All DI configuration interfaces |
| `Docs/ExternalSystems.md` | External system conduit contract, lifecycle, relay/mirror behavior, Sample demo |

When making changes that affect the interface wire format, peer protocol, data schema, or any configuration interface, update the corresponding doc file in the same commit.

## Code Conventions

### Language and Framework
- .NET 10, C# 13. Use the latest language features without hesitation.
- File-scoped namespaces everywhere.
- Each project has a single `Using.cs` file containing all `global using` directives for that project. Do not place `using` directives in individual files.
- Avalonia 11.3 for UI. AvaloniaEdit 11.3 for the body editor.

### Style
- No comments unless the **why** is non-obvious — a hidden constraint, a platform quirk, a non-obvious invariant. Never comment what the code obviously does.
- No multi-line comment blocks.
- All types and members (`public` and `internal`) require full XML documentation comments (`<summary>`, `<param>`, `<returns>`, `<exception>`, etc. as applicable). Use `<inheritdoc />` on members that implement a documented interface without adding meaningful additional documentation.
- `sealed` on all classes that are not designed for inheritance.
- Prefer `record` types with `required init` properties for DTOs and model types.
- `internal` by default; only `public` what a host application genuinely needs to consume.
- One type per file, with these exceptions: an interface and its implementing class are co-located in one file named after the class (e.g. `IThing` and `Thing` both live in `Thing.cs`); extension classes are co-located with the class they extend (e.g. `ThingExtensions` also lives in `Thing.cs`); a standalone interface with no co-located implementation is named after its concept without the `I` prefix (e.g. `IOther` lives in `Other.cs`).
- **Member ordering**: group static members at the top of a type, then instance members below them. Within each group, order members by kind: constructors, fields, events, properties, operators, methods. Applies to interfaces too (events, then properties, then methods) — an interface's members are not exempt just because it has no constructors or fields.
- Always use braces for blocks — never an implicit one-line `if`/`else`/`for`/`foreach`/`while`/etc. Always write `if (...) { ... }`, never `if (...) ...`.
- Prefer expression-bodied members over full block bodies when possible and practical (e.g. `void Do() => Action();`). For methods, when the signature and expression body don't fit on one line, wrap with `=>` indented on its own line beneath the signature, not trailing at the end of the signature line. For properties, the `=>` always stays on the same line as the signature (e.g. `public int Foo => value;`), even if the expression itself then needs to wrap onto following lines — never move the `=>` itself down to its own line as with methods.
- Do not use comments to designate sections of members (e.g. `// ── Section ──` dividers). All members should simply be ordered according to the member ordering rule above — no additional grouping by topic/area via comments.
- Do not define a private static field solely to back an instance property that always returns the same value. Initialize the instance property directly instead (e.g. `public IReadOnlyList<X> Foo { get; } = [...];`) rather than adding a separate `private static readonly` field just to hold that value.
- When a property returns a reference-type value (e.g. a list, array, dictionary, or other object), prefer storing that value in the property's own backing field, computed once, rather than an expression body that constructs a new value on every access. Do `IList<int> Numbers { get; } = [5, 2, 3];`, not `IList<int> Numbers => [5, 2, 3];`.
- Prefer private instance fields over private static fields, even when the value is the same for every instance. Reserve `static` for cases that genuinely require it (e.g. backing a static member, or a true compile-time `const`).
- Do not prefix private field names with an underscore. Use plain camelCase for private fields (e.g. `engineController`); public/internal properties use PascalCase (e.g. `EngineController`) — the casing itself is what distinguishes a private field from a public property, not a leading underscore.
- For private fields holding plain internal data (not an injected DI dependency), declare the field with its concrete/plain class type rather than an interface (e.g. `private readonly Dictionary<string, UserInfo> userCodes = new();`, not `IReadOnlyDictionary<string, UserInfo>`; `private readonly List<string> users = [...];`, not `IReadOnlyList<string>`). This does not apply to DI-injected constructor/property dependencies, which continue to use their interface type per the DI convention.
- Prefer primary constructors when possible.

### Patterns
- **DI first**: all external configuration and rule-based behavior — including the concrete message type and its logical field mapping — is consolidated into a single interface, `IEngineController` (`Engine/src/Control/EngineController.cs`). `DefaultEngineController<TMessage>` is generic over the host's message DTO and `abstract` (its message-field members are `protected abstract`), so it is never auto-registered by convention scanning (which skips open generic and abstract types) — a host must always define its own subclass and supply it as the required `TEngineController` generic type argument to `EngineApplication.Start<TEngineController>(args, windowIconUri, configureServices)`, which registers it automatically (`services.AddSingleton<IEngineController, TEngineController>()`) before invoking the optional `configureServices` callback; a host bypassing `EngineApplication` and composing its own `IHostBuilder` must register it explicitly instead (`services.AddSingleton<IEngineController, MyEngineController>();`). Never read environment variables anywhere in the codebase (Engine or Sample) — all behavior is controlled either through `config.json` or `IEngineController`; hardcoded paths inside Engine are likewise disallowed — go through it. An `IEngineController` implementation (a host's `DefaultEngineController<TMessage>` subclass) describes **non-config-file behavior only** — it must never read `EngineConfig` itself. `IEngineController` is for configuration and rules only — a component that provides genuine real-world/OS-level behavior (e.g. audio playback, printer discovery/driving, external drive discovery) that is not already one of `IEngineController`'s existing members is not a control interface: it belongs outside `Engine/src/Control/`, grouped with any other such real-behavior components in `Engine/src/Devices/` (avoid a separate single-file folder per component — group them together, since they share the same "OS-level integration" role), is `internal` unless a `public` type needs it as a constructor dependency (C# doesn't allow a less-accessible parameter type on a `public` member) — Engine always provides the real implementation directly; there is nothing for a host to sensibly swap — and is not documented as part of `IEngineController` in `Docs/Control.md`/`Docs/Configuration.md` even if it's still described there for completeness (e.g. `IAlertSoundPlayer`, `IPrintDriver`, `IExternalDriveProvider`, all in `Engine/src/Devices/`). Where a `config.json` field exists for a member, `EngineConfig` overrides are instead applied as a separate, Engine-owned decorator (`ConfiguredEngineController`) registered by `EngineExtensions.UseEngineConfigOverrides()` (call it last, after every other `ConfigureServices` call, so it sees whichever implementation ends up registered) — see `Docs/Control.md` for the full mechanism. Only override the members where Sample genuinely has distinct, non-config-file behavior worth demonstrating, via one `SampleEngineController : DefaultEngineController<SampleMessage>` in `Sample/src/SampleEngineController.cs` (registered in `Sample/src/Program.cs`) — do not add overrides just to exist beyond the required message-field mapping; see `Docs/Control.md` for the current set and why each one is or isn't overridden. Never let `SampleEngineController` change the app data path's default runtime behavior — that has caused real data loss in this project before. Whether `config.json` is read at all is itself gated by `IEngineController.ConfigFileEnabled` (disabled by default; `SampleEngineController` overrides it to `true`) — see `Docs/Control.md`.
- **Async all the way**: all I/O is async. Avoid `Task.Result` and `.GetAwaiter().GetResult()` except at the top-level host startup where a synchronization context deadlock is explicitly being avoided (and that case is already present in `EngineApp.axaml.cs`). Do not append `Async` to method names — name methods by what they do, not how they do it (`Load`, not `LoadAsync`). Name `CancellationToken` parameters `cancellation` (not `ct` or `cancellationToken`); framework-required overrides (e.g. `IHostedService.StartAsync(CancellationToken cancellationToken)`) are the only exception.
- **Events for cross-layer communication**: services expose C# events; ViewModels subscribe. Do not call ViewModel methods from services.
- **Repository pattern**: all LiteDB access goes through a repository. No direct collection access outside `LiteDbContext` and the repository classes.
- **Thread safety**: use `SemaphoreSlim(1,1)` for async-compatible locking, `ConcurrentDictionary` for shared maps, `lock` for short synchronous critical sections. Named Mutex for cross-process serialization.

- **UI layer isolation within Engine**: ViewModels and services still use primitive types and custom interfaces rather than Avalonia types, even though Views, Themes, and converters (the actual Avalonia dependency) live in the same assembly under `Engine/src/Views/`, `Engine/src/Themes/`, and the `*Converter` classes in `Engine/src/ViewModels/`. All View code-behind classes and Avalonia-type-dependent converters carry `[ExcludeFromCodeCoverage]`, since `Tests` cannot meaningfully exercise Avalonia UI. When running in Client mode, call `builder.UseEngine(EngineMode.Client).UseEngineUi()` — `UseEngineUi()` (in `EngineUiExtensions`) registers `MainWindow` and overrides `IBodyDocumentFactory` with `TextDocumentBodyDocumentFactory` so drafts receive a live AvaloniaEdit `TextDocument`. In tests and Headless mode, `IBodyDocumentFactory` resolves to the Engine default `BodyDocumentFactory` → `StringBodyDocument`.
- **Interface co-location**: every non-DTO class has a corresponding `IThing` interface declared in the same file (`Thing.cs`). Constructor and property injection always uses the interface type, never the concrete class. The Engine DI container auto-registers `IThing → Thing` pairs by convention (see `AddConventionSingletons` in `EngineExtensions.cs`); explicit registrations in `UseEngine` take precedence via `TryAddSingleton`. Excluded from the scanner: `Engine.ViewModels.Entries` classes (constructed per-entry with entity arguments, not DI-resolved).
- **Byte spans over arrays**: use `ReadOnlyMemory<byte>` / `ReadOnlySpan<byte>` for payload and data-chunk parameters and return types at method boundaries; use `Memory<byte>` when the callee needs to write. Reserve `byte[]` for serialization DTOs (e.g. protobuf `[ProtoMember]` fields) and for internal read buffers allocated with `new byte[n]`.
- **No `var`**: always declare the explicit type on local variables. Use C# 9+ target-typed `new()` to avoid repetition when the type is already on the left-hand side (e.g. `SslStream ssl = new(...)`). For tuple deconstructions write the types inline: `(string id, bool ok) = GetResult()`.

### Tests
- Tests live in `Tests/src/`. Use xUnit.
- Do not mock the database — tests use a real `LiteDbContext` pointed at a temp directory (GUID-named under `%APPDATA%`). The `Dispose()` method deletes it.
- `TestAppDataPathProvider` provides the isolated path. Use it in any test that needs `LiteDbContext` or `UserService`.
- Do not add `#if DEBUG` guards. All features must work in Release configuration.
- For UI changes, run and exercise the application headlessly using `xvfb-run`. Always target a virtual display — never the real/physical display.

### Windows Compatibility
- File I/O that may be accessed by multiple processes must use `FileShare.ReadWrite` and a named Mutex. See `DailyFileLoggerProvider` for the pattern.
- Avoid `Environment.SpecialFolder` paths hardcoded as strings — use `IEngineController`.

## Packaging

- `Engine/Engine.csproj` is the only project that carries NuGet package metadata (`PackageId` = `BlueHeighliner.Comlink`, `Version`, description, etc.) and the only one published. `Sample` and `Tests` are never packaged.
- A Release build produces the package automatically:

  ```
  dotnet build Engine/Engine.csproj -c Release
  ```

  The resulting `.nupkg` lands in `Engine/bin/Release/`. `dotnet pack -c Release` works the same way if you only want the package without a full build.
- CI (`.github/workflows/csharp.yml`) builds and tests on every push/PR to `main`, and packs + publishes to GitHub Packages on a published GitHub Release or manual `workflow_dispatch`. On an actual GitHub Release (not a manual `workflow_dispatch`), the `.nupkg` is also attached directly to the release as a downloadable asset.
- **Single-file deployment**: `Engine/Engine.csproj` sets `<DebugType>embedded</DebugType>` for Release builds — deliberately, not an oversight. `Sample` publishes as a single-file self-contained deployment (`PublishSingleFile`/`SelfContained` in `Sample.csproj`); a `PublishSingleFile` bundle only packs the files the runtime actually needs to execute, and a separate portable `.pdb` for a referenced library like Engine is **not** one of them — it would be left behind as a loose file next to the single executable instead of being embedded in the bundle. Embedding debug symbols directly in `BlueHeighliner.Comlink.Engine.dll` avoids that, at the cost of `Engine`'s NuGet package having no meaningful separate symbol package (`IncludeSymbols`/`SymbolPackageFormat=snupkg` were deliberately left off the `PackageId` property group for this reason — a `.snupkg` built against an embedded-symbol DLL has no `.pdb` to include and is empty). Verify with `dotnet publish Sample/Sample.csproj -c Release -r <RID> -o <dir>` and confirm the output directory contains only the single executable (plus the harmless `BlueHeighliner.Comlink.Engine.xml` doc-comments file) — no stray `.pdb`.

## Versioning

- The version lives in one place — `Engine/Engine.csproj`'s `<Version>`, currently `0.1.0`. It is not tied to any automated versioning scheme (e.g. git tags, a CI-computed version) — bump it by hand before cutting a release.
- The version bump and the git tag used for the GitHub Release (which triggers the CI `publish` job) must match, or the published package will disagree with the release it's attached to.

## CLI Arguments

Engine supports `--config <path-to-config.json>` to load configuration from a JSON file. The `--config` argument is available in Debug builds and in Release builds that define the `ALLOW_CONFIG` compile-time constant.

```
Sample.exe --config <path-to-config.json>
```

If `--config` is omitted, all settings use their defaults (Client mode, default ports, system app data folder). An empty config file behaves identically to omitting the argument entirely. Reading `--config` at all is itself gated by `IEngineController.ConfigFileEnabled` (`Docs/Control.md`), disabled by default; Sample overrides it to `true` so its own `--config` examples below work, but a host that leaves the Engine default in place gets `EngineConfig` always using its defaults, regardless of `--config`.

### Config file schema

```json
{
  "HeadlessMode":        false,
  "UserName":            null,
  "PeerPort":            50021,
  "InterfacePort":       50020,
  "DataFolder":          null,
  "PeerCertificateName": null,
  "AlertText":           null,
  "AlarmSoundSeconds":   null,
  "QuickConfirmationEnabled": null,
  "ComposeAlertsEnabled": null,
  "MessageTagsEnabled": null,
  "MessageTagLabel": null,
  "PrintReceivedEnabled": null,
  "NodeRole": null,
  "ServerEndpoint": null,
  "ServerUsers": {},
  "Users": {
    "USER-A": { "IpAddress": "192.168.1.10", "Port": 7890 }
  },
  "UserGroups": {
    "OPS": ["USER-A", "USER-B"]
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `HeadlessMode` | bool | `false` | Run as a headless peer client instead of launching the GUI |
| `UserName` | string? | `null` | Debug user name override — skips `State.json` |
| `PeerPort` | int? | `null` | Peer TCP listen port (`null` = 50021) |
| `InterfacePort` | int? | `null` | Interface TCP listen port (`null` = 50020; always active, in every mode) |
| `DataFolder` | string? | `null` | Custom app data directory; defaults to `%APPDATA%\{AppName}`. A path starting with `@` is relative to that default (e.g. `@test/user` → `%APPDATA%\{AppName}\test\user`) |
| `PeerCertificateName` | string? | `null` | TLS cert subject name for peer auth. `null`/absent = auto (`USER-{userName}`, throws if missing); `"disable"` = no auth; explicit name = use that cert (throws if missing). See `Docs/Config.md`. |
| `AlertText` | string? | `null` | Alert box text in the title bar while alarming (`null` = `"ALERT"`) |
| `AlarmSoundSeconds` | double? | `null` | Seconds the alarm sound plays before auto-stopping, reset on each new alert (`null` = 30) |
| `QuickConfirmationEnabled` | bool? | `null` | Whether click/Space/Enter quick-confirms the latest pending alert (`null` = `true`) |
| `ComposeAlertsEnabled` | bool? | `null` | Whether the draft editor's alert checkbox is shown (`null` = `true`); disabling only affects local origination, not receiving peer alerts |
| `MessageTagsEnabled` | bool? | `null` | Whether message tags are shown anywhere in the UI (`null` = `true`) |
| `MessageTagLabel` | string? | `null` | Label for the tag input's watermark in the draft editor (`null` = `"Tag"`) |
| `PrintReceivedEnabled` | bool? | `null` | Whether the print manager's "print received" toggle starts enabled (`null` = `false`) |
| `NodeRole` | string? | `null` | Networking topology: `"Peer"`, `"Client"`, or `"Server"` (`null`/unrecognized = `"Peer"`). See `Docs/Peer.md#node-roles`. |
| `ServerEndpoint` | object? | `null` | Server endpoint a `"Client"`-role instance connects through: `{ IpAddress, Port }`. Required when `NodeRole` is `"Client"`. |
| `ServerUsers` | object | `{}` | Full server-user-map topology for a `"Server"`-role instance: map of server user name → `{ IpAddress, Port, ChildClients }`, describing every server in the cluster. Required when `NodeRole` is `"Server"`. |
| `Users` | object | `{}` | Map of user name → `{ IpAddress, Port }` — overrides or defines user endpoints |
| `UserGroups` | object | `{}` | Map of group name → member list (user or group names); groups are addressable destinations and are expanded recursively on send |

JSON property names are PascalCase (matching C# property names). Deserialization is case-insensitive. Missing fields use their defaults. If `--config` points to a non-existent or unreadable file, the process throws at startup.
