using System.Globalization;

namespace AnyPSConversor;

/// <summary>Confere o espaço livre antes de extrair ou converter, para não parar no meio com o disco cheio.</summary>
public static class DiskSpace
{
    /// <summary>Folga sobre o estimado: sistema de arquivos, arquivos temporários do relinker e arredondamentos.</summary>
    public static long WithMargin(long bytes) => bytes + bytes / 20 + (256L << 20);

    /// <summary>Lança <see cref="UserException"/> "err_disk_space" se o disco de <paramref name="path"/> não comporta <paramref name="required"/> bytes.</summary>
    public static void Ensure(string path, long required, long? available = null)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path))!;
        var free = available ?? new DriveInfo(root).AvailableFreeSpace;
        var needed = WithMargin(required);
        if (free < needed)
            throw new UserException("err_disk_space", Format(needed), root, Format(free), Format(needed - free));
    }

    /// <summary>
    /// Confere várias necessidades de uma vez, somadas por disco: destino e pasta temporária no mesmo disco
    /// disputam o mesmo espaço livre, e conferidos um a um cada um passaria sozinho.
    /// </summary>
    public static void EnsureAll(IEnumerable<(string Path, long Bytes)> needs, Func<string, long>? available = null)
    {
        foreach (var disk in needs.GroupBy(n => Path.GetPathRoot(Path.GetFullPath(n.Path))!, StringComparer.OrdinalIgnoreCase))
            Ensure(disk.Key, disk.Sum(n => n.Bytes), available?.Invoke(disk.Key));
    }

    /// <summary>Verdadeiro para "disco cheio" do Windows (ERROR_DISK_FULL e ERROR_HANDLE_DISK_FULL).</summary>
    public static bool IsDiskFull(Exception e) => e is IOException && (e.HResult & 0xFFFF) is 0x70 or 0x27;

    /// <summary>Lança a mensagem de disco cheio no lugar do erro de E/S, citando o disco de <paramref name="path"/>.</summary>
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
