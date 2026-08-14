namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Control interface for this app's own identity and top-level presentation: its display/data-folder
/// name, the root directory its persistent state (LiteDB, user state, logs) is written under, whether the
/// main window runs in kiosk mode, and the placeholder text shown when no entry is selected.
/// </summary>
public interface IAppSettings
{
    /// <summary>The application name, used as the default data folder name and in log headers.</summary>
    string AppName { get; }
    /// <summary>Absolute path to the application data directory.</summary>
    string AppDataPath { get; }
    /// <summary><see langword="true"/> to enable kiosk mode, which hides window chrome and restricts navigation.</summary>
    bool IsKioskMode { get; }
    /// <summary>Returns the text displayed in the content area when no entry is selected.</summary>
    string GetHomeText();
}

/// <summary>
/// Implements <see cref="IAppSettings"/> deriving <see cref="AppName"/> from the entry assembly,
/// <see cref="AppDataPath"/> as <c>%APPDATA%\{AppName}</c>, kiosk mode disabled, and the literal home text
/// "HOME". Describes non-config-file behavior; see <see cref="ConfiguredAppSettings"/> for how
/// <c>config.json</c> overrides <see cref="AppDataPath"/>. Members are <see langword="virtual"/> so a host
/// can inherit and override just one — see <c>Docs/Control.md</c>. Note that the default <see cref="AppDataPath"/>
/// reads <see cref="AppName"/> through virtual dispatch, so a host overriding only <see cref="AppName"/>
/// automatically gets a matching default data folder without needing to also override <see cref="AppDataPath"/>.
/// </summary>
public class DefaultAppSettings : IAppSettings
{
    /// <inheritdoc />
    public virtual string AppName => Assembly.GetEntryAssembly()?.GetName().Name ?? "App";

    /// <inheritdoc />
    public virtual string AppDataPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    /// <inheritdoc />
    public virtual bool IsKioskMode => false;

    /// <inheritdoc />
    public virtual string GetHomeText() => "HOME";
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.DataFolder"/> over whichever <see cref="IAppSettings"/>
/// is registered (Engine default or a host override) — a path starting with <c>@</c> is relative to the
/// fallback's own <see cref="IAppSettings.AppDataPath"/>. Every other member is left entirely to the wrapped
/// provider, since there is no corresponding <c>config.json</c> field for them. Registered by
/// <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by control-interface convention scanning.
/// </summary>
internal sealed class ConfiguredAppSettings : IAppSettings
{
    private readonly IAppSettings _fallback;
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing a data folder override.</param>
    public ConfiguredAppSettings(IAppSettings fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
    }

    /// <inheritdoc />
    public string AppName => _fallback.AppName;

    /// <inheritdoc />
    public string AppDataPath => _config.DataFolder switch
    {
        null => _fallback.AppDataPath,
        ['@', ..] => Path.Combine(_fallback.AppDataPath, _config.DataFolder[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
        _ => _config.DataFolder
    };

    /// <inheritdoc />
    public bool IsKioskMode => _fallback.IsKioskMode;

    /// <inheritdoc />
    public string GetHomeText() => _fallback.GetHomeText();
}
