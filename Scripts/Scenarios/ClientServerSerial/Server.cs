#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Manual test scenario: one server with two clients, each reaching it over a different medium. Client1 is joined to the
// server by a single MicroGate serial link; Client2 connects over MSMT/IP like in the ClientServer scenario, so the server
// routes messages between a serial client and an IP client.
// The serial link runs on one machine: the client opens local serial port ttyUSB0 and the server opens local port ttyUSB1 (the two SyncLink USB devices),
// which are physically cabled to each other. A serial link carries no certificate, so each end names the user at the other
// end of its cable on its outgoing point in Config.json ("User"), and lists the two HDLC station addresses the other way
// round. The port names are the two devices on the development machine: edit them in Config.json to match the ports of the two MicroGate devices in use.
// The identity certificates are reused from the ClientServer scenario. Run via Run.task, or standalone (after Build.cs) in
// its own terminal alongside the other two, from the repo root:
//   dotnet run Scripts/Scenarios/ClientServerSerial/Server.cs

using Markwardt.ScriptUtilities;

(await Script.Run("dotnet", "run", "--no-build", "--project", "Sample/Sample.csproj", "--", "--config", "Scripts/Scenarios/ClientServerSerial/Config.json", "--user", "SERVER")).Verify();
