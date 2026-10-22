#:package Markwardt.ScriptUtilities
#:property TreatWarningsAsErrors=true

// Manual test scenario: a two-server cluster (Server1, Server2), each with one Client child (Client1
// under Server1, Client2 under Server2). Both servers form a long-term connection to each other in
// addition to hosting their own child, so each server's connections table shows one row for the other
// server. Run via Scripts/RunServerCluster.task, or standalone (after Support/Build.cs) in its own terminal alongside Server1.cs,
// Server2.cs, and Client1.cs, from the repo root:
//   dotnet run Scripts/Support/ServerCluster/Client2.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--no-build", "--project", "Sample/Sample.csproj", "--", "--data", "Scripts/Support/ServerCluster/Data/CLIENT2", "--config", "Scripts/Support/ServerCluster/Config.json", "--authority", "Scripts/Support/Root.cer", "--certificate", "Scripts/Support/ServerCluster/CLIENT2.pfx")).Verify();
