using System.Text.Json;
using AnyPSConversor;
using Xunit;

namespace AnyPSConversor.Tests;

/// <summary>
/// Actually converts the free homebrews from build/homebrew.json, downloaded by build/download-homebrew.ps1
/// into the folder in ANYPS5_HOMEBREW. Checks everything that depends on our side: the relinker accepts the options
/// we pass, the bar reads its progress, the syscall error is recognized and the final folder comes out complete.
/// Without the variable (everyday tests), it is skipped.
/// </summary>
public sealed class HomebrewConversionTests
{
    static readonly string? Homebrew = Environment.GetEnvironmentVariable("ANYPS5_HOMEBREW");

    public static TheoryData<string, string> Titles()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "build", "homebrew.json")))
            folder = folder.Parent;
        var data = new TheoryData<string, string>();
        if (folder is null)
            return data;
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder.FullName, "build", "homebrew.json")));
        foreach (var title in manifest.RootElement.GetProperty("titles").EnumerateArray())
            data.Add(title.GetProperty("id").GetString()!, title.GetProperty("expect").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Titles))]
    public async Task Converts(string id, string expect)
    {
        Assert.SkipWhen(Homebrew is null, "ANYPS5_HOMEBREW is not set; run build/download-homebrew.ps1");
        // The output goes on the same drive as the homebrews so it also exercises hard links.
        var output = Path.Combine(Homebrew!, ".out", id);
        Directory.CreateDirectory(output);
        var log = new List<string>();
        try
        {
            var game = ConsolePlatform.Load(Path.Combine(Homebrew!, id));
            string exe;
            try
            {
                exe = await game.Platform.ConvertAsync(game, output, false, new Progress<ConversionProgress>(),
                    line => { lock (log) log.Add(line); }, TestContext.Current.CancellationToken);
            }
            catch (UserException e) when (expect == "syscall" && e.Key == "err_syscall")
            {
                // Expected: the title talks directly to the PS5 system and the right warning was recognized.
                return;
            }
            catch (UserException e)
            {
                // The relinker message and the end of the log go into the result: it is what the failure issue shows.
                lock (log)
                    Assert.Fail($"{id}: {e.Key} {string.Join(" | ", e.Args)}{Environment.NewLine}{string.Join(Environment.NewLine, log.TakeLast(15))}");
                throw;
            }

            var folder = Path.GetDirectoryName(exe)!;
            Assert.True(new FileInfo(exe).Length > 0, "the .exe was not generated");
            Assert.True(File.Exists(Path.Combine(folder, "app0", "eboot.bin")), "app0/ incompleto");
            Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(folder, "libs"), "*", SearchOption.AllDirectories));
            Assert.Contains(log, line => Converter.RelinkerFraction(line) is not null);
            Assert.DoesNotContain(log, line => line.Contains("icon0.png rejected", StringComparison.Ordinal));
            if (Converter.NeedsFixedBase(game))
                Assert.Contains(log, line => line.Contains("--windows-fixed-base 0x400000", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }
}
