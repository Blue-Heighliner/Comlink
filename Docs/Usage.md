# Usage

Runnable examples of `IEngineConfiguration` and `Engine.Start<T>` in different situations.

## Minimal host

The smallest possible host: a frame DTO, a configuration, and the entry point. The engine is only transport, GUI and storage, so a host that wants messages to flow states a frame handler that implements the protocol.

```csharp
[ProtoContract]
public sealed class MyFrame
{
    [ProtoMember(1)] public string Id { get; set; } = "";
    [ProtoMember(2)] public string FromUser { get; set; } = "";
    [ProtoMember(3)] public string Body { get; set; } = "";
    [ProtoMember(4)] public List<string> To { get; set; } = [];
}

public sealed class MyEngineConfiguration : IEngineConfiguration
{
    public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPacket, MyPriority, MyLevel, MyAspect, NoProfile>()
        .Priority(MyPriority.Normal)
        .Certificates("Authority.cer", "Certificate.pfx")
        .Frames<MyFrameHandler>();
}

await Engine.Start<MyEngineConfiguration>(args);
```

`Certificates` names the authority certificate and the user's identity file; the node is the user that identity names (see below), so a host that omits it shows the error screen instead of the app unless both files come from the command line.

The handler reacts to what happens, through the context each method is handed, and acts through services it asks for with dependency injection. For example, a client that sends each message to its server and records each frame it receives:

```csharp
public sealed class MyFrameHandler(IUserCatalog<MyLevel, NoProfile> users, INetwork<MyFrame, MyPriority, MyLevel, MyAspect> network) : IFrameHandler<MyFrame, MyPriority, MyLevel, MyAspect>
{
    public async Task OnSent(INetworkSentContext<MyPriority, MyLevel, MyAspect> context)
    {
        Message<MyPriority, MyLevel, MyAspect> message = context.Message;
        MyFrame frame = new() { Id = message.Id, FromUser = message.FromUser, Body = message.Body, To = [.. message.Addresses.Select(a => a.UserName)] };
        bool accepted = await network.Send(users.CurrentUser.Parent!.User, message.Priority, frame);
        foreach (string user in frame.To)
        {
            await network.SetSentStatus(message.Id, user, accepted ? DestinationStatus.Sent : DestinationStatus.Failed);
        }
    }

    public Task OnReceived(INetworkReceivedContext<MyFrame> context)
        => network.ReceiveMessage(new Message<MyPriority, MyLevel, MyAspect>
        {
            Id = context.Frame.Id,
            FromUser = context.Frame.FromUser,
            Body = context.Frame.Body,
            Addresses = [.. context.Frame.To.Select(user => new MessageAddress { UserName = user, Type = AddressType.To })],
            SentAt = DateTime.UtcNow,
            Priority = MyPriority.Normal
        });
}
```

Receipts, routing through servers, retrieval and the network indicator are written the same way, in the handler; `Sample/src/Components/FrameHandler.cs` is a complete one. The frame type also needs `[ProtoContract]`/`[ProtoMember]` attributes for the default network serializer.
By default this runs the Avalonia desktop UI, with command-line overrides disallowed (`CommandLineOverrides` is off
unless stated) and no window icon (the display handler's `Icon` is the operating system's unless stated).

## Stating a single behavior

A host only states what it needs distinct behavior for; every other setting keeps the engine's default.

```csharp
public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPriority, MyLevel, MyAspect, NoProfile>()
    .Display<MyDisplayHandler>()
    .Frames<MyFrameHandler>();
```

## The network configuration file

A host describes its whole network in one JSON file (see [Config.md](Components/Config.md) for the schema): every user
with their role, listen ports, outgoing connections, level and node settings, plus groups. The engine always reads `Config.json` from the app's data folder (`%APPDATA%/{AppName}/`); nothing about a user is stated in
code, and nothing is installed: the node is the user its user certificate (`Certificates`) names, by default its first subject common name, which the file must list, and the
certificate must chain to the authority certificate. Stating `CommandLineOverrides(true)` additionally
lets `--config` name another file, `--authority` and `--certificate` name the certificate files, and `--data` name another data folder.

```csharp
public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPriority, MyLevel, MyAspect, NoProfile>()
    .Frames<MyFrameHandler>()
    .CommandLineOverrides(true);
```

## Running headless

Set `Headless` on a user's entry in the network configuration file and run as that
user (the one the certificate names), to run with no UI. `Engine.Start` is called exactly the same
way: the same configuration drives both modes. A headless node has no window to show a certificate problem in, so there `Engine.Start` throws an `InvalidOperationException` instead.
