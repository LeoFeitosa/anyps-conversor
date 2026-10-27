using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace AnyPSConversor;

/// <summary>The archive has a password: none was given (<see cref="Wrong"/> false) or the one given does not open it.</summary>
public sealed class PasswordException(bool wrong) : Exception("password required")
{
    public bool Wrong { get; } = wrong;
}

/// <summary>Extraction progress: percentage and, as each entry starts, its path in the archive.</summary>
public readonly record struct ExtractProgress(int Percent, string? Entry);

/// <summary>
/// What is known about an archive before extracting it: the game (name and icon, when readable without
/// decompressing the whole archive), the size to extract and that of the executables. The sizes come from the index and
/// are used to check the space for the whole process, split the bar and estimate the time; in tar, gz, bz2 and xz,
/// which have no index, they are the archive's own size and zero.
/// </summary>
public sealed record ArchivePreview(GamePreview? Game, long Size, long Executables);

/// <summary>
/// Extracts the game from an archive (.zip, .rar, .7z, .tar and variants) to a work folder in the
/// same location where the converted game will be saved, to use the drive chosen by the user and be able to move
/// (instead of copy) the files to app0/.
/// </summary>
public static class Archives
{
    public static readonly string[] Extensions = [".zip", ".rar", ".7z", ".tar", ".tgz", ".gz", ".bz2", ".xz"];

    public static bool IsArchive(string path) =>
        File.Exists(path) && Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    static readonly string TempBase = Path.Combine(Path.GetTempPath(), Settings.Name);
    const string WorkFolderName = "." + Settings.Name;

    /// <summary>Names from version 0.6 and earlier: whatever is left under them is cleaned up too.</summary>
    static readonly string LegacyTempBase = Path.Combine(Path.GetTempPath(), Settings.LegacyName);
    const string LegacyWorkFolderName = "." + Settings.LegacyName;
    static readonly string RunName = "run-" + Environment.ProcessId;

    /// <summary>
    /// This run's temporary folder for what is small (relinker and unwrapped executables). It lives
    /// in a subfolder named after the process ID so that <see cref="CleanStaleTemp"/> knows what to delete.
    /// </summary>
    public static string TempRoot { get; } = Path.Combine(TempBase, RunName);

    /// <summary>This run's hidden folder inside the output location, where extractions go.</summary>
    public static string WorkRoot(string outputParent)
    {
        var root = Path.Combine(outputParent, WorkFolderName);
        Directory.CreateDirectory(root);
        File.SetAttributes(root, File.GetAttributes(root) | FileAttributes.Hidden);
        return Path.Combine(root, RunName);
    }

    /// <summary>
    /// True inside a work folder (of this or another run, including old versions):
    /// everything in it is deleted when the owning run ends or the next time the program opens.
    /// </summary>
    public static bool IsInsideWorkFolder(string path)
    {
        var full = SourceCleanup.Normalize(path);
        return full.Split(Path.DirectorySeparatorChar).Any(p =>
                   p.Equals(WorkFolderName, StringComparison.OrdinalIgnoreCase) || p.Equals(LegacyWorkFolderName, StringComparison.OrdinalIgnoreCase))
               || SourceCleanup.IsSameOrInside(full, SourceCleanup.Normalize(TempBase))
               || SourceCleanup.IsSameOrInside(full, SourceCleanup.Normalize(LegacyTempBase));
    }

