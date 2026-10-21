namespace BlueHeighliner.Comlink;

/// <summary>Calls the host's <see cref="IReconfigurer"/> each time the configuration has been loaded and the current user identified.</summary>
internal interface IReconfigurationService
{
    /// <summary>
    /// Calls the host's reconfigurer, if it stated one, unless there is no current user. An exception it throws, or one thrown building it, is logged rather than thrown back.
    /// </summary>
    Task Run();
}

/// <inheritdoc cref="IReconfigurationService" />
/// <param name="engineController">Supplies the host's reconfigurer.</param>
/// <param name="userService">Says whether there is a current user.</param>
/// <param name="loggerFactory">Factory for the logger that records a failure.</param>
internal sealed class ReconfigurationService(IEngineController engineController, IUserService userService, ILoggerFactory loggerFactory) : IReconfigurationService
{
    private readonly MicrosoftLogger logger = loggerFactory.CreateLogger(LogCategories.App);

    /// <inheritdoc />
    public async Task Run()
    {
        if (userService.GetCurrentUserInfo() is null)
        {
            return;
        }

        try
        {
            if (engineController.Reconfigurer is { } reconfigurer)
            {
                await reconfigurer.OnReconfigured();
            }
        }
        catch (Exception ex)
        {
            logger.Record(LogEvents.ReconfigurerFailed, ex, "The reconfigurer failed: {Message}", ex.Message);
            logger.Record(LogEvents.ReconfigurerStopped, "The application's own work after a configuration change failed");
        }
    }
}
