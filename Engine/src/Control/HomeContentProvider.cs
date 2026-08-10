namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Control interface: provides the string shown in the content area when no entry is selected.</summary>
public interface IHomeContentProvider
{
    /// <summary>Returns the text displayed in the content area when no entry is selected.</summary>
    string GetHomeText();
}

/// <summary>Implements <see cref="IHomeContentProvider"/> returning the literal text "HOME".</summary>
internal sealed class HomeContentProvider : IHomeContentProvider
{
    /// <inheritdoc />
    public string GetHomeText() => "HOME";
}
