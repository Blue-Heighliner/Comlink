# Project

Documentation for this repository's own tooling and workflow.

## Scripts

`Scripts/` holds file-based C# apps (`dotnet run Scripts/<Name>.cs`) for automated repository
actions, using the `Markwardt.ScriptUtilities` package for `Script.Run`/`Script.Log`/`Script.Delete`/etc.
helpers. Chosen over shell/task scripts because control flow (polling, conditional rollback) stays
identical on any OS with `dotnet` installed, unlike a shell script, which doesn't share syntax
between `sh` and `cmd.exe`.

- `Scripts/Test.cs` - runs the test suite with coverage collection and prints a summary.
- `Scripts/Verify.cs` - applies formatting fixes and regenerates the coverage badge. Run before
  every commit and again before `Publish.cs`; CI's own formatting check only verifies, it never
  fixes or commits.
- `Scripts/Publish.cs` - cuts a release (see Publishing below). A manual, human-only action, never
  run by CI.
- `Scripts/Run.cs` - runs `Sample` locally via `dotnet run`.
- `Scripts/Scenarios/Build.cs` - builds `Sample` once, shared by every scenario so concurrently
  launched roles never race each other rebuilding the same output.
- `Scripts/Scenarios/<Scenario>/<Role>.cs` - runs one role of a manual multi-node test scenario
  against `Sample` with `--no-build` (`ClientServer`: `Server`/`Client1`/`Client2`; `Peer`:
  `Peer1`/`Peer2`; `ServerCluster`: `Server1`/`Server2`/`Client1`/`Client2`), passing the matching
  `<Role>.json` in the same folder as `--config`. Each role simulates one installation talking to the
  others over loopback; `Scripts/Scenarios/Root.cer` is the shared certificate authority signing
  every role's `.pfx` identity in every scenario.
- `Scripts/Scenarios/<Scenario>/Run.task` - runs `Build.cs`, then every role `.cs` script of that
  scenario concurrently against the shared build, as ordered batches separated by a `-` wait marker
  (AutoDev's `.task` file format — see `TaskFileParser` in `Auto-Dev`).

## Publishing

Run `Scripts/Publish.cs` locally to cut a release:

1. It prompts for the version to publish (e.g. `1.2.3`) — `Core/Core.csproj` carries no `<Version>` of its
   own, so this is what actually gets built and published.
2. It creates the GitHub Release (and its underlying tag) for that version locally via `gh release
   create` — done locally because a repo ruleset blocks the default `GITHUB_TOKEN` from creating tags.
3. It dispatches `build.yml`'s `workflow_dispatch` trigger with the version as input. The workflow
   verifies the dispatcher has Admin permission on the repo, refuses to run from anything but `main`,
   packs `Core/Core.csproj`, pushes the package to GitHub Packages and nuget.org, and uploads the
   `.nupkg` as a release asset (no `.snupkg` — see the `DebugType` comment in `Core/Core.csproj` for why).
4. It waits for that run to finish, rolling the release/tag back if the workflow fails, so a failed
   publish never leaves one behind. If it can't confirm the run happened at all, it leaves the release
   in place instead, rather than risk deleting one that's still running.

## Workflows

- `.github/workflows/build.yml` — builds, verifies formatting (`dotnet format --verify-no-changes`,
  never applies fixes), and runs tests on every push/PR to `main`; its `publish` job (see Publishing
  above) only runs on `workflow_dispatch`, gated on the dispatcher having Admin permission on the repo
  and the run being on `main`, and pushes the package to both GitHub Packages and nuget.org.
- `.github/workflows/codeql.yml` — CodeQL security analysis on push/PR to `main` and a weekly schedule.
