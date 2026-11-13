namespace BlueHeighliner.Comlink.Tests.Unit.Public.Testing;

/// <summary>Unit tests for <see cref="EngineConfigurationTesting"/>: a complete configuration passes, and an incomplete one or one naming a component that cannot be built fails the way the engine would.</summary>
public sealed class EngineConfigurationTestingTests
{
    private interface IMissing
    {
    }

    private sealed class Present : IMissing
    {
    }

    private sealed class ActingDeleteHandler(INetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect> network, IUserCatalog<TestLevel, NoProfile> users, IDataStore store, IDebugMode debug) : IDeleteHandler
    {
        public bool CanDelete(IDeleteContext context) => network is not null && users.Current is not null && store is not null && !debug.IsEnabled;
    }

    private sealed class NeedsMissingDeleteHandler(IMissing missing) : IDeleteHandler
    {
        public bool CanDelete(IDeleteContext context) => missing is null;
    }

    private sealed class NoTypes : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
        {
        }
    }

    private sealed class WithHandler<THandler> : IEngineConfiguration where THandler : class, IDeleteHandler
    {
        public void Configure(IEngineBuilder engine) => new TestEngineConfiguration().Apply(engine).Deletes<THandler>();
    }

    /// <summary>A complete configuration passes with nothing registered.</summary>
    [Fact]
    public Task Verify_CompleteConfiguration_Passes() => EngineConfigurationTesting.Verify<TestEngineConfiguration>();

    /// <summary>A handler that asks for the services the engine supplies is built, with the fakes standing in for them.</summary>
    [Fact]
    public Task Verify_HandlerAskingForEngineServices_IsBuiltWithFakes() => EngineConfigurationTesting.Verify<WithHandler<ActingDeleteHandler>>();

    /// <summary>A configuration that never states its types fails with the engine's own message.</summary>
    [Fact]
    public async Task Verify_IncompleteConfiguration_Throws()
    {
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => EngineConfigurationTesting.Verify<NoTypes>());

        Assert.Contains("Types", error.Message);
    }

    /// <summary>A stated component that asks for a service nothing provides fails, and registering the service makes it pass.</summary>
    [Fact]
    public async Task Verify_ComponentWithAMissingDependency_ThrowsUntilItIsRegistered()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => EngineConfigurationTesting.Verify<WithHandler<NeedsMissingDeleteHandler>>());

        await EngineConfigurationTesting.Verify<WithHandler<NeedsMissingDeleteHandler>>(services => services.AddSingleton<IMissing, Present>());
    }
}
