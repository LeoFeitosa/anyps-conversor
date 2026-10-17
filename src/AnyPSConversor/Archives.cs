using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace AnyPSConversor;

/// <summary>O arquivo tem senha: nenhuma foi informada (<see cref="Wrong"/> falso) ou a informada não abre.</summary>
public sealed class PasswordException(bool wrong) : Exception("password required")
{
    public bool Wrong { get; } = wrong;
}

/// <summary>Andamento da extração: porcentagem e, ao começar cada entrada, o caminho dela no arquivo.</summary>
public readonly record struct ExtractProgress(int Percent, string? Entry);

/// <summary>
/// O que se sabe de um arquivo compactado antes de extraí-lo: o jogo (nome e ícone, quando dá para ler sem
/// descompactar o arquivo inteiro), o tamanho a extrair e o dos executáveis. Os tamanhos vêm do índice e
/// servem para conferir o espaço do processo inteiro, dividir a barra e estimar o tempo; em tar, gz, bz2 e xz,
/// que não têm índice, ficam o tamanho do próprio arquivo e zero.
/// </summary>
public sealed record ArchivePreview(GamePreview? Game, long Size, long Executables);

/// <summary>
/// Extrai o jogo de um arquivo compactado (.zip, .rar, .7z, .tar e variações) para uma pasta de trabalho no
/// mesmo local onde o jogo convertido será salvo, para usar o disco escolhido pelo usuário e poder mover
/// (em vez de copiar) os arquivos para app0/.
/// </summary>
public static class Archives
{
    public static readonly string[] Extensions = [".zip", ".rar", ".7z", ".tar", ".tgz", ".gz", ".bz2", ".xz"];

    public static bool IsArchive(string path) =>
        File.Exists(path) && Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    static readonly string TempBase = Path.Combine(Path.GetTempPath(), Settings.Name);
    const string WorkFolderName = "." + Settings.Name;

    /// <summary>Nomes da versão 0.6 e anteriores: o que sobrou neles também é limpo.</summary>
    static readonly string LegacyTempBase = Path.Combine(Path.GetTempPath(), Settings.LegacyName);
    const string LegacyWorkFolderName = "." + Settings.LegacyName;
    static readonly string RunName = "run-" + Environment.ProcessId;

    /// <summary>
    /// Pasta temporária desta execução para o que é pequeno (relinker e executáveis desembrulhados). Fica
    /// numa subpasta com o número do processo para que <see cref="CleanStaleTemp"/> saiba o que apagar.
    /// </summary>
    public static string TempRoot { get; } = Path.Combine(TempBase, RunName);

    /// <summary>Pasta oculta desta execução dentro do local de saída, onde ficam as extrações.</summary>
    public static string WorkRoot(string outputParent)
    {
        var root = Path.Combine(outputParent, WorkFolderName);
        Directory.CreateDirectory(root);
        File.SetAttributes(root, File.GetAttributes(root) | FileAttributes.Hidden);
        return Path.Combine(root, RunName);
    }

    /// <summary>
    /// Verdadeiro dentro de uma pasta de trabalho (desta ou de outra execução, inclusive das versões antigas):
    /// tudo nela é apagado quando a execução dona termina ou na próxima vez que o programa abre.
    /// </summary>
    public static bool IsInsideWorkFolder(string path)
    {
        var full = SourceCleanup.Normalize(path);
        return full.Split(Path.DirectorySeparatorChar).Any(p =>
                   p.Equals(WorkFolderName, StringComparison.OrdinalIgnoreCase) || p.Equals(LegacyWorkFolderName, StringComparison.OrdinalIgnoreCase))
               || SourceCleanup.IsSameOrInside(full, SourceCleanup.Normalize(TempBase))
               || SourceCleanup.IsSameOrInside(full, SourceCleanup.Normalize(LegacyTempBase));
    }

    /// <summary>Apaga a pasta de trabalho desta execução e, se ficar vazia, a pasta oculta que a contém.</summary>
    public static void DeleteWorkRoot(string outputParent)
    {
        var root = Path.Combine(outputParent, WorkFolderName);
        TryDelete(Path.Combine(root, RunName));
        RemoveIfEmpty(root);
    }

