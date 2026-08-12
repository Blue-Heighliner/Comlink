namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>Which entries an export operation should include.</summary>
public enum ExportScope
{
    /// <summary>Export every message, draft, note, and activity log entry in the database.</summary>
    All,
    /// <summary>Export only the entries the user has explicitly added to <see cref="IExportViewModel.SelectedEntries"/>.</summary>
    Some
}
