namespace BlueHeighliner.Comlink.Engine;

/// <summary>Extension methods for wiring the Avalonia GUI layer (Views, ViewModel registrations, styles) into the DI host and Avalonia application builder.</summary>
[ExcludeFromCodeCoverage]
public static class EngineUiExtensions
{
    /// <summary>
    /// Registers GUI-layer components — <see cref="IMainViewModel"/> and <see cref="MainWindow"/> — into the host's DI container.
    /// Call this after <see cref="EngineExtensions.UseEngine"/> when running in GUI client mode.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The same <see cref="IHostBuilder"/> for chaining.</returns>
    public static IHostBuilder UseEngineUi(this IHostBuilder builder)
    {
        return builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton<IBodyDocumentFactory, TextDocumentBodyDocumentFactory>();
            services.TryAddSingleton<IMainViewModel, MainViewModel>();
            services.AddSingleton<MainWindow>();
        });
    }

    /// <summary>Applies the Engine dark theme and AvaloniaEdit styles to the Avalonia application builder.</summary>
    /// <param name="builder">The Avalonia application builder to configure.</param>
    /// <returns>The same <see cref="AppBuilder"/> for chaining.</returns>
    public static AppBuilder UseEngineStyles(this AppBuilder builder)
    {
        return builder.AfterSetup(b =>
        {
            if (b.Instance is null) { return; }
            b.Instance.RequestedThemeVariant = ThemeVariant.Dark;
            b.Instance.Styles.Add(new StyleInclude(new Uri("avares://AvaloniaEdit"))
            {
                Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml")
            });
            b.Instance.Styles.Add(new DarkTheme());
        });
    }
}
