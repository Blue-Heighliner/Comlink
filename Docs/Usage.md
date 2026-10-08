# Usage

Runnable examples of `IEngineConfiguration` and `Engine.Start<T>` in different situations.

## Minimal host

The smallest possible host: a frame DTO, a configuration, and the entry point. The engine is only transport, GUI and storage, so a host that wants messages to flow states a network processor that implements the protocol.

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
    public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPacket, MyPriority, MyMessageLevel, MyMessageAspect>()
        .Priorities().Priority(MyPriority.Normal)
        .Frames()
            .Processor<MyNetworkProcessor>();
}

await Engine.Start<MyEngineConfiguration>(args);
```

The processor reacts to what happens and acts through the context it is handed. For example, a client that sends each message to its server and records each frame it receives:

```csharp
public sealed class MyNetworkProcessor : INetworkProcessor<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect>
{
    public async Task OnSent(INetworkSentContext<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect> context)
    {
        Message<MyPriority, MyMessageLevel, MyMessageAspect> message = context.Message;
        MyFrame frame = new() { Id = message.Id, FromUser = message.FromUser, Body = message.Body, To = [.. message.Addresses.Select(a => a.UserName)] };
        bool accepted = await context.Send(context.CurrentUser.Parent!.User, message.Priority, frame);
        foreach (string user in frame.To)
        {
            await context.SetSentStatus(message.Id, user, accepted ? DestinationStatus.Sent : DestinationStatus.Failed);
        }
    }

    public Task OnReceived(INetworkReceivedContext<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect> context)
        => context.ReceiveMessage(new Message<MyPriority, MyMessageLevel, MyMessageAspect>
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

Receipts, routing through servers, retrieval and the network indicator are written the same way, in the processor; `Sample/src/Components/NetworkProcessor.cs` is a complete one. The frame type also needs `[ProtoContract]`/`[ProtoMember]` attributes for the default network serializer.
By default this runs the Avalonia desktop UI, with command-line overrides disallowed (`CommandLineOverrides` is off
unless stated) and no window icon (the display handler's `Icon` is the operating system's unless stated).

## Stating a single behavior

A host only states what it needs distinct behavior for; every other setting keeps the engine's default.

```csharp
public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect>()
    .Display<MyDisplayHandler>()
    .Frames() /* ...the processor from above... */;
```

## The network configuration file

A host describes its whole network in one JSON file (see [Config.md](Components/Config.md) for the schema): every user
with their role, listen ports, outgoing connections, message level and node settings, plus groups and the trusted
certificate authority. The engine always reads `Config.json` from the working directory; nothing about a user is stated in
code, and the user installs by name on the install screen. Stating `CommandLineOverrides(true)` additionally
lets `--config` name another file and `--user` name the user, who is checked like an installed one.

```csharp
public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPriority, MyMessageLevel, MyMessageAspect>()
    .Frames() /* ...the processor... */
    .CommandLineOverrides(true);
```

## Running headless

Set `Headless` on a user's entry in the network configuration file and run as that
user (installed, or `--user` when overrides are allowed), to run with no UI. `Engine.Start` is called exactly the same
way: the same configuration drives both modes.
