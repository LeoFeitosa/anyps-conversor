namespace AnyPSConversor;

/// <summary>
/// Opção "mover os originais para a Lixeira" ao fim da conversão. Só move o arquivo compactado escolhido
/// ou a pasta onde está o eboot.bin, nunca uma pasta acima dela, e recusa locais que não podem sumir.
/// </summary>
public static class SourceCleanup
{
    static readonly Environment.SpecialFolder[] Protected =
    [
        Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.Desktop, Environment.SpecialFolder.DesktopDirectory,
        Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyPictures,
        Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.Windows, Environment.SpecialFolder.System,
        Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ApplicationData,
        Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.CommonApplicationData,
    ];

    /// <summary>O que pode ir para a Lixeira: o arquivo compactado ou a pasta do jogo.</summary>
    /// <param name="input">O que o usuário escolheu.</param>
    /// <param name="gameFolder">Pasta com o eboot.bin (dentro da extração, quando veio de arquivo).</param>
    public static string SourceToRecycle(string input, string gameFolder) =>
        Archives.IsArchive(input) ? Path.GetFullPath(input) : Path.GetFullPath(gameFolder);

    /// <summary>Falso para raiz de disco, pastas do sistema ou do usuário e quando o destino está dentro da origem.</summary>
    public static bool IsSafeToRecycle(string source, string output, IEnumerable<string>? extraProtected = null)
    {
        var path = Normalize(source);
        if (path.Length == 0 || string.Equals(path, Normalize(Path.GetPathRoot(path) ?? ""), StringComparison.OrdinalIgnoreCase))
            return false;

        var target = Normalize(output);
        if (IsSameOrInside(target, path))
            return false;

        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var guarded = Protected.Select(Environment.GetFolderPath).Append(downloads).Concat(extraProtected ?? []);
        return !guarded.Where(p => p.Length > 0).Any(p => IsSameOrInside(Normalize(p), path));
    }

    public static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string path) =>
        path.Length == 0 ? "" : Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
