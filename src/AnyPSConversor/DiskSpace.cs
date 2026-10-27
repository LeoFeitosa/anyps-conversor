using System.Globalization;

namespace AnyPSConversor;

/// <summary>Checks free space before extracting or converting, so as not to stop midway with a full drive.</summary>
public static class DiskSpace
{
    /// <summary>Margin over the estimate: file system, relinker temporary files and rounding.</summary>
    public static long WithMargin(long bytes) => bytes + bytes / 20 + (256L << 20);

    /// <summary>Throws <see cref="UserException"/> "err_disk_space" if the drive of <paramref name="path"/> cannot hold <paramref name="required"/> bytes.</summary>
    public static void Ensure(string path, long required, long? available = null)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path))!;
        var free = available ?? new DriveInfo(root).AvailableFreeSpace;
        var needed = WithMargin(required);
        if (free < needed)
            throw new UserException("err_disk_space", Format(needed), root, Format(free), Format(needed - free));
    }

    /// <summary>
    /// Checks several needs at once, summed per drive: destination and temporary folder on the same drive
    /// compete for the same free space, and checked one by one each would pass on its own.
    /// </summary>
    public static void EnsureAll(IEnumerable<(string Path, long Bytes)> needs, Func<string, long>? available = null)
    {
        foreach (var disk in needs.GroupBy(n => Path.GetPathRoot(Path.GetFullPath(n.Path))!, StringComparer.OrdinalIgnoreCase))
            Ensure(disk.Key, disk.Sum(n => n.Bytes), available?.Invoke(disk.Key));
    }

    /// <summary>True for Windows "disk full" (ERROR_DISK_FULL and ERROR_HANDLE_DISK_FULL).</summary>
    public static bool IsDiskFull(Exception e) => e is IOException && (e.HResult & 0xFFFF) is 0x70 or 0x27;

    /// <summary>Throws the disk full message in place of the I/O error, naming the drive of <paramref name="path"/>.</summary>
    public static UserException DiskFull(string path) =>
        new("err_disk_full", Path.GetPathRoot(Path.GetFullPath(path))!);

    public static long FolderSize(string folder, Func<string, bool>? include = null) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => include?.Invoke(f) ?? true)
            .Sum(f => new FileInfo(f).Length);

    public static string Format(long bytes) => bytes >= 1L << 30
        ? (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.CurrentCulture) + " GB"
        : Math.Max(1, bytes >> 20).ToString(CultureInfo.CurrentCulture) + " MB";
}
