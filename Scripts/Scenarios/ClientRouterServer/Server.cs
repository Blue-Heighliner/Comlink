#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Manual test scenario: two clients (Client1, Client2) behind one router, which connects to one server. The server routes everything the router forwards and stores messages; it has no inbox/outbox/notes/drafts UI of its own. Run via Run.task, or standalone (after Build.cs) in
// its own terminal alongside the other roles, from the repo root:
//   dotnet run Scripts/Scenarios/ClientRouterServer/Server.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--no-build", "--project", "Sample/Sample.csproj", "--", "--config", "Scripts/Scenarios/ClientRouterServer/Config.json", "--user", "SERVER")).Verify();
