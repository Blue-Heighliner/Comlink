#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Manual test scenario: a two-server cluster (Server1, Server2), each with one Client child (Client1
// under Server1, Client2 under Server2). Both servers form a long-term connection to each other in
// addition to hosting their own child, so each server's connections table shows one row for the other
// server. Run via Run.task, or standalone (after Build.cs) in its own terminal alongside Server1.cs,
// Client1.cs, and Client2.cs, from the repo root:
//   dotnet run Scripts/Scenarios/ServerCluster/Server2.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--no-build", "--project", "Sample/Sample.csproj", "--", "--config", "Scripts/Scenarios/ServerCluster/Server2.json")).Verify();