    /// <summary>
    /// Apaga o que execuções anteriores deixaram quando foram encerradas à força ou o PC desligou: subpastas
    /// de processos que não existem mais em %TEMP% e na pasta de trabalho dos locais de saída indicados,
    /// além das pastas soltas das versões 0.1 e 0.2.
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
            // Uma versão antiga (AnyPS5-Conversor.exe) aberta ao mesmo tempo continua dona das pastas dela.
            string[] names = [System.Diagnostics.Process.GetCurrentProcess().ProcessName, Settings.Name, Settings.LegacyName];
            return pid == Environment.ProcessId || names.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Zip, rar e 7z têm índice e são abertos inteiros; tar, gz, bz2 e xz são lidos em sequência e não têm senha.</summary>
    static bool IsIndexed(string archive) =>
        new[] { ".zip", ".rar", ".7z" }.Any(e => archive.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Confere o arquivo sem extrair nada, para recusar na hora o que não é jogo e mostrar o jogo antes de
    /// converter. Pelo índice: sem o arquivo principal de nenhum console, recusa. Se o arquivo não é sólido, lê
    /// também o principal (recusa se criptografado), o nome e o ícone; num arquivo sólido isso exigiria
    /// descompactar quase tudo, e fica para a extração.
    /// </summary>
    /// <exception cref="PasswordException">O arquivo tem senha e ela falta ou não confere.</exception>
    public static ArchivePreview Inspect(string archive, string? password)
    {
        if (!IsIndexed(archive))
        {
            // Sem índice de arquivos: o .xz ainda diz o tamanho final; .tar já é ele; gz e bz2 não guardam.
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
    /// Erro de leitura traduzido para o usuário. Cada formato acusa a senha errada de um jeito
    /// (CryptographicException, "bad password", erro de dados no 7z); com senha informada, qualquer falha de
    /// leitura é tratada como senha errada.
    /// </summary>
    static Exception ReadFailure(Exception e, string archive, string? password) =>
        password is not null ? new PasswordException(wrong: true)
        : e is CryptographicException ? new PasswordException(wrong: false)
        : new UserException("err_archive", Path.GetFileName(archive), e.Message);

    /// <summary>
    /// Tamanho descompactado de um .xz, somado do índice que cada stream guarda no fim (formato .xz 1.0.4,
    /// seções 2.1.2 e 4). Lê só alguns bytes do fim do arquivo. Nulo se o arquivo não seguir o formato.
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
                // Preenchimento entre streams: blocos de 4 bytes zerados.
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
                // Antes dos blocos vem o cabeçalho de 12 bytes do stream; antes dele, o stream anterior.
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

    /// <param name="password">Senha do arquivo; nula até o usuário informar uma.</param>
    /// <param name="workRoot">Onde extrair; normalmente <see cref="WorkRoot"/> do local de saída.</param>
    /// <exception cref="PasswordException">O arquivo tem senha e ela falta ou não confere.</exception>
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

    /// <summary>Verdadeiro se alguma entrada é criptografada ou se até a lista de arquivos é protegida.</summary>
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
    /// Recusa antes de extrair o que não pode virar jogo: sem o arquivo principal de nenhum console no índice (no
    /// PS5, o eboot.bin) ou, num arquivo não sólido, com ele inutilizável, como criptografado. Só ele é lido, em
    /// memória: o aviso chega em segundos e nada é gravado no disco.
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
        // Só o .tar sem compressão diz o tamanho final de antemão; nos demais, o disco cheio é tratado ao gravar.
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
            // Sem o tamanho final, o andamento é a posição no arquivo compactado; anda também dentro de uma
            // entrada grande, senão a barra ficaria parada até ela terminar.
            Write(input, Destination(target, reader.Entry.Key),
                _ => progress.Report(new((int)(100 * file.Position / Math.Max(1, file.Length)), null)), cancel);
        }
    }

    /// <summary>Caminho final de uma entrada; recusa entradas que saiam da pasta (zip slip).</summary>
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
