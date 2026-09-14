#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Manual test scenario: default Peer-to-peer role (no NodeRole set). Launches two peer users, each with
// the other's endpoint pre-configured in "Users", so they can address each other directly. Run via
// Run.task, or standalone (after Build.cs) in its own terminal alongside Peer1.cs, from the repo root:
//   dotnet run Scripts/Scenarios/Peer/Peer2.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--no-build", "--project", "Sample/Sample.csproj", "--", "--config", "Scripts/Scenarios/Peer/Peer2.json")).Verify();
