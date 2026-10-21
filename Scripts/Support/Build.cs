#:package Markwardt.ScriptUtilities
#:property TreatWarningsAsErrors=true

// Builds Sample once, shared by every scenario's role scripts (which pass --no-build to `dotnet run`
// and rely on this having already run) - avoids every concurrently-launched role rebuilding the same
// output and racing each other for the same lock files. Supports the scenario tasks in the folder above
// and is not run directly. File-based app (dotnet run) - run from the repo root, e.g.
//   dotnet run Scripts/Support/Build.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "build", "Sample/Sample.csproj")).Verify();
