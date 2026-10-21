namespace BlueHeighliner.Comlink.Tests.Unit.Internal;

/// <summary>Unit tests for <see cref="ServiceInstantiation"/>.</summary>
public sealed class ServiceInstantiationTests
{
    private sealed class Dependency(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class Mixed(Dependency constructed)
    {
        public Dependency Constructed { get; } = constructed;

        public required Dependency Required { get; init; }

        public required Dependency? RequiredOptional { get; init; }

        public Dependency? NotRequired { get; init; }
    }

    private sealed class NeedsMissing
    {
        public required Dependency Missing { get; init; }
    }

    private static IServiceProvider Services(bool withDependency = true)
    {
        ServiceCollection services = new();
        if (withDependency)
        {
            services.AddSingleton(new Dependency("injected"));
        }

        return services.BuildServiceProvider();
    }

    /// <summary>The constructor's arguments and every required property, an init one included, come from the container; a property that is not required is left as it is.</summary>
    [Fact]
    public void Create_FillsConstructorArgumentsAndRequiredProperties()
    {
        Mixed mixed = Services().Create<Mixed>();

        Assert.Equal("injected", mixed.Constructed.Name);
        Assert.Equal("injected", mixed.Required.Name);
        Assert.Equal("injected", mixed.RequiredOptional?.Name);
        Assert.Null(mixed.NotRequired);
    }

    /// <summary>A required property whose type is nullable is optional: without the service it is left unset, where any other is an error naming the type and the property.</summary>
    [Fact]
    public void Create_RequiredNullableIsOptional_AnyOtherMissingServiceIsAnError()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Services(withDependency: false).Create<NeedsMissing>());

        Assert.Contains("NeedsMissing.Missing", ex.Message);
        Assert.Contains("Dependency", ex.Message);
    }

    /// <summary>A registered instance is returned as it is, and a type the container does not have is constructed with its required properties filled.</summary>
    [Fact]
    public void GetOrCreate_PrefersTheRegisteredInstance_ElseConstructs()
    {
        ServiceCollection services = new();
        Dependency registered = new("registered");
        services.AddSingleton(registered);
        services.AddSingleton<NeedsMissing>(_ => new NeedsMissing { Missing = registered });
        IServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(registered, provider.GetOrCreate(typeof(Dependency)));
        Assert.Same(provider.GetRequiredService<NeedsMissing>(), provider.GetOrCreate(typeof(NeedsMissing)));
        Mixed constructed = Assert.IsType<Mixed>(provider.GetOrCreate(typeof(Mixed)));
        Assert.Same(registered, constructed.Required);
        Assert.NotSame(constructed, provider.GetOrCreate(typeof(Mixed)));
    }
}
