using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace AnyPSConversor;

/// <summary>
/// Conversion progress: fraction from 0 to 1 (negative = indeterminate) and status key. The step is the detail
/// line below the status: text key (or null) and the current file or item.
/// </summary>
public readonly record struct ConversionProgress(double Fraction, string StatusKey, params object[] Args)
{
    public string? StepKey { get; init; }
    public string? StepItem { get; init; }
}

/// <summary>A step within a stage: its fraction, the step's text key and the current file or item.</summary>
public delegate void StepReport(double fraction, string? stepKey, string? item);

/// <summary>
/// Converts a PS5 game folder to Windows: unwraps the SELFs, runs the AnyPS5 relinker,
/// copies the game files to app0/ and installs the AnyPS5 libraries in libs/.
/// </summary>
public static class Converter
{
    /// <summary>Relinkers running now: when the program closes they die with it (<see cref="KillRunning"/>).</summary>
    static readonly HashSet<Process> Running = [];

    // ponytail: only covers normal closing; ending it from Task Manager leaves the relinker running until it
    // finishes on its own. A Job Object with KILL_ON_JOB_CLOSE covers that case, if needed.
    /// <summary>Game folder being generated right now; if the program closes midway, it is deleted.</summary>
    public static string? Unfinished { get; private set; }

    public static void KillRunning()
    {
        lock (Running)
            foreach (var process in Running)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
    }

    static string? anyPS5Version;

    public static string AnyPS5Version => anyPS5Version ??= ReadResourceText("anyps5-version").Trim();

    public static string ThirdPartyNotices => ReadResourceText("third-party-notices");

