namespace BlueHeighliner.Comlink;

/// <summary>Extension methods for wiring the Engine into a .NET generic host.</summary>
[ExcludeFromCodeCoverage]
internal static class EngineExtensions
{
    /// <summary>
    /// Registers all Engine services, repositories, ViewModels, and infrastructure into the host's DI container,
    /// including the <see cref="IEngineController"/> built from what the host stated and the configuration file.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="mode">Whether to run as a GUI client or headless peer client.</param>
    /// <param name="engine">What the host stated in its <see cref="IEngineConfiguration"/>.</param>
    /// <param name="network">The loaded network configuration: the users of the network and the node settings for this process.</param>
    /// <param name="identity">What checking the node's certificates found: who the node is, or why that cannot be told.</param>
    public static IHostBuilder UseEngine(this IHostBuilder builder, EngineMode mode, EngineBuilder engine, NetworkConfig network, IdentityCheck identity)
    {
        return builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton(typeof(EngineMode), mode);
            services.AddSingleton(engine);
            services.AddSingleton(network);
            services.AddSingleton(identity);
            services.AddSingleton<ICurrentUserProvider>(new CurrentUserProvider { UserName = identity.Problem is null ? identity.Identity.UserName : null });
            services.AddSingleton<IEngineController>(sp =>
            {
                ICurrentUserProvider currentUser = sp.GetRequiredService<ICurrentUserProvider>();
                return new ConfiguredEngineController(new EngineController(engine, currentUser, network, sp), network, currentUser, identity.Identity);
            });

            services.AddSingleton<RolePeerService>();
            services.AddSingleton<IRolePeerService>(sp => sp.GetRequiredService<RolePeerService>());
            services.AddSingleton<IPeerService>(sp => sp.GetRequiredService<RolePeerService>());
            services.AddSingleton<IConnectionStatusService>(sp => sp.GetRequiredService<RolePeerService>());

            services.AddConventionSingletons();
            services.AddMsmt();
            services.TryAddSingleton<IHdlcPeerFactory, HdlcPeerFactory>();

            services.AddSingleton<IEngineConnection, DirectServiceConnection>();
            engine.TypedServices?.Invoke(services);
            if (mode is EngineMode.Client)
            {
                services.TryAddSingleton<IBodyDocumentFactory, BodyDocumentFactory>();
            }

            services.AddHostedService<EngineHost>();
        }).ConfigureLogging((_, logging) =>
        {
            logging.ClearProviders();
            logging.AddFilter("Microsoft", LogLevel.None);
            logging.AddFilter("System", LogLevel.None);
            logging.Services.AddSingleton<ILoggerProvider, DailyFileLoggerProvider>();
            if (mode is EngineMode.Client)
            {
                logging.Services.AddSingleton<ILoggerProvider, ActivityLoggerProvider>();
            }
        });
    }

    /// <summary>
    /// Registers what the host's configuration and handlers are built from: the host's own registrations, then every concrete class of the host's assemblies as its <c>IThing</c> interface singleton
    /// (see <see cref="AddHostConventionSingletons"/>), which never replaces a registration the host made itself.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="configure">Registers the host's own services, or <see langword="null"/> for none.</param>
    /// <param name="assemblies">The host's assemblies to scan.</param>
    public static IHostBuilder UseHostServices(this IHostBuilder builder, Action<IServiceCollection>? configure, IReadOnlyList<Assembly> assemblies)
        => builder.ConfigureServices((_, services) =>
        {
            configure?.Invoke(services);
            services.AddHostConventionSingletons(assemblies);
        });

    /// <summary>
    /// Scans <paramref name="assemblies"/> and registers each concrete class as its <c>IThing</c> interface singleton, constructed through dependency injection (see <see cref="ServiceInstantiation"/>),
    /// with <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService}(IServiceCollection, Func{IServiceProvider, TService})"/>, so a service the host registered by hand always takes precedence.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <param name="assemblies">The host's assemblies to scan.</param>
    public static IServiceCollection AddHostConventionSingletons(this IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        foreach ((Type iface, Type type) in assemblies.Distinct().SelectMany(ConventionPairs))
        {
            if (!typeof(IEngineConfiguration).IsAssignableFrom(type))
            {
                services.TryAddSingleton(iface, provider => provider.Create(type));
            }
        }

        return services;
    }

    private static void AddConventionSingletons(this IServiceCollection services)
    {
        foreach ((Type iface, Type type) in ConventionPairs(typeof(EngineExtensions).Assembly))
        {
            services.TryAddSingleton(iface, type);
        }
    }

    // Each concrete class that implements the interface named for it, such as MyService for IMyService. A class marked ConstructedManually is built where it is used, so it is left out.
    private static IEnumerable<(Type Interface, Type Type)> ConventionPairs(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = [.. ex.Types.OfType<Type>()];
        }

        foreach (Type type in types)
        {
            if (type.IsAbstract || !type.IsClass || type.IsNested || type.IsGenericTypeDefinition || type.IsDefined(typeof(ConstructedManuallyAttribute), false))
            {
                continue;
            }

            if (type.GetInterfaces().FirstOrDefault(candidate => candidate.Name == $"I{type.Name}" && !candidate.IsGenericTypeDefinition) is { } iface)
            {
                yield return (iface, type);
            }
        }
    }
}
