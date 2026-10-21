namespace BlueHeighliner.Comlink;

/// <summary>
/// Receives what Avalonia reports about itself, such as a binding that could not be resolved, in place of its own console output, and records it under
/// <see cref="LogCategories.Avalonia"/>, which is off unless <c>Logging.json</c> or <c>--log</c> turns it on. Avalonia starts logging before the engine's logging exists,
/// so what it reports until <see cref="Attach"/> is dropped.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class AvaloniaLogSink : ILogSink
{
    private volatile MicrosoftLogger? logger;
    private volatile ILogSettings? settings;

    /// <summary>Starts recording what Avalonia reports to <paramref name="logger"/>, subject to <paramref name="settings"/>.</summary>
    /// <param name="logger">The logger of the <see cref="LogCategories.Avalonia"/> category.</param>
    /// <param name="settings">Says whether that category is written.</param>
    public void Attach(MicrosoftLogger logger, ILogSettings settings)
    {
        this.settings = settings;
        this.logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning && logger is not null && settings?.IsEnabled(LogCategories.Avalonia) is true;

    /// <inheritdoc />
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Log(level, area, source, messageTemplate, []);

    /// <inheritdoc />
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (logger is { } target && IsEnabled(level, area))
        {
            target.Record(LogEvents.AvaloniaReported, "Avalonia {Level} in {Area} ({Source}): " + messageTemplate, [level, area, source?.GetType().Name, .. propertyValues]);
        }
    }
}
