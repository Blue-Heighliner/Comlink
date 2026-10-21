namespace BlueHeighliner.Comlink.Tests;

/// <summary>A service the convention scan should find, because <see cref="ConventionService"/> is the class named for it.</summary>
public interface IConventionService
{
    /// <summary>Gets which implementation this is.</summary>
    string Origin { get; }
}

/// <summary>The class the convention scan registers for <see cref="IConventionService"/>.</summary>
public sealed class ConventionService : IConventionService
{
    /// <inheritdoc />
    public string Origin => "convention";
}

/// <summary>A service that takes what it needs as required properties, which the convention scan fills in.</summary>
public interface IConventionConsumer
{
    /// <summary>Gets the service that was injected.</summary>
    IConventionService Service { get; }

    /// <summary>Gets the optional service, or <see langword="null"/> when the container has none.</summary>
    IOptionalConvention? Optional { get; }
}

/// <summary>A service nothing registers, for an optional required property.</summary>
public interface IOptionalConvention
{
}

/// <summary>The class the convention scan registers for <see cref="IConventionConsumer"/>.</summary>
public sealed class ConventionConsumer : IConventionConsumer
{
    /// <inheritdoc />
    public required IConventionService Service { get; init; }

    /// <inheritdoc />
    public IOptionalConvention? Optional { get; init; }
}

/// <summary>A configuration that matches the naming convention, which the scan must leave alone.</summary>
public interface ITestScannedConfiguration : IEngineConfiguration
{
}

/// <summary>The class named for <see cref="ITestScannedConfiguration"/>.</summary>
public sealed class TestScannedConfiguration : ITestScannedConfiguration
{
    /// <inheritdoc />
    public void Configure(IEngineBuilder engine)
    {
    }
}
