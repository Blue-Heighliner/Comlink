namespace BlueHeighliner.Comlink.Testing;

/// <summary>
/// Checks a host's <see cref="IEngineConfiguration"/> in a unit test without starting an engine: it is run against the real builder with the same checks as <see cref="Engine.Start{TConfiguration}"/>, and every component it states
/// (handlers, serializers, formats, heartbeat and handshake handlers, the configuration handler) is built through dependency injection from a container like the running engine's, so a missing constructor argument or
/// <see langword="required"/> property shows up. The services the engine would supply (<c>INetwork</c>, <c>IUserCatalog</c>, <c>IServiceConnection</c> and <c>IDataStore</c>) are the fakes of this namespace.
/// </summary>
public static class EngineConfigurationTesting
{
    /// <summary>
    /// Runs <typeparamref name="TConfiguration"/> and builds everything it states, throwing when the configuration is incomplete or contradicts itself, or when a component it names cannot be built.
    /// </summary>
    /// <typeparam name="TConfiguration">The host's configuration, constructed through dependency injection.</typeparam>
    /// <param name="configureServices">The services the host registers for the engine (the <c>configureServices</c> it gives <see cref="Engine.Start{TConfiguration}"/>), or <see langword="null"/> for none; the fakes of this namespace stand in for what the engine supplies.</param>
    /// <exception cref="InvalidOperationException">The configuration is incomplete or contradicts itself, or a stated component cannot be built, such as one that asks for a service nothing provides.</exception>
    public static async Task Verify<TConfiguration>(Action<IServiceCollection>? configureServices = null) where TConfiguration : class, IEngineConfiguration
    {
        Assembly[] assemblies = [typeof(TConfiguration).Assembly];
        await using EngineBuilder builder = EngineBuilder.Build<TConfiguration>(configureServices, assemblies);

        ServiceCollection services = new();
        services.AddLogging();
        configureServices?.Invoke(services);
        services.TryAddSingleton<IDebugMode, DebugMode>();
        services.TryAddSingleton<IDataPool, DataPool>();
        services.TryAddSingleton<ILogger, Logger>();
        services.TryAddSingleton<IDataStore>(new FakeDataStore());
        services.AddHostConventionSingletons(assemblies);
        builder.FakeTypedServices?.Invoke(services);
        await using ServiceProvider provider = services.BuildServiceProvider();

        EngineController controller = new(builder, new CurrentUserProvider { UserName = "TEST" }, null, provider);
        controller.Validate();
        _ = controller.FrameSerializer;
        _ = controller.PacketSerializer;
        _ = controller.FrameHandler;
        _ = controller.ExportFormats;
        _ = controller.ImportFormats;
        _ = controller.PacketHandshakeHandler;
        _ = controller.FrameHandshakeHandler;
        _ = controller.DraftTagRules;
        _ = controller.LogWidths;
        _ = controller.AlarmSoundDuration;
        _ = controller.PrintReceivedDefaultEnabled;
        _ = controller.CanDelete(FolderType.Drafts);
        _ = controller.ConfigurationHandler;
    }
}