    /// <param name="sourceIsExtraction">
    /// The game folder is our own extraction and can be consumed: on the same drive, the files are moved to
    /// app0/ instead of copied, which is instant and takes no extra space.
    /// </param>
    public static async Task<string> ConvertAsync(
        GameInfo game,
        string parentFolder,
        bool sourceIsExtraction,
        IProgress<ConversionProgress> progress,
        Action<string> log,
        CancellationToken cancel)
    {
        // Saving inside the game folder would mix the converted output with the original and make the copy read what it
        // is itself writing.
        if (SourceCleanup.IsSameOrInside(SourceCleanup.Normalize(parentFolder), SourceCleanup.Normalize(game.Folder)))
            throw new UserException("err_output_in_game");
        // Work folders are deleted when the owning run ends or the program opens again: the converted game
        // cannot stay in them, nor an original chosen directly there (only this conversion's extraction).
        if (Archives.IsInsideWorkFolder(parentFolder))
            throw new UserException("err_output_in_work");
        if (!sourceIsExtraction && Archives.IsInsideWorkFolder(game.Folder))
            throw new UserException("err_game_in_work");
        var output = UniqueFolder(Path.Combine(parentFolder, game.SafeName));
        var move = sourceIsExtraction && SameVolume(game.Folder, parentFolder);
        var work = Path.Combine(Archives.TempRoot, "work-" + Guid.NewGuid().ToString("N"));
        Unfinished = output;
        try
        {
            progress.Report(new(0.02, "status_preparing"));
            EnsureSpace(game, parentFolder, move);
            var relinker = ExtractRelinker();
            log($"AnyPS5 {AnyPS5Version}: {relinker}");

            progress.Report(new(0.04, "status_unwrapping"));
            var converted = await Task.Run(() => PrepareInputs(game, work, log,
                file => progress.Report(new(0.04, "status_unwrapping") { StepKey = "step_reading", StepItem = file }), cancel), cancel);

            // The relinker uses a single core and reads only the work folder: copying the game to app0/ and the
            // libraries run at the same time. The bar (10–98%) follows the slower of the two.
            progress.Report(new(0.10, "status_relinking", 0));
            Directory.CreateDirectory(output);
            var exe = Path.Combine(output, game.SafeName + ".exe");
            double relinked = 0, copied = 0;
            (string? Key, string? Item) relinkStep = default, copyStep = default;
            var gate = new Lock();
            void Report(ref double stage, ref (string? Key, string? Item) step, double fraction, string? stepKey, string? item)
            {
                lock (gate)
                {
                    stage = fraction;
                    if (stepKey is not null)
                        step = (stepKey, item);
                    // Shows the slowest stage, which is the one that decides when the conversion ends.
                    var (key, low, shown) = relinked <= copied
                        ? ("status_relinking", relinked, relinkStep)
                        : ("status_copying", copied, copyStep);
                    progress.Report(new(0.10 + 0.88 * low, key, (int)(low * 100)) { StepKey = shown.Key, StepItem = shown.Item });
                }
            }

            using var failed = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            async Task Stage(Func<Task> run)
            {
                try
                {
                    await run();
                }
                catch
                {
                    // If one stage fails, the other stops right away instead of finishing for nothing.
                    failed.Cancel();
                    throw;
                }
            }
            var both = Task.WhenAll(
                Stage(() => RelinkAsync(relinker, work, exe, NeedsFixedBase(game), log,
                    (f, key, item) => Report(ref relinked, ref relinkStep, f, key, item), failed.Token)),
                Stage(() => Task.Run(() =>
                {
                    Report(ref copied, ref copyStep, 0, "step_libs", null);
                    InstallLibraries(output);
                    CopyGame(game.Folder, Path.Combine(output, "app0"), converted, move,
                        (f, key, item) => Report(ref copied, ref copyStep, f, key, item), failed.Token);
                }, failed.Token)));
            try
            {
                await both;
            }
            catch when (both.Exception?.InnerExceptions.FirstOrDefault(e => e is not OperationCanceledException) is { } cause)
            {
                // The stage that stopped because of the other only reports "canceled"; the error that counts is the one that failed.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(cause);
            }
            cancel.ThrowIfCancellationRequested();

            progress.Report(new(1, "status_done"));
            log($"OK: {exe}");
            return exe;
        }
        catch (Exception e) when (DiskSpace.IsDiskFull(e))
        {
            await Task.Run(() => TryDelete(output));
            throw DiskSpace.DiskFull(output);
        }
        catch
        {
            // The output folder was created by us and is new: don't leave half a game behind.
            // Deleting gigabytes takes time; off the window's thread, it keeps responding.
            await Task.Run(() => TryDelete(output));
            throw;
        }
        finally
        {
            Unfinished = null;
            TryDelete(work);
        }
    }

    static async Task RelinkAsync(string relinker, string work, string exe, bool fixedBase, Action<string> log, StepReport report, CancellationToken cancel)
    {
        var input = Path.Combine(work, "eboot.bin");
        try
        {
            await RunRelinkerAsync(relinker, input, exe, fixedBase, log, report, cancel);
        }
        catch (UserException e) when (e.Key == "err_relinker" && e.Args[0] is string why
            && why.Contains("icon", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(work, "sce_sys", "icon0.png")))
        {
            // An icon the relinker does not accept must not block the conversion.
            log("icon0.png rejected; converting without the game icon");
            File.Delete(Path.Combine(work, "sce_sys", "icon0.png"));
            await RunRelinkerAsync(relinker, input, exe, fixedBase, log, report, cancel);
        }
    }

    /// <summary>
    /// Destination: copy of the game in app0/, libraries and the .exe (estimated at twice the executables).
    /// Temporary: the unwrapped executables and the relinker.
    /// </summary>
    static void EnsureSpace(GameInfo game, string parentFolder, bool move) =>
        EnsureSpace(parentFolder, move ? 0 : DiskSpace.FolderSize(game.Folder), ExecutableBytes(game));

