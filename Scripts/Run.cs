#:package Markwardt.ScriptUtilities
#:property TreatWarningsAsErrors=true

// Runs the sample locally via `dotnet run`. File-based app (dotnet run) - run from the repo
// root, e.g. `dotnet run Scripts/Run.cs`.

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--project", "Sample/Sample.csproj")).Verify();
