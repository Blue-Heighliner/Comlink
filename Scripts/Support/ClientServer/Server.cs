#:package Markwardt.ScriptUtilities
#:property TreatWarningsAsErrors=true

// Manual test scenario: one server with two client children (Client1, Client2). Both clients form a
// single long-term connection to the server; the server routes messages between them and does not
// expose an inbox/outbox/notes/drafts UI of its own. Run via Scripts/RunClientServer.task, or standalone (after Support/Build.cs) in
// its own terminal alongside Client1.cs and Client2.cs, from the repo root:
//   dotnet run Scripts/Support/ClientServer/Server.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--no-build", "--project", "Sample/Sample.csproj", "--", "--config", "Scripts/Support/ClientServer/Config.json", "--authority", "Scripts/Support/Root.cer", "--certificate", "Scripts/Support/ClientServer/SERVER.pfx")).Verify();