    /// <summary>
    /// Checks the space for the whole process, summed per drive. Destination: <paramref name="gameFiles"/> (the game in
    /// app0/, or the whole game to extract when it comes from an archive), the libraries and the .exe, estimated
    /// at twice the executables. Temporary: the unwrapped executables and the relinker itself.
    /// </summary>
    public static void EnsureSpace(string parentFolder, long gameFiles, long executables)
    {
        long libraries, relinker;
        using (var zip = new ZipArchive(OpenResource("prx-windows.zip"), ZipArchiveMode.Read))
            libraries = zip.Entries.Sum(e => e.Length);
        using (var resource = OpenResource("relinker.exe"))
            relinker = resource.Length;
        DiskSpace.EnsureAll([
            (parentFolder, gameFiles + libraries + 2 * executables),
            (Archives.TempRoot, executables + relinker),
        ]);
    }

    /// <summary>Size of the game's executables (the main one and the modules): basis for the .exe and the conversion time.</summary>
    public static long ExecutableBytes(GameInfo game) =>
        new FileInfo(Path.Combine(game.Folder, game.Platform.MainFile)).Length + game.Modules.Sum(m => new FileInfo(m).Length);

    /// <summary>Zen 2 instructions that games use without checking; without them the game does not open.</summary>
    public static string[] MissingCpuFeatures()
    {
        var missing = new List<string>();
        if (!Avx2.IsSupported) missing.Add("AVX2");
        if (!Fma.IsSupported) missing.Add("FMA");
        if (!Bmi1.IsSupported) missing.Add("BMI1");
        if (!Bmi2.IsSupported) missing.Add("BMI2");
        if (!Lzcnt.IsSupported) missing.Add("LZCNT");
        if (X86Base.IsSupported)
        {
            var (_, _, ecx, _) = X86Base.CpuId(1, 0);
            if ((ecx & (1 << 29)) == 0) missing.Add("F16C");
            if ((ecx & (1 << 22)) == 0) missing.Add("MOVBE");
        }
        return [.. missing];
    }

    static bool IsIntel()
    {
        if (!X86Base.IsSupported)
            return false;
        var (_, ebx, ecx, edx) = X86Base.CpuId(0, 0);
        Span<int> vendor = [ebx, edx, ecx];
        return System.Text.Encoding.ASCII.GetString(System.Runtime.InteropServices.MemoryMarshal.AsBytes(vendor)) == "GenuineIntel";
    }

    /// <summary>Builds the clean relinker input and returns the modules that became .guest.prx.</summary>
    static HashSet<string> PrepareInputs(GameInfo game, string work, Action<string> log, Action<string> reading, CancellationToken cancel)
    {
        Directory.CreateDirectory(work);
        reading("eboot.bin");
        WriteClean(Path.Combine(game.Folder, "eboot.bin"), Path.Combine(work, "eboot.bin"), log);

        // The relinker uses sce_sys/icon0.png, next to the entry executable, as the .exe icon.
        foreach (var name in new[] { "icon0.png", "param.json" })
        {
            var source = Path.Combine(game.Folder, "sce_sys", name);
            if (!File.Exists(source))
                continue;
            Directory.CreateDirectory(Path.Combine(work, "sce_sys"));
            File.Copy(source, Path.Combine(work, "sce_sys", name));
        }

        var converted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in game.Modules)
        {
            cancel.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(game.Folder, module);
            reading(relative);
            var target = Path.Combine(work, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            WriteClean(module, target, log);
            converted.Add(relative);
        }
        return converted;
    }

    static void WriteClean(string source, string target, Action<string> log)
    {
        var data = File.ReadAllBytes(source);
        if (SelfUnwrapper.IsSelf(data))
        {
            try
            {
                data = SelfUnwrapper.Unwrap(data);
            }
            catch (EncryptedSelfException)
            {
                throw new UserException("err_encrypted", Path.GetFileName(source));
            }
            catch (SelfException e)
            {
                throw new UserException("err_unwrap", Path.GetFileName(source), e.Message);
            }
            log($"SELF → ELF: {Path.GetFileName(source)} ({data.Length:N0} bytes)");
        }
        File.WriteAllBytes(target, data);
    }

