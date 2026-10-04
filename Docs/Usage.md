# Usage

Runnable examples of `IEngineConfiguration` and `Engine.Start<T>` in different situations.

## Minimal host

The smallest possible host: a frame DTO, a configuration mapping it, and the entry point.

```csharp
public sealed class MyFrame
{
    public string Id { get; set; } = "";
    public string FromUser { get; set; } = "";
    public string Body { get; set; } = "";
    public List<(string Name, AddressType Type)> Addresses { get; set; } = [];
    public DateTime SentAt { get; set; }
    public bool IsMessage { get; set; }
    public bool IsAlert { get; set; }
    public int Priority { get; set; }
    public string Tag { get; set; } = "";
    public int? SecurityLevel { get; set; }
}

public sealed class MyEngineConfiguration : IEngineConfiguration
{
    public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPriority, MySecurityLevel>()
        .Priorities().Priority(MyPriority.Normal)
        .Frames()
            .Message<MyMessageHandler>()
            .Retrieval<MyRetrievalHandler>()
            .ReadReceipt<MyReadReceiptHandler>()
            .ReceiveReceipt<MyReceiveReceiptHandler>();
}

await Engine.Start<MyEngineConfiguration>(args);
```

A common field whose type already matches is mapped by naming the property (`m => m.Id`), which builds the setter for you; when the type differs (a host's own recipient shape for the addresses, or a packet's data) the getter and setter are given explicitly, as `Addresses` is above. Each kind of frame is handled by a class implementing the matching handler interface; for example:

```csharp
public sealed class MyMessageHandler : IMessageHandler<MyFrame, MyPriority, MySecurityLevel>
{
    public bool IsValid(MyFrame frame) => frame.IsMessage;
    public MyFrame Create(MessageCreateContext<MyPriority, MySecurityLevel> context) => new() { IsMessage = true, SentAt = context.SentAt, Body = context.Body, IsAlert = context.IsAlert, Priority = (int)context.Priority, Tag = context.Tag, SecurityLevel = (int?)context.SecurityLevel };
    public DateTime GetSentAt(MyFrame frame) => frame.SentAt;
    public string GetBody(MyFrame frame) => frame.Body;
    public bool GetIsAlert(MyFrame frame) => frame.IsAlert;
    public MyPriority GetPriority(MyFrame frame) => (MyPriority)frame.Priority;
    public string GetTag(MyFrame frame) => frame.Tag;
    public MySecurityLevel? GetSecurityLevel(MyFrame frame) => (MySecurityLevel?)frame.SecurityLevel;
}
```

The retrieval and receipt handlers follow the same shape (`Create`, `IsValid`, and getters for their own fields). `Addresses` also has an overload taking `(string Name, AddressType Type, string Information)` tuples, for a host whose recipient shape carries custom per-address instructions (e.g. `OMAHA - Deliver to Eastside Office`); `Information` is optional and defaults to an empty string when the two-tuple overload above is used instead. The frame type also needs `[ProtoContract]`/`[ProtoMember]` attributes for the default network serializer.
By default this runs the Avalonia desktop UI, with command-line overrides disallowed (`CommandLineOverrides` is off
unless stated) and no window icon (the display handler's `Icon` is the operating system's unless stated).

## Stating a single behavior

A host only states what it needs distinct behavior for; every other setting keeps the engine's default.

```csharp
public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPriority, MySecurityLevel>()
    .Display<MyDisplayHandler>()
    .Frames() /* ...required handlers from above... */;
```

## The network configuration file

A host describes its whole network in one JSON file (see [Config.md](Components/Config.md) for the schema): every user
with their role, listen ports, outgoing connections, security level and node settings, plus groups and the trusted
certificate authority. The engine always reads `Config.json` from the working directory, and the user the process runs
as from `User.json` there; nothing about a user is stated in code. Stating `CommandLineOverrides(true)` additionally
lets `--config` name another file and `--user` name the user.

```csharp
public void Configure(IEngineBuilder engine) => engine.Types<MyFrame, MyPriority, MySecurityLevel>()
    .Frames() /* ...required handlers... */
    .CommandLineOverrides(true);
```

## Running headless

Set `Headless` on a user's entry in the network configuration file and run as that
user (`User.json`, or `--user` when overrides are allowed), to run with no UI. `Engine.Start` is called exactly the same
way: the same configuration drives both modes.
