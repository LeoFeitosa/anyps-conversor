using System.Formats.Tar;
using System.IO.Compression;
using AnyPS5Conversor;
using Xunit;

namespace AnyPS5Conversor.Tests;

public sealed class ArchivesTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "anyps5-archive-tests-" + Guid.NewGuid().ToString("N"));
    readonly List<string> extracted = [];

    public ArchivesTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        Directory.Delete(root, recursive: true);
        extracted.ForEach(Archives.TryDelete);
    }

    string Extract(string archive)
    {
        var folder = Archives.Extract(archive, null, Archives.TempRoot, new Progress<ExtractProgress>(), CancellationToken.None);
        extracted.Add(folder);
        return folder;
    }

    string CreateGameFolder()
    {
        var game = Path.Combine(root, "source", "release", "PPSA00010");
        Directory.CreateDirectory(Path.Combine(game, "sce_module"));
        File.WriteAllBytes(Path.Combine(game, "eboot.bin"), SelfUnwrapperTests.BuildSelf(i => (byte)(i % 7)));
        File.WriteAllBytes(Path.Combine(game, "sce_module", "libc.prx"), SelfUnwrapperTests.BuildSelf(i => (byte)(i % 3)));
        return Path.Combine(root, "source");
    }

    [Fact]
    public void ExtractsZipWithNestedGame()
    {
        var zip = Path.Combine(root, "Jogo Teste.zip");
        ZipFile.CreateFromDirectory(CreateGameFolder(), zip);

        var info = GameInfo.Load(Extract(zip), "Jogo Teste");

        Assert.Equal("PPSA00010", Path.GetFileName(info.Folder));
        Assert.Equal("Jogo Teste", info.Name);
        Assert.Single(info.Modules);
    }

    [Fact]
    public void ExtractsTarGz()
    {
        var tarGz = Path.Combine(root, "game.tar.gz");
        using (var file = File.Create(tarGz))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
            TarFile.CreateFromDirectory(CreateGameFolder(), gzip, includeBaseDirectory: false);

        var info = GameInfo.Load(Extract(tarGz));

        Assert.Equal("PPSA00010", info.Name);
    }

    [Fact]
    public void RefusesEntriesOutsideTheTarget()
    {
        var zip = Path.Combine(root, "evil.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("jogo/eboot.bin").Open()))
                writer.Write("\u007FELF");
            using (var writer = new StreamWriter(archive.CreateEntry("../../evil.txt").Open()))
                writer.Write("x");
        }

        var error = Assert.Throws<UserException>(() => Archives.Extract(zip, null, Archives.TempRoot, new Progress<ExtractProgress>(), CancellationToken.None));

        Assert.Equal("err_archive_unsafe", error.Key);
    }

    /// <summary>Gerados com 7-Zip e WinRAR por build/generate-test-archives.ps1; a senha é "segredo123".</summary>
    [Theory]
    [InlineData("aes.zip")]
    [InlineData("zipcrypto.zip")]
    [InlineData("nomes-ocultos.7z")]
    [InlineData("dados.rar")]
    [InlineData("nomes-ocultos.rar")]
    public void AsksForPasswordAndRejectsWrongOne(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        var progress = new Progress<ExtractProgress>();

        Assert.False(Assert.Throws<PasswordException>(() => Archives.Extract(path, null, Archives.TempRoot, progress, CancellationToken.None)).Wrong);
        Assert.True(Assert.Throws<PasswordException>(() => Archives.Extract(path, "errada", Archives.TempRoot, progress, CancellationToken.None)).Wrong);

        var folder = Archives.Extract(path, "segredo123", Archives.TempRoot, progress, CancellationToken.None);
        extracted.Add(folder);
        Assert.True(File.Exists(Path.Combine(folder, "jogo", "sce_sys", "param.json")));
        Assert.Equal(5000, new FileInfo(Path.Combine(folder, "jogo", "dados.bin")).Length);
    }

    [Fact]
    public void CleansTempOfFinishedRuns()
    {
        var baseFolder = Path.GetDirectoryName(Archives.TempRoot)!;
        var dead = Path.Combine(baseFolder, "run-999999999");
        var legacy = Path.Combine(baseFolder, "extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dead);
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(Archives.TempRoot);

        Archives.CleanStaleTemp();

        Assert.False(Directory.Exists(dead));
        Assert.False(Directory.Exists(legacy));
        Assert.True(Directory.Exists(Archives.TempRoot));
    }

    [Fact]
    public void RefusesArchiveWithoutEbootBeforeExtracting()
    {
        var zip = Path.Combine(root, "sem-jogo.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("pasta/leia-me.txt").Open()))
            writer.Write("x");
        var before = Directory.Exists(Archives.TempRoot) ? Directory.GetDirectories(Archives.TempRoot).Length : 0;

        var error = Assert.Throws<UserException>(() => Archives.Extract(zip, null, Archives.TempRoot, new Progress<ExtractProgress>(), CancellationToken.None));

        Assert.Equal("err_no_eboot_archive", error.Key);
        Assert.Equal(before, Directory.GetDirectories(Archives.TempRoot).Length);
    }

    [Fact]
    public void RefusesEncryptedEbootBeforeExtracting()
    {
        var zip = Path.Combine(root, "criptografado.zip");
        var random = new Random(1);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var stream = archive.CreateEntry("PPSA00010/eboot.bin").Open())
            stream.Write(SelfUnwrapperTests.BuildSelf(_ => (byte)random.Next(256)));

        var error = Assert.Throws<UserException>(() => Archives.Extract(zip, null, Archives.TempRoot, new Progress<ExtractProgress>(), CancellationToken.None));

        Assert.Equal("err_encrypted", error.Key);
    }

    [Fact]
    public void ReportsEachFileWhileExtracting()
    {
        var zip = Path.Combine(root, "game.zip");
        ZipFile.CreateFromDirectory(CreateGameFolder(), zip);
        var entries = new List<string>();

        extracted.Add(Archives.Extract(zip, null, Archives.TempRoot, new Sync(p => { if (p.Entry is not null) entries.Add(p.Entry); }), CancellationToken.None));

        Assert.Contains(entries, e => e.EndsWith("eboot.bin", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.EndsWith("libc.prx", StringComparison.Ordinal));
    }

    /// <summary>Progress sem contexto de sincronização: os avisos chegam na hora, na mesma thread.</summary>
    sealed class Sync(Action<ExtractProgress> report) : IProgress<ExtractProgress>
    {
        public void Report(ExtractProgress value) => report(value);
    }

    [Fact]
    public void ExtractsIntoAHiddenWorkFolderInTheOutputAndRemovesIt()
    {
        var output = Path.Combine(root, "saida");
        var zip = Path.Combine(root, "game.zip");
        ZipFile.CreateFromDirectory(CreateGameFolder(), zip);

        var folder = Archives.Extract(zip, null, Archives.WorkRoot(output), new Progress<ExtractProgress>(), CancellationToken.None);

        var work = Path.Combine(output, ".AnyPS5-Conversor");
        Assert.StartsWith(work, folder);
        if (OperatingSystem.IsWindows())
            Assert.True(File.GetAttributes(work).HasFlag(FileAttributes.Hidden));
        Archives.DeleteWorkRoot(output);
        Assert.False(Directory.Exists(work));
    }

    [Fact]
    public void CleansStaleWorkFoldersInTheOutput()
    {
        var output = Path.Combine(root, "saida");
        var dead = Path.Combine(output, ".AnyPS5-Conversor", "run-999999999", "extract-x");
        Directory.CreateDirectory(dead);

        Archives.CleanStaleTemp(output);

        Assert.False(Directory.Exists(Path.Combine(output, ".AnyPS5-Conversor")));
    }

    [Fact]
    public void RecognizesArchiveExtensions()
    {
        foreach (var name in new[] { "a.zip", "a.RAR", "a.7z", "a.tar.gz", "a.tgz" })
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, "");
            Assert.True(Archives.IsArchive(path), name);
        }
        Assert.False(Archives.IsArchive(Path.Combine(root, "missing.zip")));
    }
}
