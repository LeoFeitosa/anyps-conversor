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
        var folder = Archives.Extract(archive, new Progress<int>(), CancellationToken.None);
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
        using (var writer = new StreamWriter(archive.CreateEntry("../../evil.txt").Open()))
            writer.Write("x");

        var error = Assert.Throws<UserException>(() => Archives.Extract(zip, new Progress<int>(), CancellationToken.None));

        Assert.Equal("err_archive_unsafe", error.Key);
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
