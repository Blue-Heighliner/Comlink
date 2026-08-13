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
                services.AddSingleton<IUserGroupProvider, SampleUserGroupProvider>();
                services.AddSingleton<IHomeContentProvider, SampleHomeContentProvider>();
                services.AddSingleton<IAlertSoundPlayer, SampleAlertSoundPlayer>();
                services.AddSingleton<IMessagePriorityProvider, SampleMessagePriorityProvider>();
                services.AddSingleton<IAlertConfiguration, SampleAlertConfiguration>();
                services.AddSingleton<IAlertComposeConfiguration, SampleAlertComposeConfiguration>();
                services.AddSingleton<IAppNameProvider, SampleAppNameProvider>();
                services.AddSingleton<IAppDataPathProvider, SampleAppDataPathProvider>();
                services.AddSingleton<IPortConfiguration, SamplePortConfiguration>();
                services.AddSingleton<IKioskModeProvider, SampleKioskModeProvider>();
                services.AddSingleton<IExternalDriveProvider, SampleExternalDriveProvider>();
                services.AddSingleton<IOftPeerCertificateName, SampleOftPeerCertificateName>();
                services.AddSingleton<IDebugUserOverride, SampleDebugUserOverride>();
                services.AddSingleton<IMessageTagConfiguration, SampleMessageTagConfiguration>();
                services.AddSingleton<IMessageTagPriorityPolicy, SampleMessageTagPriorityPolicy>();
                services.AddSingleton<IPrintReceivedDefaultProvider, SamplePrintReceivedDefaultProvider>();
                services.AddSingleton<IPrintReceivedRule, SamplePrintReceivedRule>();
            },
            new Uri("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png"));
    }
}
