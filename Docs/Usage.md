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
By default this runs the Avalonia desktop UI, with no `config.json` read (`ConfigFile` is off
unless stated) and no window icon (`WindowIcon` is the operating system's unless stated).

## Stating a single behavior

A host only states what it needs distinct behavior for; every other setting keeps the engine's default.

```csharp
public IEngineBuilder Configure(IEngineBuilder engine) => engine
    .Message<MyMessage>(/* ...required mapping from above... */)
    .HomeText("Select a folder and entry to get started.")
    .WindowIcon(new Uri("avares://MyApp/Assets/icon.png"));
```

## Enabling config.json

Stating `ConfigFile` lets a host be configured via a `config.json` file (see
`EngineConfigFile` for the full set of fields) without any other code change: every setting with a
corresponding config field is overridden automatically once enabled.

```csharp
public IEngineBuilder Configure(IEngineBuilder engine) => engine
    .Message<MyMessage>(/* ...required mapping... */)
    .ConfigFile();
```

## Injecting services into the configuration

The configuration is constructed through dependency injection, from a container holding logging plus whatever the host
registers, so its constructor can take services:

```csharp
public sealed class MyEngineConfiguration(ILogger<MyEngineConfiguration> logger, IUserDirectory directory) : IEngineConfiguration
{
    public IEngineBuilder Configure(IEngineBuilder engine)
    {
        logger.LogInformation("Configuring the engine");
        return engine
            .Message<MyMessage>(/* ...required mapping... */)
            .Users([.. directory.GetNames()]);
    }
}

await Engine.Start<MyEngineConfiguration>(args, services => services.AddSingleton<IUserDirectory, UserDirectory>());
```

## Interacting with a running engine

Once `Engine.Start` has started the host, `IServiceConnection` is the surface a UI or headless
consumer uses to send messages and observe delivery. A host reaches it through a service it registers with
`configureServices`, which is applied to the running engine as well:

```csharp
await Engine.Start<MyEngineConfiguration>(args, services => services.AddHostedService<Greeter>());

public sealed class Greeter(IServiceConnection connection) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await connection.Connect(stoppingToken);

        connection.MessageReceived += async received =>
        {
            Console.WriteLine($"Received: {received.Subject}");
            await Task.CompletedTask;
        };

        await connection.SendMessage("Hello", "Body text", [new AddressRequest { UserName = "alice", Type = "To" }], cancellation: stoppingToken);
    }
}
```

## Running headless

Set `HeadlessMode` in `config.json` (requires `ConfigFile`), or pass `--config` pointing at
a file with `"HeadlessMode": true`, to run with no UI. `Engine.Start` is called exactly the same
way: the same configuration drives both modes.
