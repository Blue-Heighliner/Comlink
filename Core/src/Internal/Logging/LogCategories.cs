namespace BlueHeighliner.Comlink;

/// <summary>The log categories the engine writes under. A category is the only way a log entry is classified; there are no levels.</summary>
internal static class LogCategories
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

    /// <summary>Gets the only categories that are written unless <c>Logging.json</c> or the <c>--log</c> argument turns another on; every other category, including a host's own, is off.</summary>
    public static IReadOnlyList<string> OnByDefault { get; } = [Activity, App, Error, Crash];
}