    /// <summary>Stages the relinker prints, in the order they happen.</summary>
    static readonly string[] RelinkerStages = ["Instruction scan", "Syscall scan", "PRX conversion", "Output image", "Write output"];

    /// <summary>Fraction from 0 to 1 of a relinker progress line, such as "PRX conversion | libc.prx | 57% (0/1)".</summary>
    public static double? RelinkerFraction(string line) => ParseRelinker(line)?.Fraction;

    /// <summary>Fraction, stage (index into <see cref="RelinkerStages"/>) and item of a relinker progress line.</summary>
    public static (double Fraction, int Stage, string Item)? ParseRelinker(string line)
    {
        var parts = line.Split('|');
        if (parts.Length < 3)
            return null;
        var stage = Array.IndexOf(RelinkerStages, parts[0].Trim());
        var percent = System.Text.RegularExpressions.Regex.Match(parts[2], @"(\d+)%");
        if (stage < 0 || !percent.Success)
            return null;
        return ((stage + int.Parse(percent.Groups[1].Value) / 100.0) / RelinkerStages.Length, stage, parts[1].Trim());
    }

    /// <summary>
    /// Titles that read their own app0/imports.txt assume the image is loaded at 0x400000, as on the console
    /// (Ruffle, VLC and XPSemu in the AnyPS5 documentation); for them the .exe needs a fixed base.
    /// </summary>
    public static bool NeedsFixedBase(GameInfo game) => File.Exists(Path.Combine(game.Folder, "imports.txt"));

