namespace BlueHeighliner.Comlink;

/// <summary>The log categories the engine writes under, which a component of the host can write under too (see <see cref="ILogger"/>). A category is the only way a log entry is classified; there are no levels.</summary>
public static class LogCategories
{
    /// <summary>Gets the category of what the user reads in the activity log, in general terms.</summary>
    public static string Activity { get; } = "ACTIVITY";

    /// <summary>Gets the category of the trace of every frame, as serialized bytes, sent and received.</summary>
    public static string Frames { get; } = "FRAMES";

    /// <summary>Gets the category of the trace of every packet, as serialized bytes, sent and received.</summary>
    public static string Packets { get; } = "PACKETS";

    /// <summary>Gets the category of technical events of the running application: connections, services and the network layers.</summary>
    public static string App { get; } = "APP";

    /// <summary>Gets the category of something that failed, with the technical detail.</summary>
    public static string Error { get; } = "ERROR";

    /// <summary>Gets the category of what Avalonia reports about itself, such as a binding that could not be resolved.</summary>
    public static string Avalonia { get; } = "AVALONIA";

    /// <summary>Gets the category of a failure the application cannot go on after, or that stops a part of it.</summary>
    public static string Crash { get; } = "CRASH";

    /// <summary>Gets the category for a host's own debugging output. The engine never writes under it. Unlike every other category it is not turned on by <c>Logging.json</c>, the <c>--log</c> argument or <see cref="ILogHandler.CategoriesOnByDefault"/>: it is written exactly while <see cref="IDebugMode.IsDebug"/> is <see langword="true"/>.</summary>
    public static string Debug { get; } = "DEBUG";

    /// <summary>Gets the category of the activity of the serial (MicroGate) links: a port opening, connecting, connected, disconnected or failing, with its port and local and remote addresses. Off by default.</summary>
    public static string Serial { get; } = "SERIAL";

    /// <summary>Gets the category of the activity of the IP (MSMT) connections: the listener starting and stopping, connection attempts and failures, and connections made, accepted and closed, with their host and port. Off by default.</summary>
    public static string Ip { get; } = "IP";

    /// <summary>Gets the only categories that are written unless <c>Logging.json</c> or the <c>--log</c> argument turns another on; every other category, including a host's own, is off (except <see cref="Debug"/>, which follows debug mode).</summary>
    public static IReadOnlyList<string> OnByDefault { get; } = [Activity, App, Error, Crash];
}
