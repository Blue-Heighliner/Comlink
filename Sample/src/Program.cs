namespace BlueHeighliner.Comlink.Sample;

/// <summary>Entry point for the Sample host application.</summary>
internal static class Program
{
    /// <summary>Application entry point; registers sample services and starts the engine.</summary>
    [STAThread]
    public static async Task Main(string[] args)
    {
        // SampleEngineController overrides only the members where Sample has a genuinely distinct,
        // non-config-file behavior to demonstrate — see Docs/Control.md. Every other member uses
        // the Engine default, with config.json applied on top automatically (UseEngineConfigOverrides).
        await EngineApplication.Start<SampleEngineController>(
            args,
            windowIconUri: new Uri("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png"));
    }
}
