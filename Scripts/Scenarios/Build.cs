#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Builds Sample once, shared by every scenario's role scripts (which pass --no-build to `dotnet run`
// and rely on this having already run) - avoids every concurrently-launched role rebuilding the same
// output and racing each other for the same lock files. Run from the repo root:
//   dotnet run Scripts/Scenarios/Build.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "build", "Sample/Sample.csproj")).Verify();
