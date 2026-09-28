namespace BlueHeighliner.Comlink.Sample;

/// <summary>Entry point for the Sample host application.</summary>
internal static class Program
{
    /// <summary>Application entry point; starts the engine with <see cref="SampleEngineConfiguration"/>.</summary>
    [STAThread]
    public static async Task Main(string[] args)
        => await Engine.Start<SampleEngineConfiguration>(args);
}
