using System.Runtime.InteropServices;

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

    /// <summary>
    /// Manda para a Lixeira. Se não couber nela (um arquivo de dezenas de GB costuma não caber), o Windows pergunta
    /// antes de apagar de vez; a API do VisualBasic com OnlyErrorDialogs pulava essa pergunta e apagava sem volta.
    /// </summary>
    public static void Recycle(string path, IntPtr owner)
    {
        var operation = new ShFileOperation
        {
            Owner = owner,
            Function = FoDelete,
            From = Path.GetFullPath(path) + "\0\0",
            Flags = FofAllowUndo | FofNoConfirmation | FofWantNukeWarning | FofSilent,
        };
        var result = SHFileOperation(ref operation);
        if (operation.AnyOperationsAborted || (File.Exists(path) || Directory.Exists(path)) && result == 0)
            throw new OperationCanceledException("kept by the user");
        if (result != 0)
            throw new IOException($"SHFileOperation error 0x{result:X}");
    }

    const uint FoDelete = 3;
    const ushort FofSilent = 0x4, FofNoConfirmation = 0x10, FofAllowUndo = 0x40, FofWantNukeWarning = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShFileOperation
    {
        public IntPtr Owner;
        public uint Function;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHFileOperation(ref ShFileOperation operation);
}
