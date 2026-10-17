using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace AnyPSConversor;

/// <summary>
/// Andamento da conversão: fração de 0 a 1 (negativa = indeterminada) e chave de status. O passo é a linha de
/// detalhe embaixo do status: chave de texto (ou nula) e o arquivo ou item da vez.
/// </summary>
public readonly record struct ConversionProgress(double Fraction, string StatusKey, params object[] Args)
{
    public string? StepKey { get; init; }
    public string? StepItem { get; init; }
}

/// <summary>Um passo de uma etapa: fração dela, chave de texto do passo e o arquivo ou item da vez.</summary>
public delegate void StepReport(double fraction, string? stepKey, string? item);

/// <summary>
/// Converte a pasta de um jogo de PS5 para Windows: desembrulha os SELF, roda o relinker do AnyPS5,
/// copia os arquivos do jogo para app0/ e instala as bibliotecas do AnyPS5 em libs/.
/// </summary>
public static class Converter
{
    static string? anyPS5Version;

    public static string AnyPS5Version => anyPS5Version ??= ReadResourceText("anyps5-version").Trim();

    public static string ThirdPartyNotices => ReadResourceText("third-party-notices");

    /// <param name="sourceIsExtraction">
    /// A pasta do jogo é uma extração nossa e pode ser consumida: no mesmo disco, os arquivos são movidos para
    /// app0/ em vez de copiados, o que é instantâneo e não ocupa espaço a mais.
    /// </param>
    public static async Task<string> ConvertAsync(
        GameInfo game,
        string parentFolder,
        bool sourceIsExtraction,
        IProgress<ConversionProgress> progress,
        Action<string> log,
        CancellationToken cancel)
    {
        // Salvar dentro da pasta do jogo misturaria o convertido com o original e faria a cópia ler o que ela
        // mesma está gravando.
        if (SourceCleanup.IsSameOrInside(SourceCleanup.Normalize(parentFolder), SourceCleanup.Normalize(game.Folder)))
            throw new UserException("err_output_in_game");
        // Pastas de trabalho são apagadas quando a execução dona termina ou o programa abre de novo: o jogo
        // convertido não pode ficar nelas, nem o original escolhido ali direto (só a extração desta conversão).
        if (Archives.IsInsideWorkFolder(parentFolder))
            throw new UserException("err_output_in_work");
        if (!sourceIsExtraction && Archives.IsInsideWorkFolder(game.Folder))
            throw new UserException("err_game_in_work");
        var output = UniqueFolder(Path.Combine(parentFolder, game.SafeName));
        var move = sourceIsExtraction && SameVolume(game.Folder, parentFolder);
        var work = Path.Combine(Archives.TempRoot, "work-" + Guid.NewGuid().ToString("N"));
        try
        {
            progress.Report(new(0.02, "status_preparing"));
            EnsureSpace(game, parentFolder, move);
            var relinker = ExtractRelinker();
            log($"AnyPS5 {AnyPS5Version}: {relinker}");

            progress.Report(new(0.04, "status_unwrapping"));
            var converted = await Task.Run(() => PrepareInputs(game, work, log,
                file => progress.Report(new(0.04, "status_unwrapping") { StepKey = "step_reading", StepItem = file }), cancel), cancel);

            // O relinker usa um núcleo só e lê apenas a pasta de trabalho: a cópia do jogo para app0/ e as
            // bibliotecas correm ao mesmo tempo. A barra (10–98%) acompanha a mais atrasada das duas.
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
                    // Mostra a etapa mais atrasada, que é a que decide quando a conversão termina.
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
                    // Se uma etapa falha, a outra para na hora em vez de terminar à toa.
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
                // A etapa que parou por causa da outra só acusa "cancelado"; o erro que vale é o da que falhou.
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
            // A pasta de saída foi criada por nós e é nova: não deixa meio jogo para trás.
            // Apagar gigabytes leva tempo; fora da thread da janela, ela continua respondendo.
            await Task.Run(() => TryDelete(output));
            throw;
        }
        finally
        {
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
            // Um ícone que o relinker não aceita não deve impedir a conversão.
            log("icon0.png rejected; converting without the game icon");
            File.Delete(Path.Combine(work, "sce_sys", "icon0.png"));
            await RunRelinkerAsync(relinker, input, exe, fixedBase, log, report, cancel);
        }
    }

