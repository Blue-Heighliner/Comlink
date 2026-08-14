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
                // Only control interfaces where Sample has a genuinely distinct, non-config-file behavior
                // to demonstrate are overridden here — see Docs/Control.md. Every other interface uses the
                // Engine default, with config.json applied on top automatically (UseEngineConfigOverrides).
                services.AddSingleton<IUserIdentity, SampleUserIdentity>();
                services.AddSingleton<IUserDirectory, SampleUserDirectory>();
                services.AddSingleton<IAppSettings, SampleAppSettings>();
                services.AddSingleton<IAlertSettings, SampleAlertSettings>();
                services.AddSingleton<IMessageComposition, SampleMessageComposition>();
                services.AddSingleton<IPrintPolicy, SamplePrintPolicy>();
            },
            new Uri("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png"));
    }
}
