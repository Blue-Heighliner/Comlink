namespace BlueHeighliner.Comlink.Tests.Unit.Internal;

/// <summary>Unit tests for how the engine registers the host's classes by convention (see <see cref="EngineExtensions"/>).</summary>
public sealed class EngineExtensionsTests
{
    private static readonly IReadOnlyList<Assembly> hostAssemblies = [typeof(ConventionService).Assembly];

    private sealed class Custom : IConventionService
    {
        public string Origin => "manual";
    }

    /// <summary>A class answers for the interface named for it without being registered, so <c>IMyService</c> resolves to <c>MyService</c>.</summary>
    [Fact]
    public void ConventionSingletons_ResolveTheClassNamedForTheInterface()
    {
        ServiceCollection services = new();
        services.AddHostConventionSingletons(hostAssemblies);

        IServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal("convention", provider.GetRequiredService<IConventionService>().Origin);
        Assert.Same(provider.GetRequiredService<IConventionService>(), provider.GetRequiredService<IConventionService>());
    }

    /// <summary>The engine's own scan finds its data store service: IDataStore resolves to the class named for it, built from the engine's controller.</summary>
    [Fact]
    public void ConventionSingletons_ResolveTheEnginesDataStore()
    {
        ServiceCollection services = new();
        services.AddSingleton(Mock.Of<IEngineController>());
        services.AddHostConventionSingletons([typeof(DataStore).Assembly]);

        IServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<DataStore>(provider.GetRequiredService<IDataStore>());
        Assert.Same(provider.GetRequiredService<IDataStore>(), provider.GetRequiredService<IDataStore>());
    }

    /// <summary>A registration the host made by hand wins over the convention, whether it was added before the scan or after it, and whether it was added or only tried.</summary>
    [Theory]
    [InlineData("before-add")]
    [InlineData("before-try")]
    [InlineData("after-add")]
    public void ManualRegistration_AlwaysTakesPrecedence(string how)
    {
        ServiceCollection services = new();
        if (how is "before-add")
        {
            services.AddSingleton<IConventionService, Custom>();
        }
        else if (how is "before-try")
        {
            services.TryAddSingleton<IConventionService, Custom>();
        }

        services.AddHostConventionSingletons(hostAssemblies);
        if (how is "after-add")
        {
            services.AddSingleton<IConventionService, Custom>();
        }

        Assert.Equal("manual", services.BuildServiceProvider().GetRequiredService<IConventionService>().Origin);
    }

    /// <summary>A service found by convention takes its dependencies as required properties too.</summary>
    [Fact]
    public void ConventionSingletons_FillRequiredProperties()
    {
        ServiceCollection services = new();
        services.AddHostConventionSingletons(hostAssemblies);

        IConventionConsumer consumer = services.BuildServiceProvider().GetRequiredService<IConventionConsumer>();

        Assert.Equal("convention", consumer.Service.Origin);
        Assert.Null(consumer.Optional);
    }

    /// <summary>The host's configuration is built by the engine itself, so it is never registered by convention.</summary>
    [Fact]
    public void ConventionSingletons_LeaveAConfigurationAlone()
        => Assert.DoesNotContain(new ServiceCollection().AddHostConventionSingletons(hostAssemblies), descriptor => descriptor.ServiceType == typeof(ITestScannedConfiguration));

    /// <summary>The host's own services in the running engine are registered after its manual ones, so the manual ones win there too.</summary>
    [Fact]
    public void UseHostServices_RegistersManualServicesThenConventions()
    {
        IHost host = Host.CreateDefaultBuilder().UseHostServices(services => services.AddSingleton<IConventionService, Custom>(), hostAssemblies).Build();

        Assert.Equal("manual", host.Services.GetRequiredService<IConventionService>().Origin);
        Assert.Equal("manual", host.Services.GetRequiredService<IConventionConsumer>().Service.Origin);
    }

    /// <summary>The configuration, built before the engine runs, can take the host's own services and the services the engine offers early, and what it is built from includes the conventions.</summary>
    [Fact]
    public async Task Build_Generic_OffersTheEnginesEarlyServicesAndTheConventions()
    {
        await using EngineBuilder builder = EngineBuilder.Build<ConventionalConfiguration>(null, hostAssemblies);

        Assert.True(builder.AreCommandLineOverridesAllowed);
    }

    private sealed class ConventionalConfiguration(IDebugMode debug, IDataPool pool, ILogger log, IConventionService service) : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => new TestEngineConfiguration().Apply(engine)
                .CommandLineOverrides(debug is not null && pool is not null && log is not null && service.Origin == "convention");
    }
}