    /// <summary>
    /// Destino: cópia do jogo em app0/, bibliotecas e o .exe (estimado em duas vezes os executáveis).
    /// Temporário: os executáveis desembrulhados e o relinker.
    /// </summary>
    static void EnsureSpace(GameInfo game, string parentFolder, bool move) =>
        EnsureSpace(parentFolder, move ? 0 : DiskSpace.FolderSize(game.Folder), ExecutableBytes(game));

    /// <summary>
    /// Confere o espaço do processo inteiro, somado por disco. Destino: <paramref name="gameFiles"/> (o jogo em
    /// app0/, ou o jogo inteiro a extrair quando vem de um arquivo compactado), as bibliotecas e o .exe, estimado
    /// em duas vezes os executáveis. Temporário: os executáveis desembrulhados e o próprio relinker.
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

    /// <summary>Tamanho dos executáveis do jogo (o principal e os módulos): base do .exe e do tempo de conversão.</summary>
    public static long ExecutableBytes(GameInfo game) =>
        new FileInfo(Path.Combine(game.Folder, game.Platform.MainFile)).Length + game.Modules.Sum(m => new FileInfo(m).Length);

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
    static HashSet<string> PrepareInputs(GameInfo game, string work, Action<string> log, Action<string> reading, CancellationToken cancel)
    {
        Directory.CreateDirectory(work);
        reading("eboot.bin");
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

    /// <summary>Etapas que o relinker imprime, na ordem em que acontecem.</summary>
    static readonly string[] RelinkerStages = ["Instruction scan", "Syscall scan", "PRX conversion", "Output image", "Write output"];

    /// <summary>Fração de 0 a 1 de uma linha de progresso do relinker, como "PRX conversion | libc.prx | 57% (0/1)".</summary>
    public static double? RelinkerFraction(string line) => ParseRelinker(line)?.Fraction;

    /// <summary>Fração, etapa (índice em <see cref="RelinkerStages"/>) e item de uma linha de progresso do relinker.</summary>
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
    /// Títulos que leem o próprio app0/imports.txt supõem a imagem carregada em 0x400000, como no console
    /// (Ruffle, VLC e XPSemu na documentação do AnyPS5); para eles o .exe precisa de base fixa.
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
            // O relinker redesenha a barra de progresso com \r; cada pedaço atualiza a porcentagem
            // e só a última versão vai para o registro.
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
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancel);
        }
        catch (OperationCanceledException)
        {
            // Espera o relinker morrer de fato: o .exe pela metade só pode ser apagado depois.
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
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
    /// Leva o jogo para app0/. Extração nossa no mesmo disco: move. Pasta do usuário no mesmo disco: link físico,
    /// que é instantâneo e não ocupa espaço (o original continua intacto e apagar um não afeta o outro). Nos demais
    /// casos, ou se o disco não aceitar links, copia.
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
            // Somente leitura é atributo do arquivo, compartilhado pelo link: tirá-lo para apagar uma conversão
            // que falhou mexeria no original. Esses vão por cópia.
            var linked = link && !File.GetAttributes(file.Path).HasFlag(FileAttributes.ReadOnly)
                && (link = CreateHardLink(destination, file.Path, IntPtr.Zero));
            if (move)
                File.Move(file.Path, destination);
            else if (!linked)
            {
                using (var input = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan))
                using (var output = File.Create(destination))
                {
                    // Reserva o tamanho de uma vez: menos fragmentação, e o disco cheio aparece antes de gravar.
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

    /// <summary>Dados locais do programa: só os registros de conversões que falharam.</summary>
    public static string LogFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Settings.Name, "logs");

    /// <summary>
    /// Mantém no disco só o que o usuário pode precisar: os 10 registros de falha mais recentes, de até
    /// 30 dias. Remove também a cópia do relinker que as versões 0.1 a 0.3 guardavam por versão.
    /// </summary>
    public static void CleanLocalData()
    {
        // A medida de tempo de conversão das versões 0.9 e 0.10 deixou de ser usada: não fica no disco.
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

    /// <summary>O relinker vai embutido no .exe e é extraído para a pasta temporária desta execução.</summary>
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
