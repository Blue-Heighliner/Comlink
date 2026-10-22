namespace BlueHeighliner.Comlink;

/// <summary>Says whether the application is running in debug mode, for any component that behaves differently while it is being developed. Ask for it through dependency injection; it is also available to the configuration, so a host can state settings (such as <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.StackTrace"/>) from it.</summary>
public interface IDebugMode
{
    /// <summary>Gets a value indicating whether the application was compiled with its own <c>DEBUG</c> compiler constant defined, as the Debug configuration does and the Release configuration does not. The package's build targets record the constant in the application's assembly, which is why a <c>#if DEBUG</c> in the engine itself would not do: the package is always a Release build.</summary>
    bool IsDebug { get; }
}
