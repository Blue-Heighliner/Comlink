# Usage

Runnable examples of `IEngineConfiguration` and `Engine.Start<T>` in different situations.

## Minimal host

The smallest possible host: a message DTO, a configuration mapping it, and the entry point.

```csharp
public sealed class MyMessage
{
    public string Id { get; set; } = "";
    public string FromUser { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public List<(string Name, AddressType Type)> Addresses { get; set; } = [];
    public DateTime SentAt { get; set; }
    public string ConfirmationId { get; set; } = "";
    public bool IsAlert { get; set; }
    public int Priority { get; set; }
    public string Tag { get; set; } = "";
}

public sealed class MyEngineConfiguration : IEngineConfiguration
{
    public IEngineBuilder Configure(IEngineBuilder engine) => engine
        .Message<MyMessage>(message => message
            .Id(m => m.Id)
            .Sender(m => m.FromUser)
            .Subject(m => m.Subject)
            .Body(m => m.Body)
            .Addresses(m => m.Addresses, (m, value) => m.Addresses = [.. value])
            .SentAt(m => m.SentAt)
            .ConfirmationId(m => m.ConfirmationId)
            .Retrieval(r => r.IsRequest(m => m.IsRetrieval).From(m => m.RetrievalFrom).To(m => m.RetrievalTo)
                .Authors(m => m.RetrievalAuthors, (m, v) => m.RetrievalAuthors = [.. v])
                .Destinations(m => m.RetrievalDestinations, (m, v) => m.RetrievalDestinations = [.. v])
                .Ids(m => m.RetrievalIds, (m, v) => m.RetrievalIds = [.. v]))
            .IsAlert(m => m.IsAlert)
            .Priority(m => m.Priority)
            .Tag(m => m.Tag));
}

await Engine.Start<MyEngineConfiguration>(args);
```

A field whose type already matches is mapped by naming the property (`m => m.Id`), which builds the setter for you; when the type differs (a host's own recipient shape for the addresses, or a packet's data) the getter and setter are given explicitly, as `Addresses` is above. `Addresses` also has an overload taking `(string Name, AddressType Type, string Information)` tuples, for a host whose recipient shape carries custom per-address instructions (e.g. `OMAHA - Deliver to Eastside Office`); `Information` is optional and defaults to an empty string when the two-tuple overload above is used instead. The message type also needs `[ProtoContract]`/`[ProtoMember]` attributes for the default network serializer.
By default this runs the Avalonia desktop UI, with command-line overrides disallowed (`CommandLineOverrides` is off
unless stated) and no window icon (`WindowIcon` is the operating system's unless stated).

## Stating a single behavior

A host only states what it needs distinct behavior for; every other setting keeps the engine's default.

```csharp
public IEngineBuilder Configure(IEngineBuilder engine) => engine
    .Message<MyMessage>(/* ...required mapping from above... */)
    .HomeText("Select a folder and entry to get started.")
    .WindowIcon("avares://MyApp/Assets/icon.png");
```

## The network configuration file

A host describes its whole network in one JSON file (see [Config.md](Components/Config.md) for the schema): every user
with their role, listen ports, outgoing connections, security level and node settings, plus groups and the trusted
certificate authority. The engine always reads `Config.json` from the working directory, and the user the process runs
as from `User.json` there; nothing about a user is stated in code. Stating `CommandLineOverrides(true)` additionally
lets `--config` name another file and `--user` name the user.

```csharp
public IEngineBuilder Configure(IEngineBuilder engine) => engine
    .Message<MyMessage>(/* ...required mapping... */)
    .CommandLineOverrides(true);
```

## Running headless

Set `Headless` on a user's entry in the network configuration file and run as that
user (`User.json`, or `--user` when overrides are allowed), to run with no UI. `Engine.Start` is called exactly the same
way: the same configuration drives both modes.
