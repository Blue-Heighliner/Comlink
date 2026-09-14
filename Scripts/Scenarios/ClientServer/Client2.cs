#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Manual test scenario: one server with two client children (Client1, Client2). Both clients form a
// single long-term connection to the server; the server routes messages between them and does not
// expose an inbox/outbox/notes/drafts UI of its own. Run via Run.task, or standalone (after Build.cs) in
// its own terminal alongside Server.cs and Client1.cs, from the repo root:
//   dotnet run Scripts/Scenarios/ClientServer/Client2.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--no-build", "--project", "Sample/Sample.csproj", "--", "--config", "Scripts/Scenarios/ClientServer/Client2.json")).Verify();
