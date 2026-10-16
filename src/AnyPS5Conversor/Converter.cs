using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Intrinsics.X86;

namespace AnyPS5Conversor;

/// <summary>Andamento da conversão: fração de 0 a 1 (negativa = indeterminada) e chave de status.</summary>
public readonly record struct ConversionProgress(double Fraction, string StatusKey, params object[] Args);

/// <summary>
/// Converte a pasta de um jogo de PS5 para Windows: desembrulha os SELF, roda o relinker do AnyPS5,
/// copia os arquivos do jogo para app0/ e instala as bibliotecas do AnyPS5 em libs/.
/// </summary>
public static class Converter
{
    static string? anyPS5Version;

    public static string AnyPS5Version => anyPS5Version ??= ReadResourceText("anyps5-version").Trim();

    public static async Task<string> ConvertAsync(
        GameInfo game,
        string parentFolder,
        IProgress<ConversionProgress> progress,
        Action<string> log,
        CancellationToken cancel)
    {
        var output = UniqueFolder(Path.Combine(parentFolder, game.SafeName));
        var work = Path.Combine(Path.GetTempPath(), "AnyPS5-Conversor", Guid.NewGuid().ToString("N"));
        try
        {
            progress.Report(new(0.02, "status_preparing"));
            var relinker = ExtractRelinker();
            log($"AnyPS5 {AnyPS5Version}: {relinker}");

            progress.Report(new(0.04, "status_unwrapping"));
            var converted = await Task.Run(() => PrepareInputs(game, work, log), cancel);

            // Faixas da barra: gerar o .exe 10–55%, copiar o jogo 55–93%, bibliotecas 93–100%.
            progress.Report(new(0.10, "status_relinking", 0));
            Directory.CreateDirectory(output);
            var exe = Path.Combine(output, game.SafeName + ".exe");
            void Report(double f) => progress.Report(new(0.10 + 0.45 * f, "status_relinking", (int)(f * 100)));
            try
            {
                await RunRelinkerAsync(relinker, Path.Combine(work, "eboot.bin"), exe, NeedsFixedBase(game), log, Report, cancel);
            }
            catch (UserException e) when (e.Key == "err_relinker" && e.Args[0] is string why
                && why.Contains("icon", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(work, "sce_sys", "icon0.png")))
            {
                // Um ícone que o relinker não aceita não deve impedir a conversão.
                log("icon0.png rejected; converting without the game icon");
                File.Delete(Path.Combine(work, "sce_sys", "icon0.png"));
                await RunRelinkerAsync(relinker, Path.Combine(work, "eboot.bin"), exe, NeedsFixedBase(game), log, Report, cancel);
            }

            await Task.Run(() => CopyGame(game.Folder, Path.Combine(output, "app0"), converted, progress, cancel), cancel);

            progress.Report(new(0.93, "status_libs"));
            await Task.Run(() => InstallLibraries(output), cancel);

            progress.Report(new(1, "status_done"));
            log($"OK: {exe}");
            return exe;
        }
        catch
        {
            // A pasta de saída foi criada por nós e é nova: não deixa meio jogo para trás.
            TryDelete(output);
            throw;
        }
        finally
        {
            TryDelete(work);
        }
    }

    /// <summary>Instruções do Zen 2 que os jogos usam sem conferir; sem elas o jogo não abre.</summary>
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

    /// <summary>Monta a entrada limpa do relinker e devolve os módulos que viraram .guest.prx.</summary>
    static HashSet<string> PrepareInputs(GameInfo game, string work, Action<string> log)
    {
        Directory.CreateDirectory(work);
        WriteClean(Path.Combine(game.Folder, "eboot.bin"), Path.Combine(work, "eboot.bin"), log);

        // O relinker usa sce_sys/icon0.png, ao lado do executável de entrada, como ícone do .exe.
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
            var relative = Path.GetRelativePath(game.Folder, module);
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

    /// <summary>Etapas que o relinker imprime, na ordem em que acontecem.</summary>
    static readonly string[] RelinkerStages = ["Instruction scan", "Syscall scan", "PRX conversion", "Output image", "Write output"];

    /// <summary>Fração de 0 a 1 de uma linha de progresso do relinker, como "PRX conversion | libc.prx | 57% (0/1)".</summary>
    public static double? RelinkerFraction(string line)
    {
        var parts = line.Split('|');
        if (parts.Length < 3)
            return null;
        var stage = Array.IndexOf(RelinkerStages, parts[0].Trim());
        var percent = System.Text.RegularExpressions.Regex.Match(parts[2], @"(\d+)%");
        if (stage < 0 || !percent.Success)
            return null;
        return (stage + int.Parse(percent.Groups[1].Value) / 100.0) / RelinkerStages.Length;
    }

    /// <summary>
    /// Títulos que leem o próprio app0/imports.txt supõem a imagem carregada em 0x400000, como no console
    /// (Ruffle, VLC e XPSemu na documentação do AnyPS5); para eles o .exe precisa de base fixa.
    /// </summary>
    public static bool NeedsFixedBase(GameInfo game) => File.Exists(Path.Combine(game.Folder, "imports.txt"));

    static async Task RunRelinkerAsync(string relinker, string input, string exe, bool fixedBase, Action<string> log, Action<double> report, CancellationToken cancel)
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
            // O relinker redesenha a barra de progresso com \r; cada pedaço atualiza a porcentagem
            // e só a última versão vai para o registro.
            var segments = line.Split('\r', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (RelinkerFraction(segment) is { } fraction && fraction > best)
                {
                    best = fraction;
                    report(fraction);
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
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancel);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode == 0)
            report(1);
        if (process.ExitCode != 0 && failures.Any(f => f.Contains("Forbidden syscall", StringComparison.Ordinal)))
            throw new UserException("err_syscall", string.Join(Environment.NewLine, failures));
        if (process.ExitCode != 0)
            throw new UserException("err_relinker", failures.Count > 0 ? string.Join(Environment.NewLine, failures) : $"exit code {process.ExitCode}");
    }

    static void CopyGame(string source, string target, HashSet<string> skip, IProgress<ConversionProgress> progress, CancellationToken cancel)
    {
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(f => (Path: f, Relative: Path.GetRelativePath(source, f)))
            .Where(f => !skip.Contains(f.Relative))
            .Select(f => (f.Path, f.Relative, Size: new FileInfo(f.Path).Length))
            .ToList();
        var total = Math.Max(1, files.Sum(f => f.Size));
        long done = 0;
        var buffer = new byte[4 << 20];
        foreach (var file in files)
        {
            var destination = Path.Combine(target, file.Relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var input = File.OpenRead(file.Path))
            using (var output = File.Create(destination))
            {
                int read;
                while ((read = input.Read(buffer)) > 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    done += read;
                    var fraction = (double)done / total;
                    progress.Report(new(0.55 + 0.38 * fraction, "status_copying", (int)(fraction * 100)));
                }
            }
            File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(file.Path));
        }
    }

    static void InstallLibraries(string output)
    {
        using var stream = OpenResource("prx-windows.zip");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        zip.ExtractToDirectory(output, overwriteFiles: true);
    }

    /// <summary>O relinker vai embutido no .exe; é extraído uma vez por versão do AnyPS5.</summary>
    static string ExtractRelinker()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnyPS5-Conversor", AnyPS5Version);
        var path = Path.Combine(folder, "relinker.exe");
        using var resource = OpenResource("relinker.exe");
        if (File.Exists(path) && new FileInfo(path).Length == resource.Length)
            return path;
        Directory.CreateDirectory(folder);
        var temporary = path + ".tmp";
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
