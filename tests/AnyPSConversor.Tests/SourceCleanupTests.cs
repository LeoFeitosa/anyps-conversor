using AnyPSConversor;
using Xunit;

namespace AnyPSConversor.Tests;

public sealed class SourceCleanupTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "anyps5-cleanup-tests-" + Guid.NewGuid().ToString("N"));

    public SourceCleanupTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void RecycleMovesFilesAndFoldersToTheRecycleBin()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Lixeira só existe no Windows");
        var file = Path.Combine(root, "jogo.rar");
        var folder = Path.Combine(root, "PPSA00001");
        File.WriteAllText(file, "x");
        Directory.CreateDirectory(folder);
        SourceCleanup.Recycle(file, IntPtr.Zero);
        SourceCleanup.Recycle(folder, IntPtr.Zero);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void RecyclesTheArchiveOrOnlyTheGameFolder()
    {
        var archive = Path.Combine(root, "game.zip");
        File.WriteAllText(archive, "");
        var game = Path.Combine(root, "downloads", "PPSA00001");

        Assert.Equal(archive, SourceCleanup.SourceToRecycle(archive, "/tmp/extracted/PPSA00001"));
        Assert.Equal(game, SourceCleanup.SourceToRecycle(Path.Combine(root, "downloads"), game));
    }

    [Fact]
    public void AllowsAnOrdinaryGameFolder()
    {
        var game = Path.Combine(root, "PPSA00001");

        Assert.True(SourceCleanup.IsSafeToRecycle(game, Path.Combine(root, "converted")));
    }

    [Fact]
    public void RefusesWhenTheOutputIsInsideTheSource()
    {
        var game = Path.Combine(root, "PPSA00001");

        Assert.False(SourceCleanup.IsSafeToRecycle(game, Path.Combine(game, "converted")));
        Assert.False(SourceCleanup.IsSafeToRecycle(game, game));
    }

    [Fact]
    public void RefusesDriveRootsAndUserFolders()
    {
        Assert.False(SourceCleanup.IsSafeToRecycle(Path.GetPathRoot(root)!, root));
        Assert.False(SourceCleanup.IsSafeToRecycle(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), root));
        Assert.False(SourceCleanup.IsSafeToRecycle(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), root));
        Assert.False(SourceCleanup.IsSafeToRecycle(root, Path.Combine(Path.GetTempPath(), "out"), [Path.Combine(root, "protected")]));
    }
}
