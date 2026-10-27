namespace AnyPSConversor;

/// <summary>
/// A console the program converts: how to recognize one of its games and how to convert it. Today only PS5, with
/// AnyPS5. A new console is another implementation registered in <see cref="ConsolePlatform.All"/>; the window,
/// extraction, early rejection of archives and cleanup already ask the platform.
/// </summary>
public interface IConsolePlatform
{
    /// <summary>Short console name, such as "PS5".</summary>
    string Name { get; }

    /// <summary>File that identifies a game for this console (on PS5, eboot.bin).</summary>
    string MainFile { get; }

    /// <summary>
    /// Checks the already read main file and throws <see cref="UserException"/> if it cannot be converted
    /// (on PS5, encrypted). Used before extracting a whole archive.
    /// </summary>
    void CheckMainFile(byte[] data);

    /// <summary>Whether the file, relative to the main file's folder, is an executable the conversion processes.</summary>
    bool IsExecutable(string relativePath);

    /// <summary>
    /// Game name, ID and icon read from inside an archive, before extracting.
    /// <paramref name="read"/> returns a file by its path relative to the main file's folder, or null.
    /// </summary>
    GamePreview Preview(Func<string, byte[]?> read);

    /// <summary>Reads the game at <paramref name="path"/>; throws "err_no_eboot" if it is not a game for this console.</summary>
    GameInfo Load(string path, string? fallbackName);

    /// <summary>Converts the game and returns the path of the generated executable.</summary>
    Task<string> ConvertAsync(GameInfo game, string parentFolder, bool sourceIsExtraction,
        IProgress<ConversionProgress> progress, Action<string> log, CancellationToken cancel);
}

/// <summary>What can be shown of a game before extracting it.</summary>
public sealed record GamePreview(string? Name, string? TitleId, byte[]? Icon, bool HasInfo);

public static class ConsolePlatform
{
    public static readonly IReadOnlyList<IConsolePlatform> All = [Ps5Platform.Instance];

    /// <summary>Reads the game with the first console that recognizes it.</summary>
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
                // Not for this console; try the next one. The last one explains what is missing.
            }
        }
        throw new UserException("err_no_eboot");
    }

    /// <summary>Console whose main file has this name, or null.</summary>
    public static IConsolePlatform? ForMainFile(string fileName) =>
        All.FirstOrDefault(p => p.MainFile.Equals(fileName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>PS5 with AnyPS5: eboot.bin, sce_sys/param.json, the relinker and the prx-windows libraries.</summary>
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
            // Other SELF defects are explained during conversion, with the full log.
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
