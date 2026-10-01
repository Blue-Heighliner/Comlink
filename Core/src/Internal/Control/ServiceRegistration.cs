namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// A service a host stated by type (a processor or a serializer), to be instantiated through dependency injection once the engine runs: the instance the
/// container has registered for the type, or else one it constructs from the container's services.
/// </summary>
/// <typeparam name="TService">The engine's view of the service.</typeparam>
/// <param name="Create">Instantiates the service from the running engine's container, or from one with no services when <see langword="null"/>.</param>
internal sealed record ServiceRegistration<TService>(Func<IServiceProvider?, TService> Create) where TService : class
{
    /// <summary>Registers <paramref name="type"/>, presented to the engine through <paramref name="adapt"/>.</summary>
    /// <param name="type">The host's type.</param>
    /// <param name="adapt">Presents an instance of <paramref name="type"/> as <typeparamref name="TService"/>.</param>
    public static ServiceRegistration<TService> Of(Type type, Func<object, TService> adapt)
        => new(services =>
        {
            ServiceProvider? empty = services is null ? new ServiceCollection().BuildServiceProvider() : null;
            try { return adapt(ActivatorUtilities.GetServiceOrCreateInstance(services ?? empty!, type)); }
            finally { empty?.Dispose(); }
        });
}
