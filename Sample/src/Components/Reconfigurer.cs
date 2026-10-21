namespace BlueHeighliner.Comlink.Sample;

/// <summary>Logs how many users and groups the network configuration file lists each time the configuration is loaded, at start-up and after every refresh, to show a reconfigurer taking its dependencies as required properties and writing to the engine's log.</summary>
public sealed class Reconfigurer : IReconfigurer
{
    /// <summary>Gets who is on the network, which the engine supplies.</summary>
    public required IUserCatalog Users { get; init; }

    /// <summary>Gets the engine's log, which the engine supplies.</summary>
    public required ILogger Logger { get; init; }

    /// <inheritdoc />
    public Task OnReconfigured()
    {
        Logger.Log(LogCategories.App, "SM1", $"The network has {Users.Users.Count} users and {Users.Groups.Count} groups");
        return Task.CompletedTask;
    }
}
