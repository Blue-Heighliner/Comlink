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

### Patterns
- **DI first**: all external configuration is expressed as an interface in `Engine/src/Control/`. Register defaults with `TryAddSingleton` so hosts can override. Never read environment variables or hardcode paths inside Engine — go through a provider.
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
- `TestAppDataPathProvider` provides the isolated path. Use it in any test that needs `LiteDbContext` or `SiteService`.
- Do not add `#if DEBUG` guards. All features must work in Release configuration.
- For UI changes, run and exercise the application headlessly using `xvfb-run`. Always target a virtual display — never the real/physical display.

### Windows Compatibility
- File I/O that may be accessed by multiple processes must use `FileShare.ReadWrite` and a named Mutex. See `DailyFileLoggerProvider` for the pattern.
- Avoid `Environment.SpecialFolder` paths hardcoded as strings — use `IAppDataPathProvider`.

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

If `--config` is omitted, all settings use their defaults (Client mode, default ports, system app data folder). An empty config file behaves identically to omitting the argument entirely.

### Config file schema

```json
{
  "HeadlessMode":        false,
  "SiteName":            null,
  "PeerPort":            50021,
  "InterfacePort":       50020,
  "DataFolder":          null,
  "PeerCertificateName": null,
  "Sites": {
    "SITE-A": { "IpAddress": "192.168.1.10", "Port": 7890 }
  },
  "SiteGroups": {
    "OPS": ["SITE-A", "SITE-B"]
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `HeadlessMode` | bool | `false` | Run as a headless peer client instead of launching the GUI |
| `SiteName` | string? | `null` | Debug site name override — skips `State.json` |
| `PeerPort` | int? | `null` | Peer TCP listen port (`null` = 50021) |
| `InterfacePort` | int? | `null` | Interface TCP listen port (`null` = 50020; always active, in every mode) |
| `DataFolder` | string? | `null` | Custom app data directory; defaults to `%APPDATA%\{AppName}`. A path starting with `@` is relative to that default (e.g. `@test/site` → `%APPDATA%\{AppName}\test\site`) |
| `PeerCertificateName` | string? | `null` | TLS cert subject name for peer auth. `null`/absent = auto (`SITE-{siteName}`, throws if missing); `"disable"` = no auth; explicit name = use that cert (throws if missing). See `Docs/Config.md`. |
| `Sites` | object | `{}` | Map of site name → `{ IpAddress, Port }` — overrides or defines site endpoints |
| `SiteGroups` | object | `{}` | Map of group name → member list (site or group names); groups are addressable destinations and are expanded recursively on send |

JSON property names are PascalCase (matching C# property names). Deserialization is case-insensitive. Missing fields use their defaults. If `--config` points to a non-existent or unreadable file, the process throws at startup.
