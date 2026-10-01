namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// A processor type a host stated, to be instantiated through dependency injection once the engine runs: the instance the container has registered for the
/// type, or else one it constructs from the container's services.
/// </summary>
/// <typeparam name="TProcessor">The engine's view of the processor.</typeparam>
/// <param name="Type">The host's processor type.</param>
/// <param name="Adapt">Presents an instance of <paramref name="Type"/> as <typeparamref name="TProcessor"/>.</param>
internal sealed record ProcessorRegistration<TProcessor>(Type Type, Func<object, TProcessor> Adapt) where TProcessor : class
{
    /// <summary>Instantiates the processor.</summary>
    /// <param name="services">The running engine's container, or <see langword="null"/> for one with no services.</param>
    public TProcessor Create(IServiceProvider? services)
    {
        ServiceProvider? empty = services is null ? new ServiceCollection().BuildServiceProvider() : null;
        try { return Adapt(ActivatorUtilities.GetServiceOrCreateInstance(services ?? empty!, Type)); }
        finally { empty?.Dispose(); }
    }
}
