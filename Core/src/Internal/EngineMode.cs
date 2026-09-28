namespace BlueHeighliner.Comlink;

/// <summary>Specifies the operating mode for the Engine host.</summary>
internal enum EngineMode
{
    /// <summary>Runs the full GUI client with local database and UI.</summary>
    Client,
    /// <summary>Runs headless — as a normal peer client, with local database and no UI.</summary>
    Headless
}
