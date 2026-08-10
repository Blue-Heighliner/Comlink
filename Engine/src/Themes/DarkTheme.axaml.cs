namespace BlueHeighliner.Comlink.Engine.Themes;

/// <summary>Avalonia styles that define the application dark theme.</summary>
[ExcludeFromCodeCoverage]
public partial class DarkTheme : Styles
{
    /// <summary>Initializes a new instance and loads the AXAML resource.</summary>
    public DarkTheme(IServiceProvider? sp = null)
    {
        AvaloniaXamlLoader.Load(sp, this);
    }
}
