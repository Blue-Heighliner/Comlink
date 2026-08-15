namespace BlueHeighliner.Comlink.Engine.Devices;

/// <summary>Describes a single external drive available as an export destination or import source.</summary>
public sealed record ExternalDriveInfo
{
    /// <summary>Gets the root directory path to write to (e.g. <c>"E:\"</c> or <c>"/media/usb"</c>).</summary>
    public required string RootPath { get; init; }
    /// <summary>Gets the display label for the drive, combining its volume label (if any) and drive name.</summary>
    public required string DisplayName { get; init; }
}

/// <summary>
/// Enumerates the external (removable/optical) drives currently connected, ready, and writable — used as a
/// destination for the export feature or a source for the import feature (see <c>Docs/ViewModels.md</c>,
/// <c>IExportViewModel</c>/<c>IImportViewModel</c>). This is real OS-level behavior, not configuration or
/// rules, so it does not live on <see cref="Control.IEngineController"/> and is not registered/overridable
/// the way control interfaces are — Engine always provides real behavior for it directly, the same way it
/// always provides real behavior for alarm sound playback (see <see cref="IAlertSoundPlayer"/>) and
/// printer discovery/driving (see <see cref="IPrintDriver"/>).
/// </summary>
public interface IExternalDriveProvider
{
    /// <summary>Returns the external drives currently connected, ready, and writable.</summary>
    IReadOnlyList<ExternalDriveInfo> GetDrives();
}

/// <summary>
/// Default <see cref="IExternalDriveProvider"/> backed by <see cref="DriveInfo"/>, filtered to ready
/// removable and optical drives that pass a live write probe (a small temp file written and deleted at the
/// drive root). Not unit tested directly — inherently environment-dependent, so a unit test could only
/// meaningfully assert against whatever removable drives happen to be connected to the machine running the
/// test; see <c>Docs/Control.md</c>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ExternalDriveProvider : IExternalDriveProvider
{
    /// <inheritdoc />
    public IReadOnlyList<ExternalDriveInfo> GetDrives()
    {
        List<ExternalDriveInfo> drives = [];
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType is not (DriveType.Removable or DriveType.CDRom))
            {
                continue;
            }
            if (!IsWritable(drive))
            {
                continue;
            }

            drives.Add(new ExternalDriveInfo
            {
                RootPath = drive.RootDirectory.FullName,
                DisplayName = BuildDisplayName(drive)
            });
        }
        return drives;
    }

    private static bool IsWritable(DriveInfo drive)
    {
        string probePath = Path.Combine(drive.RootDirectory.FullName, $".comlink-write-check-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probePath, [0]);
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildDisplayName(DriveInfo drive)
    {
        string? label = TryGetVolumeLabel(drive);
        return string.IsNullOrWhiteSpace(label) ? drive.Name : $"{label} ({drive.Name})";
    }

    private static string? TryGetVolumeLabel(DriveInfo drive)
    {
        try { return drive.VolumeLabel; } catch { return null; }
    }
}
