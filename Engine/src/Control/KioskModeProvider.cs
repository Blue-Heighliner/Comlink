namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Controls whether the application UI runs in kiosk mode.</summary>
public interface IKioskModeProvider
{
    /// <summary><see langword="true"/> to enable kiosk mode, which hides window chrome and restricts navigation.</summary>
    bool IsKioskMode { get; }
}

/// <summary>Implements <see cref="IKioskModeProvider"/> with kiosk mode disabled.</summary>
internal sealed class KioskModeProvider : IKioskModeProvider
{
    /// <inheritdoc />
    public bool IsKioskMode => false;
}
