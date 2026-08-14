namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Describes a single external drive available as an export destination.</summary>
public sealed record ExternalDriveInfo
{
    /// <summary>Gets the root directory path to write to (e.g. <c>"E:\"</c> or <c>"/media/usb"</c>).</summary>
    public required string RootPath { get; init; }
    /// <summary>Gets the display label for the drive, combining its volume label (if any) and drive name.</summary>
    public required string DisplayName { get; init; }
}

/// <summary>Enumerates the external (removable/optical) drives currently available for the export feature to write to.</summary>
public interface IExternalDriveProvider
{
    /// <summary>Returns the external drives currently connected, ready, and writable.</summary>
    IReadOnlyList<ExternalDriveInfo> GetDrives();
}

/// <summary>
/// Default <see cref="IExternalDriveProvider"/> backed by <see cref="DriveInfo"/>, filtered to ready
/// removable and optical drives that pass a live write probe. Members are <see langword="virtual"/> so a
/// host can inherit and override — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultExternalDriveProvider : IExternalDriveProvider
{
    /// <inheritdoc />
    public virtual IReadOnlyList<ExternalDriveInfo> GetDrives()
    {
        List<ExternalDriveInfo> drives = [];
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType is not (DriveType.Removable or DriveType.CDRom))
                continue;
            if (!IsWritable(drive))
                continue;

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
