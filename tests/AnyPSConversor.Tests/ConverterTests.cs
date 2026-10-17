using AnyPSConversor;
using Xunit;

namespace AnyPSConversor.Tests;

public class ConverterTests
{
    [Fact]
    public async Task RefusesToSaveInsideTheGameFolder()
    {
        var game = Path.Combine(Path.GetTempPath(), "anyps-output-tests-" + Guid.NewGuid().ToString("N"), "PPSA00001");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "eboot.bin"), SelfUnwrapperTests.BuildSelf(i => (byte)(i % 7)));
        try
        {
            var info = GameInfo.Load(game);
            foreach (var output in new[] { game, Path.Combine(game, "saida") })
            {
                var error = await Assert.ThrowsAsync<UserException>(() => Converter.ConvertAsync(
                    info, output, false, new Progress<ConversionProgress>(), _ => { }, TestContext.Current.CancellationToken));
                Assert.Equal("err_output_in_game", error.Key);
            }
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(game)!, recursive: true);
        }
    }

    [Fact]
    public async Task RefusesWorkFoldersAsOutputOrGame()
    {
        var root = Path.Combine(Path.GetTempPath(), "anyps-work-tests-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(root, ".AnyPS5-Conversor", "run-1", "extract-x", "PPSA00001");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "eboot.bin"), SelfUnwrapperTests.BuildSelf(i => (byte)(i % 7)));
        try
        {
            var info = GameInfo.Load(game);
            var error = await Assert.ThrowsAsync<UserException>(() => Converter.ConvertAsync(
                info, root, false, new Progress<ConversionProgress>(), _ => { }, TestContext.Current.CancellationToken));
            Assert.Equal("err_game_in_work", error.Key);
            error = await Assert.ThrowsAsync<UserException>(() => Converter.ConvertAsync(
                info, Path.Combine(root, ".AnyPS-Conversor", "run-2"), true, new Progress<ConversionProgress>(), _ => { }, TestContext.Current.CancellationToken));
            Assert.Equal("err_output_in_work", error.Key);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Instruction scan       | libc.prx  |   0%", 0.0)]
    [InlineData("Syscall scan           | eboot.bin | 100%", 0.4)]
    [InlineData("PRX conversion         | libc.prx  |  50% (0/1)", 0.5)]
    [InlineData("Write output           | DOOM.exe  | 100%", 1.0)]
    public void ReadsRelinkerProgress(string line, double expected) =>
        Assert.Equal(expected, Converter.RelinkerFraction(line)!.Value, precision: 3);

    [Fact]
    public void ReadsRelinkerStageAndItem()
    {
        var step = Converter.ParseRelinker("PRX conversion         | libc.prx  |  57% (0/1)")!.Value;
        Assert.Equal(2, step.Stage);
        Assert.Equal("libc.prx", step.Item);
    }

    [Theory]
    [InlineData("Conversion complete")]
    [InlineData("NID input: 171 references")]
    [InlineData("Unknown stage | x | 50%")]
    public void IgnoresOtherLines(string line) => Assert.Null(Converter.RelinkerFraction(line));
}
