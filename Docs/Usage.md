# Usage

Runnable examples of `IEngineController` and `Engine.Start` in different situations.

## Minimal host

The smallest possible host: a message DTO, a controller implementing just the required
message-field mapping, and the entry point.

```csharp
public sealed class MyMessage
{
    public string Id { get; set; } = "";
    public string FromUser { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public List<MessageAddress> Addresses { get; set; } = [];
}

public sealed class MyEngineController(ICurrentUserProvider currentUserProvider)
    : DefaultEngineController<MyMessage>(currentUserProvider)
{
    protected override string GetMessageId(MyMessage message) => message.Id;
    protected override void SetMessageId(MyMessage message, string value) => message.Id = value;
    protected override string GetFromUser(MyMessage message) => message.FromUser;
    protected override void SetFromUser(MyMessage message, string value) => message.FromUser = value;
    protected override string GetSubject(MyMessage message) => message.Subject;
    protected override void SetSubject(MyMessage message, string value) => message.Subject = value;
    protected override string GetBody(MyMessage message) => message.Body;
    protected override void SetBody(MyMessage message, string value) => message.Body = value;
    protected override List<MessageAddress> GetAddresses(MyMessage message) => message.Addresses;
    protected override void SetAddresses(MyMessage message, List<MessageAddress> value) => message.Addresses = value;
}

await Engine.Start(args, services => services.AddSingleton<IEngineController, MyEngineController>());
```

By default this runs the Avalonia desktop UI, with no `config.json` read (`ConfigFileEnabled` is
`false` unless overridden) and no window icon (`WindowIconUri` is `null` unless overridden).

## Overriding a single behavior

A host only overrides the members it needs distinct behavior for; every other member keeps
`DefaultEngineController<TMessage>`'s own default.

```csharp
public sealed class MyEngineController(ICurrentUserProvider currentUserProvider)
    : DefaultEngineController<MyMessage>(currentUserProvider)
{
    // ...required message-field members from above...

    public override string HomeText => "Select a folder and entry to get started.";
    public override Uri? WindowIconUri => new Uri("avares://MyApp/Assets/icon.png");
}
```

## Enabling config.json

Overriding `ConfigFileEnabled` lets a host be configured via a `config.json` file (see
`EngineConfig` for the full set of fields) without any other code change — every member with a
corresponding config field is overridden automatically once enabled.

```csharp
public sealed class MyEngineController(ICurrentUserProvider currentUserProvider)
    : DefaultEngineController<MyMessage>(currentUserProvider)
{
    // ...required message-field members...

    public override bool ConfigFileEnabled => true;
}
```

## Interacting with a running engine

Once `Engine.Start` has started the host, `IServiceConnection` is the surface a UI or headless
consumer uses to send messages and observe delivery:

```csharp
IServiceConnection connection = provider.GetRequiredService<IServiceConnection>();
await connection.Connect();

connection.MessageReceived += async received =>
{
    Console.WriteLine($"Received: {received.Message.Subject}");
    await Task.CompletedTask;
};

await connection.SendMessage("Hello", "Body text", [new AddressRequest { UserName = "alice" }]);
```

## Running headless

Set `HeadlessMode` in `config.json` (requires `ConfigFileEnabled`), or pass `--config` pointing at
a file with `"HeadlessMode": true`, to run with no UI. `Engine.Start` is called exactly the same
way — the same `IEngineController` implementation drives both modes.