    /// <summary>
    /// Deletes, after the program closes, this run's work folder and the half-converted game,
    /// through a separate cmd that waits 2 s (files opened by this process are released on exit). Whatever
    /// is left of the work folder the next startup cleans up.
    /// </summary>
    public static void DeleteAfterExit(string outputParent, string? unfinished)
    {
        var root = Path.Combine(outputParent, WorkFolderName);
        var folders = new[] { Path.Combine(root, RunName), unfinished }.Where(Directory.Exists).ToList();
        if (folders.Count == 0)
            return;
        var removes = string.Concat(folders.Select(f => $" & rd /s /q \"{f}\""));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
            $"/d /c ping -n 3 127.0.0.1 >nul{removes} & rd \"{root}\" 2>nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }

    /// <summary>Deletes this run's work folder and, if it becomes empty, the hidden folder that holds it.</summary>
    public static void DeleteWorkRoot(string outputParent)
    {
        var root = Path.Combine(outputParent, WorkFolderName);
        TryDelete(Path.Combine(root, RunName));
        RemoveIfEmpty(root);
    }

    /// <summary>
    /// Deletes what previous runs left behind when they were force-closed or the PC shut down: subfolders
    /// of processes that no longer exist in %TEMP% and in the work folder of the given output locations,
    /// plus the loose folders from versions 0.1 and 0.2.
    /// </summary>
    public static void CleanStaleTemp(params string?[] outputParents)
    {
        var bases = outputParents
            .Where(p => !string.IsNullOrEmpty(p))
            .SelectMany(p => new[] { Path.Combine(p!, WorkFolderName), Path.Combine(p!, LegacyWorkFolderName) })
            .Prepend(LegacyTempBase)
            .Prepend(TempBase);
        foreach (var folderBase in bases)
        {
            if (!Directory.Exists(folderBase))
                continue;
            foreach (var folder in Directory.GetDirectories(folderBase))
            {
                var name = Path.GetFileName(folder);
                var alive = name.StartsWith("run-", StringComparison.Ordinal)
                    && int.TryParse(name.AsSpan(4), out var pid)
                    && IsConverterProcess(pid);
                if (!alive)
                    TryDelete(folder);
            }
            if (folderBase != TempBase)
                RemoveIfEmpty(folderBase);
        }
    }

    static void RemoveIfEmpty(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    static bool IsConverterProcess(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            // An old version (AnyPS5-Conversor.exe) open at the same time still owns its folders.
            string[] names = [System.Diagnostics.Process.GetCurrentProcess().ProcessName, Settings.Name, Settings.LegacyName];
            return pid == Environment.ProcessId || names.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Zip, rar and 7z have an index and are opened whole; tar, gz, bz2 and xz are read sequentially and have no password.</summary>
    static bool IsIndexed(string archive) =>
        new[] { ".zip", ".rar", ".7z" }.Any(e => archive.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Checks the archive without extracting anything, to reject right away what is not a game and show the game before
    /// converting. From the index: without any console's main file, rejects. If the archive is not solid, also reads
    /// the main file (rejects if encrypted), the name and the icon; in a solid archive this would require
    /// decompressing almost everything, so it is left for the extraction.
    /// </summary>
    /// <exception cref="PasswordException">The archive has a password and it is missing or wrong.</exception>
    public static ArchivePreview Inspect(string archive, string? password)
    {
        if (!IsIndexed(archive))
        {
            // No file index: .xz still states the final size; .tar already is it; gz and bz2 don't store it.
            var length = new FileInfo(archive).Length;
            var size = archive.EndsWith(".xz", StringComparison.OrdinalIgnoreCase) ? XzUncompressedSize(archive) ?? length : length;
            return new(null, size, 0);
        }
        if (password is null && NeedsPassword(archive))
            throw new PasswordException(wrong: false);
        try
        {
            using var opened = ArchiveFactory.OpenArchive(archive, new ReaderOptions { Password = password });
            var entries = opened.Entries.Where(e => !e.IsDirectory).ToList();
            var size = entries.Sum(e => Math.Max(0, e.Size));
            var (main, platform) = CheckRequiredFiles(archive, opened, entries);
            var folder = EntryPath(main)[..(EntryPath(main).LastIndexOf('/') + 1)];
            var executables = entries
                .Where(e => EntryPath(e).StartsWith(folder, StringComparison.OrdinalIgnoreCase)
                    && platform.IsExecutable(EntryPath(e)[folder.Length..]))
                .Sum(e => Math.Max(0, e.Size));
            if (opened.IsSolid)
                return new(null, size, executables);
            byte[]? Read(string relative) =>
                entries.FirstOrDefault(e => EntryPath(e).Equals(folder + relative, StringComparison.OrdinalIgnoreCase)) is { } entry
                    ? ReadEntry(entry)
                    : null;
            return new(platform.Preview(Read), size, executables);
        }
        catch (Exception e) when (e is not UserException and not PasswordException)
        {
            throw ReadFailure(e, archive, password);
        }
    }

    /// <summary>
    /// Read error translated for the user. Each format reports a wrong password differently
    /// (CryptographicException, "bad password", data error in 7z); with a password given, any read
    /// failure is treated as a wrong password.
    /// </summary>
    static Exception ReadFailure(Exception e, string archive, string? password) =>
        password is not null ? new PasswordException(wrong: true)
        : e is CryptographicException ? new PasswordException(wrong: false)
        : new UserException("err_archive", Path.GetFileName(archive), e.Message);

    /// <summary>
    /// Uncompressed size of an .xz, summed from the index each stream keeps at its end (.xz format 1.0.4,
    /// sections 2.1.2 and 4). Reads only a few bytes from the end of the file. Null if the file does not follow the format.
    /// </summary>
    public static long? XzUncompressedSize(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            var footer = new byte[12];
            long end = file.Length, total = 0;
            while (end > 0)
            {
                // Stream padding: zeroed 4-byte blocks.
                while (end >= 4)
                {
                    file.Position = end - 4;
                    file.ReadExactly(footer, 0, 4);
                    if (footer[0] != 0 || footer[1] != 0 || footer[2] != 0 || footer[3] != 0)
                        break;
                    end -= 4;
                }
                if (end < 24)
                    return null;
                file.Position = end - 12;
                file.ReadExactly(footer);
                if (footer[10] != (byte)'Y' || footer[11] != (byte)'Z')
                    return null;
                var indexSize = ((long)BitConverter.ToUInt32(footer, 4) + 1) * 4;
                var indexStart = end - 12 - indexSize;
                if (indexStart < 12)
                    return null;
                var index = new byte[indexSize];
                file.Position = indexStart;
                file.ReadExactly(index);
                var position = 0;
                if (index[position++] != 0)
                    return null;
                long blocks = 0;
                for (var records = ReadVarint(index, ref position); records > 0; records--)
                {
                    var unpadded = ReadVarint(index, ref position);
                    total += ReadVarint(index, ref position);
                    blocks += (unpadded + 3) & ~3L;
                }
                // Before the blocks comes the stream's 12-byte header; before that, the previous stream.
                end = indexStart - blocks - 12;
                if (end < 0)
                    return null;
            }
            return total;
        }
        catch (Exception e) when (e is IOException or IndexOutOfRangeException or InvalidDataException)
        {
            return null;
        }
    }

    static long ReadVarint(byte[] data, ref int position)
    {
        long value = 0;
        for (var shift = 0; shift < 63; shift += 7)
        {
            var current = data[position++];
            value |= (long)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return value;
        }
        throw new InvalidDataException("xz varint too long");
    }

    static string EntryPath(IArchiveEntry entry) => (entry.Key ?? "").Replace('\\', '/').TrimStart('/');

    static byte[] ReadEntry(IArchiveEntry entry)
    {
        using var input = entry.OpenEntryStream();
        using var data = new MemoryStream();
        input.CopyTo(data);
        return data.ToArray();
    }

    /// <param name="password">Archive password; null until the user provides one.</param>
    /// <param name="workRoot">Where to extract; usually the output location's <see cref="WorkRoot"/>.</param>
    /// <exception cref="PasswordException">The archive has a password and it is missing or wrong.</exception>
    public static string Extract(string archive, string? password, string workRoot, IProgress<ExtractProgress> progress, CancellationToken cancel)
    {
        var indexed = IsIndexed(archive);
        if (indexed && password is null && NeedsPassword(archive))
            throw new PasswordException(wrong: false);

        var target = Path.Combine(workRoot, "extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(target);
        try
        {
            var options = new ReaderOptions { Password = password };
            if (indexed)
                ExtractIndexed(archive, target, options, progress, cancel);
            else
                ExtractStream(archive, target, options, progress, cancel);
            return target;
        }
        catch (Exception e) when (e is not OperationCanceledException and not UserException and not PasswordException)
        {
            TryDelete(target);
            if (DiskSpace.IsDiskFull(e))
                throw DiskSpace.DiskFull(target);
            throw ReadFailure(e, archive, password);
        }
        catch
        {
            TryDelete(target);
            throw;
        }
    }

    /// <summary>True if any entry is encrypted or if even the file list is protected.</summary>
    static bool NeedsPassword(string path)
    {
        try
        {
            using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions());
            return archive.Entries.Any(e => e.IsEncrypted);
        }
        catch (CryptographicException)
        {
            return true;
        }
    }

    static void ExtractIndexed(string path, string target, ReaderOptions options, IProgress<ExtractProgress> progress, CancellationToken cancel)
    {
        using var archive = ArchiveFactory.OpenArchive(path, options);
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        CheckRequiredFiles(path, archive, entries);
        var total = Math.Max(1, entries.Sum(e => Math.Max(0, e.Size)));
        DiskSpace.Ensure(target, total);
        long done = 0;
        foreach (var entry in entries)
        {
            progress.Report(new((int)(100 * done / total), entry.Key));
            using var input = entry.OpenEntryStream();
            Write(input, Destination(target, entry.Key), read =>
            {
                done += read;
                progress.Report(new((int)Math.Min(100, 100 * done / total), null));
            }, cancel);
        }
    }

    /// <summary>
    /// Rejects before extracting what cannot become a game: without any console's main file in the index (on
    /// PS5, eboot.bin) or, in a non-solid archive, with it unusable, such as encrypted. Only that file is read, in
    /// memory: the warning arrives in seconds and nothing is written to disk.
    /// </summary>
    static (IArchiveEntry Main, IConsolePlatform Platform) CheckRequiredFiles(string path, IArchive archive, List<IArchiveEntry> entries)
    {
        var (main, platform) = entries
            .Select(e => (Entry: e, Platform: ConsolePlatform.ForMainFile(EntryPath(e).Split('/')[^1])))
            .Where(m => m.Platform is not null)
            .OrderBy(m => EntryPath(m.Entry).Length)
            .FirstOrDefault();
        if (main is null)
            throw new UserException("err_no_eboot_archive", Path.GetFileName(path));
        if (!archive.IsSolid)
            platform!.CheckMainFile(ReadEntry(main));
        return (main, platform!);
    }

    static void ExtractStream(string path, string target, ReaderOptions options, IProgress<ExtractProgress> progress, CancellationToken cancel)
    {
        // Only an uncompressed .tar states the final size up front; for the others, a full drive is handled while writing.
        if (path.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            DiskSpace.Ensure(target, new FileInfo(path).Length);
        using var file = File.OpenRead(path);
        using var reader = ReaderFactory.OpenReader(file, options);
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
                continue;
            progress.Report(new((int)(100 * file.Position / Math.Max(1, file.Length)), reader.Entry.Key));
            using var input = reader.OpenEntryStream();
            // Without the final size, progress is the position in the archive; it also moves inside a
            // large entry, otherwise the bar would sit still until it finished.
            Write(input, Destination(target, reader.Entry.Key),
                _ => progress.Report(new((int)(100 * file.Position / Math.Max(1, file.Length)), null)), cancel);
        }
    }

    /// <summary>Final path of an entry; rejects entries that escape the folder (zip slip).</summary>
    static string Destination(string target, string? key)
    {
        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(target, (key ?? "").Replace('\\', '/').TrimStart('/')));
        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new UserException("err_archive_unsafe");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        return destination;
    }

    static void Write(Stream input, string destination, Action<int> written, CancellationToken cancel)
    {
        using var output = File.Create(destination);
        var buffer = new byte[1 << 20];
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            cancel.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            written(read);
        }
    }

    public static void TryDelete(string? folder)
    {
        try
        {
            if (folder is not null && Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
