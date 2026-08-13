namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IExternalDriveProvider"/> that reproduces the Engine default (ready, writable
/// removable/optical drives) plus an additional pseudo-drive at the path named by the
/// <c>EXPORT_DRIVE_PATH</c> environment variable, if set and the directory exists — useful for exercising
/// export/import without physical removable media.
/// </summary>
public sealed class SampleExternalDriveProvider : IExternalDriveProvider
{
    /// <inheritdoc />
    public IReadOnlyList<ExternalDriveInfo> GetDrives()
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

        string? envDrivePath = Environment.GetEnvironmentVariable("EXPORT_DRIVE_PATH");
        if (!string.IsNullOrWhiteSpace(envDrivePath) && Directory.Exists(envDrivePath))
        {
            drives.Add(new ExternalDriveInfo
            {
                RootPath = envDrivePath,
                DisplayName = $"EXPORT_DRIVE_PATH ({envDrivePath})"
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