    static async Task RunRelinkerAsync(string relinker, string input, string exe, bool fixedBase, Action<string> log, StepReport report, CancellationToken cancel)
    {
        var start = new ProcessStartInfo(relinker)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(input)!,
        };
        start.ArgumentList.Add("--windows");
        start.ArgumentList.Add("--windows-gui");
        if (IsIntel())
            start.ArgumentList.Add("--to-intel");
        if (fixedBase)
        {
            start.ArgumentList.Add("--windows-fixed-base");
            start.ArgumentList.Add("0x400000");
        }
        start.ArgumentList.Add(input);
        start.ArgumentList.Add(exe);
        log("relinker " + string.Join(' ', start.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)));

        var failures = new List<string>();
        double best = 0;
        void Line(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;
            // The relinker redraws the progress bar with \r; each chunk updates the percentage
            // and only the last version goes to the log.
            var segments = line.Split('\r', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (ParseRelinker(segment) is { } step && step.Fraction >= best)
                {
                    best = step.Fraction;
                    report(step.Fraction, $"step_stage{step.Stage}", step.Item);
                }
            }
            var last = segments.LastOrDefault()?.TrimEnd();
            if (string.IsNullOrEmpty(last))
                return;
            if (last.StartsWith("FAIL", StringComparison.Ordinal))
                lock (failures) failures.Add(last);
            log(last);
        }

        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Line(e.Data);
        process.ErrorDataReceived += (_, e) => Line(e.Data);
        process.Start();
        lock (Running) Running.Add(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancel);
        }
        catch (OperationCanceledException)
        {
            // Waits for the relinker to actually die: the half-written .exe can only be deleted afterwards.
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            lock (Running) Running.Remove(process);
        }

        if (process.ExitCode == 0)
            report(1, null, null);
        if (process.ExitCode != 0 && failures.Any(f => f.Contains("Forbidden syscall", StringComparison.Ordinal)))
            throw new UserException("err_syscall", string.Join(Environment.NewLine, failures));
        if (process.ExitCode != 0)
            throw new UserException("err_relinker", failures.Count > 0 ? string.Join(Environment.NewLine, failures) : $"exit code {process.ExitCode}");
    }

    static bool SameVolume(string a, string b) =>
        string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Brings the game to app0/. Our extraction on the same drive: move. User folder on the same drive: hard link,
    /// which is instant and takes no space (the original stays intact and deleting one does not affect the other). In other
    /// cases, or if the drive does not support links, copy.
    /// </summary>
    static void CopyGame(string source, string target, HashSet<string> skip, bool move, StepReport report, CancellationToken cancel)
    {
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(f => (Path: f, Relative: Path.GetRelativePath(source, f)))
            .Where(f => !skip.Contains(f.Relative))
            .Select(f => (f.Path, f.Relative, Size: new FileInfo(f.Path).Length))
            .ToList();
        var total = Math.Max(1, files.Sum(f => f.Size));
        var link = !move && SameVolume(source, target);
        long done = 0;
        var buffer = new byte[4 << 20];
        foreach (var file in files)
        {
            cancel.ThrowIfCancellationRequested();
            var destination = Path.Combine(target, file.Relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var stepKey = move ? "step_moving" : "step_copying";
            report((double)done / total, stepKey, file.Relative);
            // Read-only is a file attribute, shared through the link: clearing it to delete a failed conversion
            // would touch the original. Those are copied.
            var linked = link && !File.GetAttributes(file.Path).HasFlag(FileAttributes.ReadOnly)
                && (link = CreateHardLink(destination, file.Path, IntPtr.Zero));
            if (move)
                File.Move(file.Path, destination);
            else if (!linked)
            {
                using (var input = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan))
                using (var output = File.Create(destination))
                {
                    // Reserves the size at once: less fragmentation, and a full drive shows up before writing.
                    output.SetLength(file.Size);
                    int read;
                    while ((read = input.Read(buffer)) > 0)
                    {
                        cancel.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, read);
                        done += read;
                        report((double)done / total, null, null);
                    }
                }
                File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(file.Path));
                continue;
            }
            done += file.Size;
            report((double)done / total, null, null);
        }
        report(1, null, null);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

    static void InstallLibraries(string output)
    {
        using var stream = OpenResource("prx-windows.zip");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        zip.ExtractToDirectory(output, overwriteFiles: true);
    }

    /// <summary>The program's local data: only the logs of failed conversions.</summary>
    public static string LogFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Settings.Name, "logs");

    /// <summary>
    /// Keeps on disk only what the user may need: the 10 most recent failure logs, up to
    /// 30 days old. Also removes the relinker copy that versions 0.1 to 0.3 kept per version.
    /// </summary>
    public static void CleanLocalData()
    {
        // The conversion time measurement from versions 0.9 and 0.10 is no longer used: it does not stay on disk.
        try
        {
            File.Delete(Path.Combine(Settings.Folder, "conversion-rate.txt"));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        var local = Path.GetDirectoryName(LogFolder)!;
        if (Directory.Exists(local))
            foreach (var folder in Directory.GetDirectories(local, "v*"))
                Archives.TryDelete(folder);
        if (!Directory.Exists(LogFolder))
            return;
        var logs = new DirectoryInfo(LogFolder).GetFiles("*.log").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
        foreach (var log in logs.Skip(10).Concat(logs.Take(10).Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30))))
        {
            try
            {
                log.Delete();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The relinker is embedded in the .exe and extracted to this run's temporary folder.</summary>
    static string ExtractRelinker()
    {
        var path = Path.Combine(Archives.TempRoot, "relinker.exe");
        if (File.Exists(path))
            return path;
        Directory.CreateDirectory(Archives.TempRoot);
        var temporary = path + ".tmp";
        using (var resource = OpenResource("relinker.exe"))
        using (var file = File.Create(temporary))
            resource.CopyTo(file);
        File.Move(temporary, path, overwrite: true);
        return path;
    }

    static Stream OpenResource(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"missing embedded resource {name}; run build/download-dependencies.ps1");

    static string ReadResourceText(string name)
    {
        using var reader = new StreamReader(OpenResource(name));
        return reader.ReadToEnd();
    }

    static string UniqueFolder(string path)
    {
        var candidate = path;
        for (var n = 2; Directory.Exists(candidate) || File.Exists(candidate); n++)
            candidate = $"{path} ({n})";
        return candidate;
    }

    static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
