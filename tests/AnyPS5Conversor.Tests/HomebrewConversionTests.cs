using System.Text.Json;
using AnyPS5Conversor;
using Xunit;

namespace AnyPS5Conversor.Tests;

/// <summary>
/// Converte de verdade os homebrews livres de build/homebrew.json, baixados por build/download-homebrew.ps1
/// para a pasta em ANYPS5_HOMEBREW. Confere tudo o que depende do nosso lado: o relinker aceita as opções que
/// passamos, a barra lê o progresso dele, o erro de syscall é reconhecido e a pasta final sai completa.
/// Sem a variável (testes do dia a dia), é pulado.
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
        Assert.SkipWhen(Homebrew is null, "ANYPS5_HOMEBREW não definido; rode build/download-homebrew.ps1");
        // A saída fica no mesmo disco dos homebrews para passar também pelos links físicos.
        var output = Path.Combine(Homebrew!, ".out", id);
        Directory.CreateDirectory(output);
        var log = new List<string>();
        try
        {
            var game = GameInfo.Load(Path.Combine(Homebrew!, id));
            string exe;
            try
            {
                exe = await Converter.ConvertAsync(game, output, false, new Progress<ConversionProgress>(),
                    line => { lock (log) log.Add(line); }, TestContext.Current.CancellationToken);
            }
            catch (UserException e) when (expect == "syscall" && e.Key == "err_syscall")
            {
                // Esperado: o título fala direto com o sistema do PS5 e o aviso certo foi reconhecido.
                return;
            }
            catch (UserException e)
            {
                // A mensagem do relinker e o fim do registro vão para o resultado: é o que a issue de falha mostra.
                lock (log)
                    Assert.Fail($"{id}: {e.Key} {string.Join(" | ", e.Args)}{Environment.NewLine}{string.Join(Environment.NewLine, log.TakeLast(15))}");
                throw;
            }

            var folder = Path.GetDirectoryName(exe)!;
            Assert.True(new FileInfo(exe).Length > 0, "o .exe não foi gerado");
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
