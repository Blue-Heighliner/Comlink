namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Control interface: determines whether the <c>--config</c> command-line argument is honored at all.
/// Resolved once, before any other control interface, since it gates whether <see cref="EngineConfig.Load"/>
/// ever runs — see <see cref="EngineApplication.Start{TMessageFormat}"/>. Because of this, an implementation
/// of this interface must never depend on <see cref="EngineConfig"/> itself.
/// </summary>
public interface IConfigFileProvider
{
    /// <summary>When <see langword="true"/> (the default), a <c>--config</c> argument is read; when <see langword="false"/>, it is ignored and <see cref="EngineConfig"/> always uses its defaults.</summary>
    bool Enabled { get; }
}

/// <summary>
/// Implements <see cref="IConfigFileProvider"/> with config file reading enabled. Members are
/// <see langword="virtual"/> so a host can inherit and override — see <c>Docs/Control.md</c>. As with any
/// <see cref="IConfigFileProvider"/> implementation, a derived class must never depend on <see cref="EngineConfig"/>.
/// </summary>
public class DefaultConfigFileProvider : IConfigFileProvider
{
    /// <inheritdoc />
    public virtual bool Enabled => true;
}
