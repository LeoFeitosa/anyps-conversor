namespace AnyPSConversor;

/// <summary>
/// Um console que o programa converte: como reconhecer um jogo dele e como convertê-lo. Hoje só o PS5, com o
/// AnyPS5. Um console novo é outra implementação registrada em <see cref="ConsolePlatform.All"/>; a janela, a
/// extração, a recusa antecipada de arquivos compactados e a limpeza já perguntam à plataforma.
/// </summary>
public interface IConsolePlatform
{
    /// <summary>Nome curto do console, como "PS5".</summary>
    string Name { get; }

    /// <summary>Arquivo que identifica um jogo deste console (no PS5, o eboot.bin).</summary>
    string MainFile { get; }

    /// <summary>
    /// Confere o arquivo principal já lido e lança <see cref="UserException"/> se ele não pode ser convertido
    /// (no PS5, criptografado). Usado antes de extrair um arquivo compactado inteiro.
    /// </summary>
    void CheckMainFile(byte[] data);

    /// <summary>Se o arquivo, relativo à pasta do arquivo principal, é um executável que a conversão processa.</summary>
    bool IsExecutable(string relativePath);

    /// <summary>
    /// Nome, código e ícone do jogo lidos de dentro de um arquivo compactado, antes de extrair.
    /// <paramref name="read"/> devolve um arquivo pelo caminho relativo à pasta do arquivo principal, ou nulo.
    /// </summary>
    GamePreview Preview(Func<string, byte[]?> read);

    /// <summary>Lê o jogo em <paramref name="path"/>; lança "err_no_eboot" se não for um jogo deste console.</summary>
    GameInfo Load(string path, string? fallbackName);

    /// <summary>Converte o jogo e devolve o caminho do executável gerado.</summary>
    Task<string> ConvertAsync(GameInfo game, string parentFolder, bool sourceIsExtraction,
        IProgress<ConversionProgress> progress, Action<string> log, CancellationToken cancel);
}

/// <summary>O que dá para mostrar de um jogo antes de extraí-lo.</summary>
public sealed record GamePreview(string? Name, string? TitleId, byte[]? Icon, bool HasInfo);

public static class ConsolePlatform
{
    public static readonly IReadOnlyList<IConsolePlatform> All = [Ps5Platform.Instance];

    /// <summary>Lê o jogo com o primeiro console que o reconhecer.</summary>
    public static GameInfo Load(string path, string? fallbackName = null)
    {
        foreach (var platform in All)
        {
            try
            {
                return platform.Load(path, fallbackName);
            }
            catch (UserException e) when (e.Key == "err_no_eboot" && platform != All[^1])
            {
                // Não é deste console; tenta o próximo. O último explica o que falta.
            }
        }
        throw new UserException("err_no_eboot");
    }

    /// <summary>Console cujo arquivo principal tem este nome, ou nulo.</summary>
    public static IConsolePlatform? ForMainFile(string fileName) =>
        All.FirstOrDefault(p => p.MainFile.Equals(fileName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>PS5 com o AnyPS5: eboot.bin, sce_sys/param.json, o relinker e as bibliotecas prx-windows.</summary>
public sealed class Ps5Platform : IConsolePlatform
{
    public static readonly Ps5Platform Instance = new();

    Ps5Platform() { }

    public string Name => "PS5";

    public string MainFile => "eboot.bin";

    public void CheckMainFile(byte[] data)
    {
        if (!SelfUnwrapper.IsSelf(data))
            return;
        try
        {
            SelfUnwrapper.Unwrap(data);
        }
        catch (EncryptedSelfException)
        {
            throw new UserException("err_encrypted", MainFile);
        }
        catch (SelfException)
        {
            // Outros defeitos do SELF são explicados na conversão, com o registro completo.
        }
    }

    public bool IsExecutable(string relativePath) =>
        relativePath.Equals(MainFile, StringComparison.OrdinalIgnoreCase)
        || (GameInfo.IsModuleFile(relativePath)
            && GameInfo.ModuleDirectories.Any(d => relativePath.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase)));

    public GamePreview Preview(Func<string, byte[]?> read)
    {
        var param = read("sce_sys/param.json");
        var (titleId, name) = param is null ? (null, null) : GameInfo.ReadParam(System.Text.Encoding.UTF8.GetString(param));
        return new(name ?? titleId, titleId, read("sce_sys/icon0.png"), param is not null);
    }

    public GameInfo Load(string path, string? fallbackName) => GameInfo.Load(path, fallbackName);

    public Task<string> ConvertAsync(GameInfo game, string parentFolder, bool sourceIsExtraction,
        IProgress<ConversionProgress> progress, Action<string> log, CancellationToken cancel) =>
        Converter.ConvertAsync(game, parentFolder, sourceIsExtraction, progress, log, cancel);
}
