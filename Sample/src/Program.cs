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
                services.AddSingleton<IUserLocator, SampleUserLocator>();
                services.AddSingleton<IUserCodeResolver, SampleUserCodeResolver>();
                services.AddSingleton<IUserNameDirectory, SampleUserNameDirectory>();
                services.AddSingleton<IHomeContentProvider, SampleHomeContentProvider>();
                services.AddSingleton<IAlertSoundPlayer, SampleAlertSoundPlayer>();
            },
            new Uri("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png"));
    }
}
