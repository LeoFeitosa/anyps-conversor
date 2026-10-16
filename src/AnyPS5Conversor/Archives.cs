using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace AnyPS5Conversor;

/// <summary>O arquivo tem senha: nenhuma foi informada (<see cref="Wrong"/> falso) ou a informada não abre.</summary>
public sealed class PasswordException(bool wrong) : Exception("password required")
{
    public bool Wrong { get; } = wrong;
}

/// <summary>Extrai o jogo de um arquivo compactado (.zip, .rar, .7z, .tar e variações) para uma pasta temporária.</summary>
public static class Archives
{
    public static readonly string[] Extensions = [".zip", ".rar", ".7z", ".tar", ".tgz", ".gz", ".bz2", ".xz"];

    public static bool IsArchive(string path) =>
        File.Exists(path) && Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>Pasta temporária onde ficam as extrações desta execução.</summary>
    public static string TempRoot { get; } = Path.Combine(Path.GetTempPath(), "AnyPS5-Conversor");

    /// <param name="password">Senha do arquivo; nula até o usuário informar uma.</param>
    /// <exception cref="PasswordException">O arquivo tem senha e ela falta ou não confere.</exception>
    public static string Extract(string archive, string? password, IProgress<int> progress, CancellationToken cancel)
    {
        // Zip, rar e 7z têm índice e são abertos inteiros; tar, gz, bz2 e xz são lidos em sequência e não têm senha.
        var indexed = new[] { ".zip", ".rar", ".7z" }.Any(e => archive.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        if (indexed && password is null && NeedsPassword(archive))
            throw new PasswordException(wrong: false);

        var target = Path.Combine(TempRoot, "extract-" + Guid.NewGuid().ToString("N"));
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
        catch (Exception e) when (e is not OperationCanceledException and not UserException)
        {
            TryDelete(target);
            // Cada formato acusa a senha errada de um jeito (CryptographicException, "bad password",
            // erro de dados no 7z); com senha informada, qualquer falha de leitura é tratada como senha errada.
            if (password is not null)
                throw new PasswordException(wrong: true);
            if (e is CryptographicException)
                throw new PasswordException(wrong: false);
            throw new UserException("err_archive", Path.GetFileName(archive), e.Message);
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

    static void ExtractIndexed(string path, string target, ReaderOptions options, IProgress<int> progress, CancellationToken cancel)
    {
        using var archive = ArchiveFactory.OpenArchive(path, options);
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        var total = Math.Max(1, entries.Sum(e => Math.Max(0, e.Size)));
        long done = 0;
        foreach (var entry in entries)
        {
            using var input = entry.OpenEntryStream();
            done = Write(input, Destination(target, entry.Key), done, total, progress, cancel);
        }
    }

    static void ExtractStream(string path, string target, ReaderOptions options, IProgress<int> progress, CancellationToken cancel)
    {
        using var file = File.OpenRead(path);
        using var reader = ReaderFactory.OpenReader(file, options);
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
                continue;
            using var input = reader.OpenEntryStream();
            Write(input, Destination(target, reader.Entry.Key), 0, 1, null, cancel);
            progress.Report((int)(100 * file.Position / Math.Max(1, file.Length)));
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

    static long Write(Stream input, string destination, long done, long total, IProgress<int>? progress, CancellationToken cancel)
    {
        using var output = File.Create(destination);
        var buffer = new byte[1 << 20];
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            cancel.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            done += read;
            progress?.Report((int)Math.Min(100, 100 * done / total));
        }
        return done;
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
