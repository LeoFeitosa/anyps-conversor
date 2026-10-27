using AnyPSConversor;
using Xunit;

namespace AnyPSConversor.Tests;

public sealed class GameInfoTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "anyps5-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(root, recursive: true);

    string CreateGame(string folder, string? paramJson)
    {
        var game = Path.Combine(root, folder);
        Directory.CreateDirectory(Path.Combine(game, "sce_sys"));
        Directory.CreateDirectory(Path.Combine(game, "sce_module"));
        File.WriteAllBytes(Path.Combine(game, "eboot.bin"), SelfUnwrapperTests.BuildSelf(i => (byte)(i % 7)));
        File.WriteAllBytes(Path.Combine(game, "sce_module", "libc.prx"), SelfUnwrapperTests.BuildSelf(i => (byte)(i % 5)));
        File.WriteAllText(Path.Combine(game, "sce_module", "readme.txt"), "not a module");
        if (paramJson is not null)
            File.WriteAllText(Path.Combine(game, "sce_sys", "param.json"), paramJson);
        return game;
    }

    [Fact]
    public void IgnoresBackupCopiesOfModules()
    {
        // Signing tools leave libc.prx.esbak next to libc.prx; passed along, the copy would make the
        // relinker reject the game for seeing two libc.prx.
        var game = CreateGame("PPSA00001", """{"titleId":"PPSA00001"}""");
        File.WriteAllBytes(Path.Combine(game, "sce_module", "libc.prx.esbak"), SelfUnwrapperTests.BuildSelf(i => (byte)(i % 5)));

        var info = GameInfo.Load(game);

        Assert.Equal("libc.prx", Path.GetFileName(Assert.Single(info.Modules)));
    }

    [Fact]
    public void FindsTheConsoleOfAGame()
    {
        var game = CreateGame("PPSA00001", """{"titleId":"PPSA00001"}""");

        Assert.Same(Ps5Platform.Instance, ConsolePlatform.Load(game).Platform);
        Assert.Same(Ps5Platform.Instance, ConsolePlatform.ForMainFile("EBOOT.BIN"));
        Assert.Null(ConsolePlatform.ForMainFile("leia-me.txt"));
        var empty = Directory.CreateDirectory(Path.Combine(root, "vazia")).FullName;
        Assert.Equal("err_no_eboot", Assert.Throws<UserException>(() => ConsolePlatform.Load(empty)).Key);
    }

    [Fact]
    public void ReadsTitleFromDefaultLanguage()
    {
        var game = CreateGame("PPSA00001", """
            {"titleId":"PPSA00001","localizedParameters":{"defaultLanguage":"pt-BR",
             "en-US":{"titleName":"Sample"},"pt-BR":{"titleName":"Exemplo/Jogo"}}}
            """);

        var info = GameInfo.Load(game);

        Assert.Equal("PPSA00001", info.TitleId);
        Assert.Equal("Exemplo/Jogo", info.Name);
        Assert.Equal("Exemplo Jogo", info.SafeName);
        Assert.True(info.HasParamJson);
        Assert.Single(info.Modules);
    }

    [Fact]
    public void AcceptsParentFolderWithSingleGame()
    {
        var game = CreateGame("PPSA00002", null);

        var info = GameInfo.Load(root);

        Assert.Equal(game, info.Folder);
        Assert.Equal("PPSA00002", info.Name);
        Assert.False(info.HasParamJson);
    }

    [Fact]
    public void FindsGameNestedInExtraFolders()
    {
        var game = CreateGame(Path.Combine("download", "release-1.0", "PPSA00003"), null);

        var info = GameInfo.Load(root, "Meu Jogo");

        Assert.Equal(game, info.Folder);
        Assert.Equal("Meu Jogo", info.Name);
    }

    [Fact]
    public void RejectsTwoGamesAtSameLevel()
    {
        CreateGame("PPSA00004", null);
        CreateGame("PPSA00005", null);

        var error = Assert.Throws<UserException>(() => GameInfo.Load(root));

        Assert.Equal("err_many_games", error.Key);
    }

    [Fact]
    public void DetectsTitlesThatNeedFixedBase()
    {
        var game = CreateGame("PPSA00006", null);
        Assert.False(Converter.NeedsFixedBase(GameInfo.Load(game)));

        File.WriteAllText(Path.Combine(game, "imports.txt"), "");
        Assert.True(Converter.NeedsFixedBase(GameInfo.Load(game)));
    }

    [Fact]
    public void RejectsFolderWithoutEboot()
    {
        Directory.CreateDirectory(root);

        var error = Assert.Throws<UserException>(() => GameInfo.Load(root));

        Assert.Equal("err_no_eboot", error.Key);
    }
}
