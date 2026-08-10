namespace BlueHeighliner.Comlink.Sample;

/// <summary>Entry point for the Sample host application.</summary>
internal static class Program
{
    /// <summary>Application entry point; registers sample services and starts the engine.</summary>
    [STAThread]
    public static async Task Main(string[] args)
    {
        await EngineApplication.Start<SampleMessageFormat>(
            args,
            services =>
            {
                services.AddSingleton<ISiteLocator, SampleSiteLocator>();
                services.AddSingleton<ISiteCodeResolver, SampleSiteCodeResolver>();
                services.AddSingleton<ISiteNameDirectory, SampleSiteNameDirectory>();
                services.AddSingleton<IHomeContentProvider, SampleHomeContentProvider>();
            },
            new Uri("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png"));
    }
}
