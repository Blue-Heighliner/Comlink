#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Manual test scenario: two clients (Client1, Client2) behind one router, which connects to one server. The router only forwards, unmodified, between the clients and the server (no storage or receipts), with the same UI as a server. Run via Run.task, or standalone (after Build.cs) in
// its own terminal alongside the other roles, from the repo root:
//   dotnet run Scripts/Scenarios/ClientRouterServer/Router.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--no-build", "--project", "Sample/Sample.csproj", "--", "--config", "Scripts/Scenarios/ClientRouterServer/Config.json", "--user", "ROUTER")).Verify();
